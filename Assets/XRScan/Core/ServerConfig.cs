using System;
using UnityEngine;

namespace XRScan.Core
{
    /// <summary>
    /// Connection + threshold settings for the remote CV server. The base URL is NOT
    /// hardcoded to any personal host: it resolves from (1) a PlayerPrefs override,
    /// (2) the XRSCAN_SERVER_URL environment variable, then (3) the serialized default
    /// (empty by design - set it per-deployment on the asset or at runtime).
    /// </summary>
    [CreateAssetMenu(fileName = "ServerConfig", menuName = "XRScan/Server Config")]
    public class ServerConfig : ScriptableObject
    {
        public const string PlayerPrefsBaseUrlKey = "XRScan.ServerBaseUrl";
        public const string EnvBaseUrlKey = "XRSCAN_SERVER_URL";

        [Tooltip("Default CV server base URL. Leave empty and override per-deployment via PlayerPrefs or the XRSCAN_SERVER_URL env var.")]
        public string baseUrl = "";
        public float boxThreshold = 0.35f;
        public float textThreshold = 0.25f;
        public int requestTimeoutSeconds = 6;
        public int retryCount = 2;
        public float retryDelaySeconds = 0.5f;

        public string ResolveBaseUrl()
        {
            var overrideUrl = PlayerPrefs.GetString(PlayerPrefsBaseUrlKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(overrideUrl))
            {
                return overrideUrl;
            }

            var envUrl = Environment.GetEnvironmentVariable(EnvBaseUrlKey);
            if (!string.IsNullOrWhiteSpace(envUrl))
            {
                return envUrl;
            }

            return baseUrl;
        }
    }
}
