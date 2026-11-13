using System.Collections.Generic;
using UnityEngine;

namespace XRScan.Core
{
    /// <summary>
    /// The shipping configuration of the companion: which config assets the real (non-mock) backends
    /// read, plus the inputs of the transport policy. Resolution order for every endpoint / option is
    /// PlayerPrefs (written by the in-headset settings panel) → environment variable → config asset;
    /// see <see cref="ConfigOverrides"/>.
    /// <para>Mocks are deliberately not configurable here: shipping builds always run the real backends
    /// (XRScan_Main). The mock backends live only in the development scene (XRScan_Dev).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "CompanionConfig", menuName = "XRScan/Companion Config")]
    public class CompanionConfig : ScriptableObject
    {
        [Header("Backends (real implementations)")]
        public ServerConfig server;
        public LlmConfig llm;
        public VoiceConfig voice;

        [Header("Development builds only")]
        [Tooltip("LAN hosts (IP or local name, e.g. a LAN Ollama) that may use plain http:// in DEVELOPMENT builds. " +
                 "Release builds ignore this list and allow https only. Entries that are not on the local network are rejected. " +
                 "The backends (UnityWebRequest) also need Player Settings > 'Allow downloads over HTTP' set to 'Allowed in " +
                 "development builds' to reach these hosts. The Android network security config mirrors this list for " +
                 "Java-level networking only. Deployment-specific: do not commit personal host names.")]
        public List<string> devCleartextHosts = new List<string>();

        [Header("In-headset settings panel")]
        [Tooltip("LLM model names the settings panel cycles through (local models served by your own Ollama / Companion Hub).")]
        public List<string> llmModelChoices = new List<string> { "llava:7b" };

        [Tooltip("Open the settings panel automatically at startup while the LLM or speech server is not configured.")]
        public bool openSettingsWhenUnconfigured = true;

        /// <summary>Applies the transport policy for the current build type; returns rejected (non-LAN) entries.</summary>
        public List<string> ApplyTransportPolicy() => ApplyTransportPolicy(CleartextPolicy.IsDevelopmentBuild);

        /// <summary>
        /// Development builds: makes <see cref="devCleartextHosts"/> the active cleartext allowlist.
        /// Release builds: clears it, so every endpoint must be https.
        /// </summary>
        public List<string> ApplyTransportPolicy(bool isDevelopmentBuild)
        {
            if (!isDevelopmentBuild)
            {
                CleartextPolicy.ClearDevAllowlist();
                return new List<string>();
            }
            return CleartextPolicy.SetDevAllowlist(devCleartextHosts);
        }

        /// <summary>The sanitized allowlist this asset yields (LAN hosts only), without applying it.</summary>
        public List<string> SanitizedDevCleartextHosts(out List<string> rejected) =>
            CleartextPolicy.Sanitize(devCleartextHosts, out rejected);

        /// <summary>True while the LLM or the speech-to-text endpoint is unusable (unset or blocked by policy).</summary>
        public bool NeedsSetup() => NeedsSetup(CleartextPolicy.IsDevelopmentBuild, CleartextPolicy.DevAllowlist);

        public bool NeedsSetup(bool isDevelopmentBuild, IEnumerable<string> devAllowlist)
        {
            if (llm == null || voice == null) return true;
            return !llm.ResolveEndpoint(isDevelopmentBuild, devAllowlist).IsUsable
                   || !voice.ResolveSttEndpoint(isDevelopmentBuild, devAllowlist).IsUsable;
        }

        /// <summary>One line per backend for the startup log (endpoints only; never keys or tokens).</summary>
        public List<string> DescribeEndpoints(bool isDevelopmentBuild, IEnumerable<string> devAllowlist)
        {
            var lines = new List<string>();
            lines.Add("LLM: " + (llm != null ? llm.ResolveEndpoint(isDevelopmentBuild, devAllowlist).ToString() : "no LlmConfig"));
            if (voice != null)
            {
                lines.Add("Speech (" + voice.EffectiveProvider + "): " + voice.ResolveSttEndpoint(isDevelopmentBuild, devAllowlist));
            }
            else
            {
                lines.Add("Speech: no VoiceConfig");
            }
            lines.Add("CV server: " + (server != null ? server.ResolveEndpoint(isDevelopmentBuild, devAllowlist).ToString() : "no ServerConfig"));
            return lines;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SanitizedDevCleartextHosts(out var rejected);
            if (rejected.Count > 0)
            {
                Debug.LogWarning("[CompanionConfig] Ignoring dev cleartext hosts that are not on the local network: " +
                                 string.Join(", ", rejected), this);
            }
        }
#endif
    }
}
