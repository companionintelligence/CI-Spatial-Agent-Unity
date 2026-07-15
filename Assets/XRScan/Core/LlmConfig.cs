using System;
using UnityEngine;

namespace XRScan.Core
{
    /// <summary>
    /// Connection + generation settings for the LLM backend. Host is NOT hardcoded to any
    /// personal machine: it resolves from a PlayerPrefs override, then the XRSCAN_LLM_HOST env
    /// var, then the serialized default. Used by OllamaBackend (and future OpenAI/Claude/CI ones).
    /// </summary>
    [CreateAssetMenu(fileName = "LlmConfig", menuName = "XRScan/LLM Config")]
    public class LlmConfig : ScriptableObject
    {
        public const string PlayerPrefsHostKey = "XRScan.LlmHost";
        public const string EnvHostKey = "XRSCAN_LLM_HOST";

        [Tooltip("LLM host (e.g. Ollama). Leave empty and override via PlayerPrefs or XRSCAN_LLM_HOST.")]
        public string host = "";
        public int port = 11434;
        public string model = "llava:7b";

        [TextArea]
        public string systemPrompt = "You are a helpful spatial companion embodied in the user's room. Answer briefly about the room and the objects you can see.";

        public int maxTokens = 300;
        public float temperature = 0.7f;
        public float requestTimeoutSeconds = 30f;

        public string ResolveHost()
        {
            var overrideHost = PlayerPrefs.GetString(PlayerPrefsHostKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(overrideHost)) return overrideHost;

            var envHost = Environment.GetEnvironmentVariable(EnvHostKey);
            if (!string.IsNullOrWhiteSpace(envHost)) return envHost;

            return string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host;
        }

        public string BaseUrl() => "http://" + ResolveHost() + ":" + port;
    }
}
