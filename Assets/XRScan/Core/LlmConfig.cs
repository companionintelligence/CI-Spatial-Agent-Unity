using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace XRScan.Core
{
    /// <summary>
    /// Connection + generation settings for the LLM backend (Ollama today; OpenAI / Claude / CI later).
    /// The endpoint is never hardcoded to a machine. Resolution order: PlayerPrefs (the in-headset
    /// settings panel) → environment variable → this asset; at each level a full URL wins over the
    /// legacy host field. <b>https is the default scheme.</b> Plain http:// (e.g. a LAN Ollama on
    /// :11434) is accepted only in development builds for hosts on the
    /// <see cref="CompanionConfig.devCleartextHosts"/> allowlist; see <see cref="CleartextPolicy"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "LlmConfig", menuName = "XRScan/LLM Config")]
    public class LlmConfig : ScriptableObject
    {
        public const string PlayerPrefsEndpointKey = "XRScan.LlmEndpoint";
        public const string EnvEndpointKey = "XRSCAN_LLM_URL";

        /// <summary>Legacy host-only override, combined with <see cref="port"/> and <see cref="useHttps"/>.</summary>
        public const string PlayerPrefsHostKey = "XRScan.LlmHost";
        public const string EnvHostKey = "XRSCAN_LLM_HOST";

        public const string PlayerPrefsModelKey = "XRScan.LlmModel";
        public const string EnvModelKey = "XRSCAN_LLM_MODEL";

        public const string ServiceName = "llm";

        [Tooltip("LLM endpoint URL, e.g. https://ollama.example.lan or https://hub.example.lan/ollama. A bare host " +
                 "defaults to https. Plain http:// only works in development builds for hosts listed in CompanionConfig > " +
                 "Dev Cleartext Hosts. Deployment-specific: prefer the in-headset settings panel, PlayerPrefs '" +
                 PlayerPrefsEndpointKey + "' or env " + EnvEndpointKey + " over committing a personal host here.")]
        public string endpoint = "";

        [Tooltip("Legacy alternative to 'endpoint': a host name or IP, combined with the port and scheme below.")]
        public string host = "";
        public int port = 11434;

        [Tooltip("Scheme for the host/port form. Keep it on (https) unless a development build talks to an allow-listed LAN host.")]
        public bool useHttps = true;

        [FormerlySerializedAs("model")]
        [Tooltip("Default model (a local model on your own Ollama / Companion Hub). Overridable from the settings panel, " +
                 "PlayerPrefs '" + PlayerPrefsModelKey + "' or env " + EnvModelKey + ".")]
        public string defaultModel = "llava:7b";

        /// <summary>
        /// Effective model name: PlayerPrefs → environment → <see cref="defaultModel"/>. Read per request by
        /// the backends, so a change from the settings panel applies to the next question.
        /// (Lower-case to stay source-compatible with callers that read the former <c>model</c> field.)
        /// </summary>
        public string model => ConfigOverrides.Resolve(PlayerPrefsModelKey, EnvModelKey, defaultModel, out _);

        [TextArea]
        public string systemPrompt = "You are a helpful spatial companion embodied in the user's room. Answer briefly about the room and the objects you can see.";

        public int maxTokens = 300;
        public float temperature = 0.7f;
        public float requestTimeoutSeconds = 30f;

        /// <summary>Effective endpoint under the current build type and allowlist.</summary>
        public EndpointCheck ResolveEndpoint() =>
            ResolveEndpoint(CleartextPolicy.IsDevelopmentBuild, CleartextPolicy.DevAllowlist);

        /// <summary>Effective endpoint under an explicit build type and allowlist (testable).</summary>
        public EndpointCheck ResolveEndpoint(bool isDevelopmentBuild, IEnumerable<string> devAllowlist)
        {
            string raw = ResolveRaw(out string source);
            return CleartextPolicy.Evaluate(raw, isDevelopmentBuild, devAllowlist, source);
        }

        /// <summary>Host of the effective endpoint (for display); "(not configured)" when none parses.</summary>
        public string ResolveHost()
        {
            var check = ResolveEndpoint();
            return check.Url.IsValid ? check.Url.Host : "(not configured)";
        }

        /// <summary>
        /// Base URL for requests (no trailing slash). When the endpoint is unconfigured or blocked by the
        /// transport policy this logs the reason and returns a never-resolvable <c>.invalid</c> address, so a
        /// backend that ignores the status fails fast instead of sending prompts or images elsewhere.
        /// </summary>
        public string BaseUrl()
        {
            var check = ResolveEndpoint();
            if (!check.IsUsable) Debug.LogWarning("[LlmConfig] LLM endpoint unavailable: " + check.Message);
            return check.UrlOrUnavailable(ServiceName);
        }

        private string ResolveRaw(out string source)
        {
            string value = ConfigOverrides.GetOverride(PlayerPrefsEndpointKey);
            if (value != null) { source = ConfigOverrides.SourcePlayerPrefs; return value; }
            value = ConfigOverrides.GetOverride(PlayerPrefsHostKey);
            if (value != null) { source = ConfigOverrides.SourcePlayerPrefs; return ComposeFromHost(value); }

            value = ConfigOverrides.GetEnvironment(EnvEndpointKey);
            if (value != null) { source = ConfigOverrides.SourceEnvironment; return value; }
            value = ConfigOverrides.GetEnvironment(EnvHostKey);
            if (value != null) { source = ConfigOverrides.SourceEnvironment; return ComposeFromHost(value); }

            source = ConfigOverrides.SourceAsset;
            if (!string.IsNullOrWhiteSpace(endpoint)) return endpoint.Trim();
            if (!string.IsNullOrWhiteSpace(host)) return ComposeFromHost(host);
            return string.Empty;
        }

        /// <summary>
        /// Builds a URL from the legacy host form. A value that already carries a scheme is used as-is;
        /// otherwise the scheme comes from <see cref="useHttps"/> and the port from <see cref="port"/>
        /// unless the value names its own port.
        /// </summary>
        public string ComposeFromHost(string hostValue)
        {
            if (string.IsNullOrWhiteSpace(hostValue)) return string.Empty;
            string h = hostValue.Trim();
            if (h.IndexOf("://", StringComparison.Ordinal) >= 0) return h;

            bool bareIpv6 = !h.StartsWith("[", StringComparison.Ordinal) && h.IndexOf(':') != h.LastIndexOf(':');
            if (bareIpv6) h = "[" + h + "]";

            bool hasPort = h.StartsWith("[", StringComparison.Ordinal)
                ? h.IndexOf("]:", StringComparison.Ordinal) > 0
                : h.IndexOf(':') >= 0;

            string scheme = useHttps ? "https" : "http";
            return scheme + "://" + h + (hasPort || port <= 0 ? string.Empty : ":" + port);
        }
    }
}
