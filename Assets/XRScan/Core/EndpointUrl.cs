using System;

namespace XRScan.Core
{
    /// <summary>
    /// A validated http(s) service endpoint: scheme, host, port and an optional base path.
    /// Parsing accepts "host", "host:port" or a full "https://host:port/path" URL; anything without
    /// a scheme defaults to <b>https</b>. Only http and https are accepted, and user-info
    /// (credentials), query strings and fragments are rejected so secrets never end up in a URL
    /// that is stored in PlayerPrefs or written to the log.
    /// </summary>
    public readonly struct EndpointUrl
    {
        public string Scheme { get; }
        public string Host { get; }
        public int Port { get; }

        /// <summary>Base path without a trailing slash ("" or e.g. "/ollama").</summary>
        public string Path { get; }

        public bool IsValid => !string.IsNullOrEmpty(Scheme) && !string.IsNullOrEmpty(Host);
        public bool IsHttps => Scheme == "https";
        public bool IsDefaultPort => Port == (IsHttps ? 443 : 80);

        private EndpointUrl(string scheme, string host, int port, string path)
        {
            Scheme = scheme;
            Host = host;
            Port = port;
            Path = path ?? string.Empty;
        }

        /// <summary>The host as it appears in a URL (IPv6 literals are bracketed).</summary>
        public string HostForUrl => Host != null && Host.IndexOf(':') >= 0 ? "[" + Host + "]" : Host;

        public override string ToString()
        {
            if (!IsValid) return string.Empty;
            return Scheme + "://" + HostForUrl + (IsDefaultPort ? string.Empty : ":" + Port) + Path;
        }

        /// <summary>Appends a relative path ("/v1/audio/speech") to this endpoint's base path.</summary>
        public string Combine(string relativePath)
        {
            if (!IsValid) return string.Empty;
            if (string.IsNullOrWhiteSpace(relativePath)) return ToString();
            string rel = relativePath.Trim();
            if (!rel.StartsWith("/", StringComparison.Ordinal)) rel = "/" + rel;
            return ToString() + rel;
        }

        /// <summary>Parses an endpoint. A missing scheme defaults to https.</summary>
        public static bool TryParse(string raw, out EndpointUrl url, out string error)
        {
            url = default;
            error = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "empty";
                return false;
            }

            string text = raw.Trim();
            if (text.IndexOf("://", StringComparison.Ordinal) < 0) text = "https://" + text;

            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            {
                error = "not a valid URL";
                return false;
            }

            string scheme = uri.Scheme.ToLowerInvariant();
            if (scheme != "http" && scheme != "https")
            {
                error = "unsupported scheme '" + scheme + "' (use https:// or http://)";
                return false;
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                error = "credentials in URLs are not supported (use the token / API key settings)";
                return false;
            }

            if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            {
                error = "query strings and fragments are not supported";
                return false;
            }

            string host = NormalizeHost(uri.Host);
            if (string.IsNullOrEmpty(host))
            {
                error = "missing host";
                return false;
            }

            string path = uri.AbsolutePath ?? string.Empty;
            path = path.TrimEnd('/');

            url = new EndpointUrl(scheme, host, uri.Port, path);
            return true;
        }

        /// <summary>Lower-cases a host and strips IPv6 brackets / a trailing dot.</summary>
        public static string NormalizeHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return string.Empty;
            string h = host.Trim().ToLowerInvariant();
            if (h.StartsWith("[", StringComparison.Ordinal) && h.EndsWith("]", StringComparison.Ordinal))
                h = h.Substring(1, h.Length - 2);
            if (h.EndsWith(".", StringComparison.Ordinal)) h = h.Substring(0, h.Length - 1);
            return h;
        }
    }
}
