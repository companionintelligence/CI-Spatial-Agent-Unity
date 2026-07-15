using System;
using UnityEngine;

namespace XRScan.Core
{
    /// <summary>
    /// Endpoints + settings for server-backed speech (STT/TTS). Defaults target the
    /// OpenAI-compatible audio API, but any server (incl. the Companion Intelligence server) can
    /// be pointed at by changing the endpoints. The API key is NEVER serialized: it resolves from
    /// a PlayerPrefs override, then the XRSCAN_VOICE_API_KEY env var.
    /// </summary>
    [CreateAssetMenu(fileName = "VoiceConfig", menuName = "XRScan/Voice Config")]
    public class VoiceConfig : ScriptableObject
    {
        public const string PlayerPrefsApiKey = "XRScan.VoiceApiKey";
        public const string EnvApiKey = "XRSCAN_VOICE_API_KEY";

        [Header("Speech-to-text (server)")]
        public string sttEndpoint = "https://api.openai.com/v1/audio/transcriptions";
        public string sttModel = "whisper-1";

        [Header("Text-to-speech (server)")]
        public string ttsEndpoint = "https://api.openai.com/v1/audio/speech";
        public string ttsModel = "tts-1";
        public string ttsVoice = "alloy";
        [Tooltip("Response audio format: mp3 or wav.")]
        public string ttsFormat = "mp3";

        [Header("Recording")]
        public int sampleRate = 16000;
        public int maxRecordingLength = 10;
        public float voiceThreshold = 0.01f;
        public float requestTimeoutSeconds = 30f;

        /// <summary>True when the endpoints require a bearer key (OpenAI-style). The CI server may not.</summary>
        public bool requiresApiKey = true;

        public string ResolveApiKey()
        {
            var overrideKey = PlayerPrefs.GetString(PlayerPrefsApiKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(overrideKey)) return overrideKey;

            var envKey = Environment.GetEnvironmentVariable(EnvApiKey);
            return envKey ?? string.Empty;
        }
    }
}
