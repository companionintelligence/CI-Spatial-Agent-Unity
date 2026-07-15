using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using XRScan.Core;

namespace XRScan.Voice
{
    /// <summary>
    /// Server-backed text-to-speech: POSTs text to an OpenAI-compatible speech endpoint (or the
    /// Companion Intelligence server), then plays the returned audio through its AudioSource — the
    /// same source the viseme controller reads for lip-sync. Ported from CI-Spatial's TextToSpeech;
    /// endpoint / voice / key come from VoiceConfig. Hybrid default; PiperTts is the on-device swap.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CloudTts : MonoBehaviour, ITextToSpeech
    {
        [SerializeField] private VoiceConfig config;

        private AudioSource _source;
        private bool _initialized;
        private bool _playing;

        public UnityEvent OnSpeechStarted { get; } = new UnityEvent();
        public UnityEvent OnSpeechCompleted { get; } = new UnityEvent();
        public UnityEvent<string> OnError { get; } = new StringUnityEvent();

        public bool IsInitialized => _initialized;
        public bool IsPlaying => _playing || (_source != null && _source.isPlaying);
        public AudioSource OutputSource => _source;

        public void Configure(VoiceConfig cfg) => config = cfg;

        public void Initialize()
        {
            if (_initialized) return;
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _initialized = true;
        }

        public void SpeakText(string text)
        {
            if (!_initialized) Initialize();
            if (string.IsNullOrWhiteSpace(text) || config == null) return;
            StartCoroutine(Generate(text));
        }

        public void StopSpeaking()
        {
            if (_source != null) _source.Stop();
            _playing = false;
        }

        public void SetVoiceSettings(string voice, float speed, float pitch)
        {
            if (!string.IsNullOrEmpty(voice) && config != null) config.ttsVoice = voice;
            if (_source != null) _source.pitch = Mathf.Clamp(pitch <= 0f ? 1f : pitch, 0.5f, 2f);
        }

        [Serializable]
        private class TtsPayload { public string model; public string input; public string voice; public string response_format; public float speed = 1f; }

        private IEnumerator Generate(string text)
        {
            string key = config.ResolveApiKey();
            if (config.requiresApiKey && string.IsNullOrEmpty(key)) { OnError.Invoke("Voice API key not set"); yield break; }

            var payload = new TtsPayload
            {
                model = config.ttsModel,
                input = text,
                voice = config.ttsVoice,
                response_format = config.ttsFormat
            };
            string json = JsonUtility.ToJson(payload);

            using var web = new UnityWebRequest(config.ttsEndpoint, "POST");
            web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            web.downloadHandler = new DownloadHandlerBuffer();
            web.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(key)) web.SetRequestHeader("Authorization", "Bearer " + key);
            web.timeout = Mathf.Max(1, (int)config.requestTimeoutSeconds);

            yield return web.SendWebRequest();
            if (web.result != UnityWebRequest.Result.Success) { OnError.Invoke("TTS failed: " + web.error); yield break; }

            yield return StartCoroutine(PlayBytes(web.downloadHandler.data, config.ttsFormat));
        }

        private IEnumerator PlayBytes(byte[] audio, string format)
        {
            string ext = string.Equals(format, "wav", StringComparison.OrdinalIgnoreCase) ? "wav" : "mp3";
            var type = ext == "wav" ? AudioType.WAV : AudioType.MPEG;
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "xrscan_tts." + ext);
            System.IO.File.WriteAllBytes(path, audio);

            using var www = UnityWebRequestMultimedia.GetAudioClip("file://" + path, type);
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                var clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null)
                {
                    _source.clip = clip;
                    _playing = true;
                    OnSpeechStarted.Invoke();
                    _source.Play();
                    yield return new WaitForSeconds(clip.length);
                    _playing = false;
                    OnSpeechCompleted.Invoke();
                }
            }
            else
            {
                OnError.Invoke("TTS audio load failed: " + www.error);
            }
        }
    }
}
