using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace XRScan.Core
{
    /// <summary>Where speech audio (STT) and reply text (TTS) are processed.</summary>
    public enum SpeechProvider
    {
        /// <summary>Default: infrastructure you run (Companion Hub speech services or any OpenAI-compatible server).</summary>
        SelfHosted = 0,

        /// <summary>A third-party cloud provider. Requires the explicit opt-in.</summary>
        Cloud = 1
    }

    /// <summary>
    /// Endpoints + settings for server-backed speech (STT/TTS).
    /// <para><b>Privacy by default:</b> speech goes to a <b>self-hosted</b> OpenAI-compatible server
    /// (the Companion Hub speech services, speaches, faster-whisper-server, Kokoro, Piper…) at
    /// <c>{base}/v1/audio/transcriptions</c> and <c>{base}/v1/audio/speech</c>. A third-party cloud
    /// provider is used only when <see cref="provider"/> is Cloud <b>and</b> the explicit
    /// <see cref="cloudOptIn"/> is set (on the asset or from the in-headset settings panel). A
    /// "self-hosted" URL that points at a known cloud provider is treated as cloud, too.</para>
    /// <para>Resolution order for every value: PlayerPrefs → environment variable → this asset.
    /// Transport follows <see cref="CleartextPolicy"/> (https; http only for allow-listed LAN hosts in
    /// development builds). Keys and tokens are never serialized.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "VoiceConfig", menuName = "XRScan/Voice Config")]
    public class VoiceConfig : ScriptableObject
    {
        public const string PlayerPrefsBaseUrlKey = "XRScan.SpeechBaseUrl";
        public const string EnvBaseUrlKey = "XRSCAN_SPEECH_URL";
        public const string PlayerPrefsProviderKey = "XRScan.SpeechProvider";     // "selfhosted" | "cloud"
        public const string PlayerPrefsCloudOptInKey = "XRScan.SpeechCloudOptIn"; // "true" | "false"

        /// <summary>Cloud provider API key (only ever sent to the opted-in cloud provider).</summary>
        public const string PlayerPrefsApiKey = "XRScan.VoiceApiKey";
        public const string EnvApiKey = "XRSCAN_VOICE_API_KEY";

        /// <summary>Optional bearer token for the self-hosted speech server.</summary>
        public const string PlayerPrefsSelfHostedTokenKey = "XRScan.SpeechToken";
        public const string EnvSelfHostedTokenKey = "XRSCAN_SPEECH_TOKEN";

        public const string ServiceName = "speech";

        [Header("Privacy")]
        [Tooltip("SelfHosted (default) keeps audio on infrastructure you run: the Companion Hub speech services or any " +
                 "OpenAI-compatible server. Cloud sends audio and text to a third-party provider and only works with the opt-in below.")]
        public SpeechProvider provider = SpeechProvider.SelfHosted;

        [Tooltip("Explicit opt-in to send audio and text to a third-party cloud speech provider. Off by default.")]
        public bool cloudOptIn = false;

        [Header("Self-hosted speech (default)")]
        [Tooltip("Base URL of the self-hosted OpenAI-compatible speech server, e.g. the Companion Hub's speech service " +
                 "on your network. Include any API prefix the server uses (a Lemonade server is '<host>:<port>/api'); the " +
                 "STT / TTS paths below are appended. Deployment-specific: set it from the in-headset settings panel, PlayerPrefs '" +
                 PlayerPrefsBaseUrlKey + "' or env " + EnvBaseUrlKey + " rather than committing a personal host here.")]
        public string selfHostedBaseUrl = "";
        public string sttPath = "/v1/audio/transcriptions";
        public string ttsPath = "/v1/audio/speech";

        [Tooltip("Send a bearer token (PlayerPrefs '" + PlayerPrefsSelfHostedTokenKey + "' or env " + EnvSelfHostedTokenKey +
                 ") to the self-hosted server.")]
        public bool selfHostedRequiresToken = false;

        [Header("Cloud speech (explicit opt-in only)")]
        [Tooltip("Only used when the provider is Cloud AND the cloud opt-in is on.")]
        [FormerlySerializedAs("sttEndpoint")]
        public string cloudSttEndpoint = "https://api.openai.com/v1/audio/transcriptions";
        [Tooltip("Only used when the provider is Cloud AND the cloud opt-in is on.")]
        [FormerlySerializedAs("ttsEndpoint")]
        public string cloudTtsEndpoint = "https://api.openai.com/v1/audio/speech";

        [Header("Models / voice (OpenAI-compatible names)")]
        public string sttModel = "whisper-1";
        public string ttsModel = "tts-1";
        public string ttsVoice = "alloy";
        [Tooltip("Response audio format: mp3 or wav.")]
        public string ttsFormat = "mp3";

        [Header("Recording")]
        public int sampleRate = 16000;
        public int maxRecordingLength = 10;
        public float voiceThreshold = 0.01f;
        public float requestTimeoutSeconds = 30f;

        private string _lastWarning;

        /// <summary>Provider after the PlayerPrefs override ("selfhosted" / "cloud").</summary>
        public SpeechProvider EffectiveProvider
        {
            get
            {
                var value = ConfigOverrides.GetOverride(PlayerPrefsProviderKey);
                if (value == null) return provider;
                switch (value.Trim().ToLowerInvariant())
                {
                    case "cloud": return SpeechProvider.Cloud;
                    case "selfhosted": case "self-hosted": case "local": return SpeechProvider.SelfHosted;
                    default: return provider;
                }
            }
        }

        /// <summary>Cloud opt-in after the PlayerPrefs override.</summary>
        public bool EffectiveCloudOptIn => ConfigOverrides.GetBoolOverride(PlayerPrefsCloudOptInKey) ?? cloudOptIn;

        /// <summary>Effective speech-to-text endpoint (the URL a request may actually go to).</summary>
        public EndpointCheck ResolveSttEndpoint() =>
            ResolveSttEndpoint(CleartextPolicy.IsDevelopmentBuild, CleartextPolicy.DevAllowlist);

        public EndpointCheck ResolveSttEndpoint(bool isDevelopmentBuild, IEnumerable<string> devAllowlist) =>
            Resolve(true, isDevelopmentBuild, devAllowlist);

        /// <summary>Effective text-to-speech endpoint.</summary>
        public EndpointCheck ResolveTtsEndpoint() =>
            ResolveTtsEndpoint(CleartextPolicy.IsDevelopmentBuild, CleartextPolicy.DevAllowlist);

        public EndpointCheck ResolveTtsEndpoint(bool isDevelopmentBuild, IEnumerable<string> devAllowlist) =>
            Resolve(false, isDevelopmentBuild, devAllowlist);

        /// <summary>
        /// Effective STT URL read by the speech backends. Unconfigured or policy-blocked endpoints
        /// (including cloud without opt-in) yield a never-resolvable <c>.invalid</c> address, so no
        /// audio can leave the device; the reason is logged.
        /// </summary>
        public string sttEndpoint => Effective(ResolveSttEndpoint(), "speech-to-text");

        /// <summary>Effective TTS URL read by the speech backends (same guarantees as <see cref="sttEndpoint"/>).</summary>
        public string ttsEndpoint => Effective(ResolveTtsEndpoint(), "text-to-speech");

        /// <summary>
        /// True when the active provider needs a key / token: an opted-in cloud provider, or a self-hosted
        /// server configured to require a token. (Cloud without the opt-in never gets a request at all, so it
        /// needs no key; the endpoint check reports the missing opt-in instead.)
        /// </summary>
        public bool requiresApiKey => EffectiveProvider == SpeechProvider.Cloud
            ? EffectiveCloudOptIn
            : selfHostedRequiresToken;

        /// <summary>
        /// The credential for the active provider only: the cloud API key is never sent to the
        /// self-hosted server and the self-hosted token is never sent to a cloud provider.
        /// </summary>
        public string ResolveApiKey()
        {
            string value = EffectiveProvider == SpeechProvider.Cloud
                ? ConfigOverrides.GetOverride(PlayerPrefsApiKey) ?? ConfigOverrides.GetEnvironment(EnvApiKey)
                : ConfigOverrides.GetOverride(PlayerPrefsSelfHostedTokenKey) ?? ConfigOverrides.GetEnvironment(EnvSelfHostedTokenKey);
            return value ?? string.Empty;
        }

        /// <summary>The self-hosted base URL after overrides (may be empty).</summary>
        public string ResolveSelfHostedBaseUrl(out string source) =>
            ConfigOverrides.Resolve(PlayerPrefsBaseUrlKey, EnvBaseUrlKey, selfHostedBaseUrl, out source);

        private EndpointCheck Resolve(bool stt, bool isDevelopmentBuild, IEnumerable<string> devAllowlist)
        {
            bool optedIn = EffectiveCloudOptIn;

            if (EffectiveProvider == SpeechProvider.Cloud)
            {
                if (!optedIn)
                    return EndpointCheck.Fail(EndpointStatus.CloudNotOptedIn, "cloud",
                        "Cloud speech is selected but not opted in; audio stays on the device. Enable the cloud opt-in " +
                        "(settings panel or VoiceConfig) or switch back to the self-hosted speech server.");

                string cloudUrl = stt ? cloudSttEndpoint : cloudTtsEndpoint;
                return CleartextPolicy.Evaluate(cloudUrl, isDevelopmentBuild, devAllowlist, "cloud (opted in)");
            }

            string baseUrl = ResolveSelfHostedBaseUrl(out string source);
            var check = CleartextPolicy.Evaluate(baseUrl, isDevelopmentBuild, devAllowlist, source);
            if (check.Status == EndpointStatus.NotConfigured)
                return EndpointCheck.Fail(EndpointStatus.NotConfigured, source,
                    "No self-hosted speech server configured (e.g. the Companion Hub speech services). Set it in the " +
                    "settings panel, PlayerPrefs '" + PlayerPrefsBaseUrlKey + "', env " + EnvBaseUrlKey + " or VoiceConfig.");
            if (!check.IsUsable) return check;

            if (CloudProviderHosts.IsKnownCloudHost(check.Url.Host) && !optedIn)
                return EndpointCheck.Fail(EndpointStatus.CloudNotOptedIn, source,
                    check.Url.Host + " is a third-party cloud provider, not a self-hosted server. Enable the cloud " +
                    "speech opt-in to use it.", check.Url);

            return check.WithPath(stt ? sttPath : ttsPath);
        }

        private string Effective(EndpointCheck check, string what)
        {
            if (check.IsUsable)
            {
                _lastWarning = null;
                return check.Url.ToString();
            }

            string warning = "[VoiceConfig] " + what + " endpoint unavailable: " + check.Message;
            if (warning != _lastWarning)
            {
                _lastWarning = warning;
                Debug.LogWarning(warning);
            }
            return EndpointCheck.UnavailableUrl(ServiceName);
        }
    }
}
