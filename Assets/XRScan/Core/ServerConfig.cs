using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace XRScan.Core
{
    /// <summary>
    /// Connection + threshold settings for the remote CV server. The base URL is NOT
    /// hardcoded to any personal host: it resolves from (1) a PlayerPrefs override (the in-headset
    /// settings panel), (2) the XRSCAN_SERVER_URL environment variable, then (3) the serialized
    /// default (empty by design - set it per-deployment on the asset or at runtime). Transport follows
    /// <see cref="CleartextPolicy"/>: https, or http only for allow-listed LAN hosts in development builds.
    /// The server's API token is runtime configuration only (<see cref="ResolveApiToken"/>): this asset has no
    /// token field, so no build ships one.
    /// </summary>
    [CreateAssetMenu(fileName = "ServerConfig", menuName = "XRScan/Server Config")]
    public class ServerConfig : ScriptableObject
    {
        public const string PlayerPrefsBaseUrlKey = "XRScan.ServerBaseUrl";
        public const string EnvBaseUrlKey = "XRSCAN_SERVER_URL";
        public const string ServiceName = "cv-server";

        /// <summary>PlayerPrefs key of the vision server's API token (entered in the in-headset settings panel).</summary>
        public const string PlayerPrefsApiTokenKey = "XRScan.ServerApiToken";

        /// <summary>Environment variable with the API token (Editor, desktop and CI runs).</summary>
        public const string EnvApiTokenKey = "XRSCAN_SERVER_TOKEN";

        [Tooltip("Default CV server base URL. Leave empty and override per-deployment via the settings panel, PlayerPrefs or the XRSCAN_SERVER_URL env var.")]
        public string baseUrl = "";
        public float boxThreshold = 0.35f;
        public float textThreshold = 0.25f;
        public int requestTimeoutSeconds = 6;
        public int retryCount = 2;
        public float retryDelaySeconds = 0.5f;

        /// <summary>Effective endpoint under the current build type and allowlist.</summary>
        public EndpointCheck ResolveEndpoint() =>
            ResolveEndpoint(CleartextPolicy.IsDevelopmentBuild, CleartextPolicy.DevAllowlist);

        public EndpointCheck ResolveEndpoint(bool isDevelopmentBuild, IEnumerable<string> devAllowlist)
        {
            string raw = ConfigOverrides.Resolve(PlayerPrefsBaseUrlKey, EnvBaseUrlKey, baseUrl, out string source);
            return CleartextPolicy.Evaluate(raw, isDevelopmentBuild, devAllowlist, source);
        }

        /// <summary>Base URL for requests under the current build type and allowlist; see the overload.</summary>
        public string ResolveBaseUrl() =>
            ResolveBaseUrl(CleartextPolicy.IsDevelopmentBuild, CleartextPolicy.DevAllowlist);

        /// <summary>
        /// Base URL for requests.
        /// <list type="bullet">
        /// <item>Usable endpoint: its URL.</item>
        /// <item>Configured but blocked by the transport policy (e.g. http:// in a release build): logs the
        /// reason and returns a never-resolvable <c>.invalid</c> address, so requests fail loudly instead of
        /// going out in cleartext.</item>
        /// <item>Not configured: empty in development builds and the Editor (callers treat that as "no CV
        /// server" and may fall back to a development mock). Release builds return the <c>.invalid</c>
        /// address instead, so the real server-backed detector is selected and reports the missing
        /// configuration rather than a mock detector silently inventing detections.</item>
        /// </list>
        /// </summary>
        public string ResolveBaseUrl(bool isDevelopmentBuild, IEnumerable<string> devAllowlist)
        {
            var check = ResolveEndpoint(isDevelopmentBuild, devAllowlist);
            if (check.IsUsable) return check.Url.ToString();
            if (check.Status == EndpointStatus.NotConfigured && isDevelopmentBuild) return string.Empty;

            string warning = "[ServerConfig] CV server endpoint unavailable: " + check.Message;
            if (warning != _lastWarning)
            {
                _lastWarning = warning;
                Debug.LogWarning(warning);
            }
            return EndpointCheck.UnavailableUrl(ServiceName);
        }

        /// <summary>
        /// The vision server's API token: the PlayerPrefs override written by the settings panel, then the
        /// <see cref="EnvApiTokenKey"/> environment variable. Never read from this asset. Empty when none is set.
        /// The server (cv-server/app/auth.py) refuses every request without it.
        /// </summary>
        public string ResolveApiToken() => ConfigOverrides.Resolve(PlayerPrefsApiTokenKey, EnvApiTokenKey, null, out _);

        /// <summary>True when an API token is configured from any source.</summary>
        public bool HasApiToken => ResolveApiToken().Length > 0;

        /// <summary>Adds "Authorization: Bearer &lt;token&gt;" to a request for this server when a token is set.</summary>
        public void ApplyAuthorization(UnityWebRequest request)
        {
            if (request == null) return;
            string token = ResolveApiToken();
            if (token.Length > 0) request.SetRequestHeader("Authorization", "Bearer " + token);
        }

        /// <summary>A user-facing reason for a failed request: its HTTP status, or 0 when the server was not reached.</summary>
        public static string DescribeFailure(long responseCode, string error)
        {
            switch (responseCode)
            {
                case 0:
                    return "Could not reach the vision server" + (string.IsNullOrEmpty(error) ? "." : ": " + error + ".");
                case 401:
                case 403:
                    return "The vision server refused the scan (" + responseCode + "). Check its token in Settings.";
                case 503:
                    return "The vision server is not ready (503). If it has no token configured, set CV_SERVER_API_TOKEN on the server.";
                default:
                    return "The vision server could not scan this image (" + responseCode + ").";
            }
        }

        [System.NonSerialized] private string _lastWarning;
    }
}
