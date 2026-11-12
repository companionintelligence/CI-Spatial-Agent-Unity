using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace XRScan.Core
{
    /// <summary>
    /// Transport policy shared by every backend endpoint (LLM, speech, CV server, glTF sync):
    /// <list type="bullet">
    /// <item>https:// is always allowed.</item>
    /// <item>Plain http:// is allowed only in <b>development</b> builds (and the Editor), only for
    /// hosts explicitly listed in <see cref="CompanionConfig.devCleartextHosts"/>, and only when the
    /// host is on the local network (loopback, RFC 1918 private, link-local, IPv6 unique-local, or a
    /// single-label / .local / .lan / .home.arpa / .internal name). Release builds never allow it.</item>
    /// </list>
    /// <para>This is the only per-host gate on the app's HTTP traffic. The backends send through
    /// UnityWebRequest, which on Android runs on Unity's native libcurl and ignores the Android network
    /// security config. The only other check on those requests is Unity's project-wide Player Settings &gt;
    /// "Allow downloads over HTTP", which the player enforces natively. A build guard (UnityHttpBuildGuard)
    /// fails release builds while that setting is "Always allowed". Development builds need "Allowed in
    /// development builds" to reach an allow-listed http host at all. So every request URL must come from this
    /// class, through <see cref="LlmConfig"/>, <see cref="VoiceConfig"/> or <see cref="ServerConfig"/>
    /// (TransportGateTests lists the call sites).</para>
    /// <para>The network security config generated at build time mirrors the same allowlist for Java-level
    /// networking only (Android plugins, WebView, VideoPlayer URL playback), as defense in depth. It does not
    /// govern UnityWebRequest.</para>
    /// </summary>
    public static class CleartextPolicy
    {
        private static readonly string[] LanSuffixes = { ".local", ".lan", ".home.arpa", ".internal", ".localdomain", ".localhost" };

        private static List<string> s_devAllowlist = new List<string>();

        /// <summary>The active development cleartext allowlist (normalized LAN hosts).</summary>
        public static IReadOnlyList<string> DevAllowlist => s_devAllowlist;

        /// <summary>True in the Editor and in development player builds.</summary>
        public static bool IsDevelopmentBuild => Debug.isDebugBuild;

        /// <summary>Replaces the active allowlist; returns the entries dropped because they are not LAN hosts.</summary>
        public static List<string> SetDevAllowlist(IEnumerable<string> hosts)
        {
            s_devAllowlist = Sanitize(hosts, out var rejected);
            return rejected;
        }

        /// <summary>Clears the active allowlist (cleartext then blocked everywhere).</summary>
        public static void ClearDevAllowlist() => s_devAllowlist = new List<string>();

        /// <summary>Normalizes entries (accepting "host", "host:port" or a URL) and keeps only LAN hosts, de-duplicated.</summary>
        public static List<string> Sanitize(IEnumerable<string> hosts, out List<string> rejected)
        {
            var kept = new List<string>();
            rejected = new List<string>();
            if (hosts == null) return kept;

            foreach (var entry in hosts)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;
                string host = ExtractHost(entry);
                if (string.IsNullOrEmpty(host) || !IsLanHost(host))
                {
                    rejected.Add(entry.Trim());
                    continue;
                }
                if (!kept.Contains(host)) kept.Add(host);
            }
            return kept;
        }

        /// <summary>True when the endpoint may use plain http under the given build type and allowlist.</summary>
        public static bool IsCleartextPermitted(string host, bool isDevelopmentBuild, IEnumerable<string> devAllowlist)
        {
            if (!isDevelopmentBuild) return false;
            string h = EndpointUrl.NormalizeHost(host);
            if (string.IsNullOrEmpty(h) || !IsLanHost(h)) return false;
            return Contains(devAllowlist, h);
        }

        /// <summary>Parses <paramref name="raw"/> and applies the transport policy.</summary>
        public static EndpointCheck Evaluate(string raw, bool isDevelopmentBuild, IEnumerable<string> devAllowlist, string source)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return EndpointCheck.Fail(EndpointStatus.NotConfigured, source, "No endpoint configured.");

            if (!EndpointUrl.TryParse(raw, out var url, out var error))
                return EndpointCheck.Fail(EndpointStatus.Invalid, source, "Not a usable http(s) endpoint: " + error + ".");

            if (url.IsHttps) return EndpointCheck.Ok(url, source);

            if (!isDevelopmentBuild)
                return EndpointCheck.Fail(EndpointStatus.CleartextBlocked, source,
                    url + " uses plain http://, which release builds never allow. Use an https:// endpoint.", url);

            if (!IsLanHost(url.Host))
                return EndpointCheck.Fail(EndpointStatus.CleartextBlocked, source,
                    url + " uses plain http:// to a non-LAN host. Use https://.", url);

            if (!Contains(devAllowlist, url.Host))
                return EndpointCheck.Fail(EndpointStatus.CleartextBlocked, source,
                    url + " uses plain http://. Add '" + url.Host + "' to CompanionConfig > Dev Cleartext Hosts " +
                    "(development builds only) or use https://.", url);

            return EndpointCheck.Ok(url, source);
        }

        /// <summary>Loopback, private, link-local and VPN-overlay addresses, or a local DNS name.</summary>
        public static bool IsLanHost(string host)
        {
            string h = EndpointUrl.NormalizeHost(host);
            if (string.IsNullOrEmpty(h)) return false;
            if (h == "localhost") return true;

            if (LooksLikeIpLiteral(h))
                return IPAddress.TryParse(h, out var ip) && IsLocalAddress(ip);

            // Single-label names ("nas", "ollama") only resolve through the local network's DNS
            // search domain; the listed suffixes are reserved / conventional for local networks.
            if (h.IndexOf('.') < 0) return IsValidHostLabel(h);
            foreach (var suffix in LanSuffixes)
            {
                if (h.EndsWith(suffix, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>True for 127.0.0.0/8, ::1 and "localhost".</summary>
        public static bool IsLoopback(string host)
        {
            string h = EndpointUrl.NormalizeHost(host);
            if (h == "localhost") return true;
            return LooksLikeIpLiteral(h) && IPAddress.TryParse(h, out var ip) && IPAddress.IsLoopback(ip);
        }

        private static bool IsLocalAddress(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip)) return true;

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                byte[] b = ip.GetAddressBytes();
                if (b[0] == 10) return true;                              // 10.0.0.0/8
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true; // 172.16.0.0/12
                if (b[0] == 192 && b[1] == 168) return true;              // 192.168.0.0/16
                if (b[0] == 169 && b[1] == 254) return true;              // 169.254.0.0/16 link-local
                // 100.64.0.0/10 (carrier-grade NAT / VPN overlays) is deliberately NOT treated as LAN:
                // on a CGNAT network that traffic crosses the ISP. Use https for overlay networks.
                return false;
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (ip.IsIPv4MappedToIPv6) return IsLocalAddress(ip.MapToIPv4());
                if (ip.IsIPv6LinkLocal) return true;                      // fe80::/10
                byte[] b = ip.GetAddressBytes();
                return (b[0] & 0xFE) == 0xFC;                             // fc00::/7 unique-local
            }

            return false;
        }

        private static bool LooksLikeIpLiteral(string host)
        {
            if (host.IndexOf(':') >= 0) return true; // IPv6
            int dots = 0;
            foreach (char c in host)
            {
                if (c == '.') dots++;
                else if (c < '0' || c > '9') return false;
            }
            return dots == 3;
        }

        private static bool IsValidHostLabel(string label)
        {
            if (label.Length == 0 || label.Length > 63) return false;
            foreach (char c in label)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-';
                if (!ok) return false;
            }
            return label[0] != '-' && label[label.Length - 1] != '-';
        }

        private static string ExtractHost(string entry)
        {
            string text = entry.Trim();
            if (text.IndexOf("://", StringComparison.Ordinal) >= 0 || text.IndexOf('/') >= 0 ||
                (text.StartsWith("[", StringComparison.Ordinal) && text.IndexOf("]:", StringComparison.Ordinal) > 0) ||
                (text.IndexOf(':') >= 0 && text.IndexOf(':') == text.LastIndexOf(':')))
            {
                // URL or host:port (a single colon means it is not a bare IPv6 literal).
                return EndpointUrl.TryParse(text, out var url, out _) ? url.Host : string.Empty;
            }
            return EndpointUrl.NormalizeHost(text);
        }

        private static bool Contains(IEnumerable<string> allowlist, string host)
        {
            if (allowlist == null) return false;
            foreach (var entry in allowlist)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;
                if (string.Equals(ExtractHost(entry), host, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
