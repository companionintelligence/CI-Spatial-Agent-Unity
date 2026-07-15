using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using XRScan.Core;

namespace XRScan.Voice
{
    /// <summary>
    /// Server-backed speech-to-text: records the mic, encodes WAV, and POSTs it as multipart to a
    /// Whisper-compatible endpoint (OpenAI or the Companion Intelligence server). Ported from
    /// CI-Spatial's SpeechToText; endpoint / key come from VoiceConfig (no hardcoded secrets).
    /// This is the hybrid default; SentisWhisperStt is the on-device swap.
    /// </summary>
    public class CloudWhisperStt : MonoBehaviour, ISpeechToText
    {
        [SerializeField] private VoiceConfig config;

        public UnityEvent OnListeningStarted { get; } = new UnityEvent();
        public UnityEvent OnListeningStopped { get; } = new UnityEvent();
        public UnityEvent<string> OnTranscriptionComplete { get; } = new StringUnityEvent();
        public UnityEvent<string> OnError { get; } = new StringUnityEvent();

        private bool _initialized;
        private bool _listening;
        private bool _processing;
        private AudioClip _clip;
        private string _mic;

        public bool IsInitialized => _initialized;
        public bool IsListening => _listening;
        public bool IsProcessing => _processing;

        public void Configure(VoiceConfig cfg) => config = cfg;

        public void Initialize()
        {
            if (_initialized) return;
            if (config == null) { OnError.Invoke("VoiceConfig not assigned"); return; }
            if (Microphone.devices.Length == 0) { OnError.Invoke("No microphone devices found"); return; }
            _mic = Microphone.devices[0];
            _initialized = true;
        }

        public void StartListening()
        {
            if (!_initialized) Initialize();
            if (!_initialized || _listening || string.IsNullOrEmpty(_mic)) return;

            _clip = Microphone.Start(_mic, false, config.maxRecordingLength, config.sampleRate);
            if (_clip == null) { OnError.Invoke("Failed to start recording"); return; }

            _listening = true;
            OnListeningStarted.Invoke();
        }

        public void StopListening()
        {
            if (!_listening) return;
            _listening = false;
            Microphone.End(_mic);
            OnListeningStopped.Invoke();
            if (_clip != null) StartCoroutine(ProcessRecordedAudio());
        }

        public void ToggleListening()
        {
            if (_listening) StopListening();
            else StartListening();
        }

        private IEnumerator ProcessRecordedAudio()
        {
            _processing = true;
            byte[] wav = null;
            try { wav = EncodeWav(_clip); }
            catch (Exception e) { OnError.Invoke("WAV encode failed: " + e.Message); }

            if (wav == null || wav.Length == 0) { _processing = false; yield break; }
            yield return StartCoroutine(SendForTranscription(wav));

            _processing = false;
            if (_clip != null) { Destroy(_clip); _clip = null; }
        }

        private IEnumerator SendForTranscription(byte[] wav)
        {
            string key = config.ResolveApiKey();
            if (config.requiresApiKey && string.IsNullOrEmpty(key))
            {
                OnError.Invoke("Voice API key not set (PlayerPrefs '" + VoiceConfig.PlayerPrefsApiKey + "' or env '" + VoiceConfig.EnvApiKey + "')");
                yield break;
            }

            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", wav, "audio.wav", "audio/wav"),
                new MultipartFormDataSection("model", config.sttModel)
            };

            using var req = UnityWebRequest.Post(config.sttEndpoint, form);
            if (!string.IsNullOrEmpty(key)) req.SetRequestHeader("Authorization", "Bearer " + key);
            req.timeout = Mathf.Max(1, (int)config.requestTimeoutSeconds);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<TranscriptResponse>(req.downloadHandler.text);
                if (resp != null && !string.IsNullOrEmpty(resp.text)) OnTranscriptionComplete.Invoke(resp.text);
                else OnError.Invoke("Empty transcription");
            }
            else
            {
                OnError.Invoke("Transcription failed: " + req.error);
            }
        }

        [Serializable] private class TranscriptResponse { public string text; }

        private static byte[] EncodeWav(AudioClip clip)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            using var stream = new MemoryStream();
            int sampleCount = samples.Length;
            int byteRate = clip.frequency * clip.channels * 2;

            void WriteStr(string s) => stream.Write(System.Text.Encoding.UTF8.GetBytes(s), 0, s.Length);

            WriteStr("RIFF");
            stream.Write(BitConverter.GetBytes(36 + sampleCount * 2), 0, 4);
            WriteStr("WAVE");
            WriteStr("fmt ");
            stream.Write(BitConverter.GetBytes(16), 0, 4);
            stream.Write(BitConverter.GetBytes((ushort)1), 0, 2);
            stream.Write(BitConverter.GetBytes((ushort)clip.channels), 0, 2);
            stream.Write(BitConverter.GetBytes(clip.frequency), 0, 4);
            stream.Write(BitConverter.GetBytes(byteRate), 0, 4);
            stream.Write(BitConverter.GetBytes((ushort)(clip.channels * 2)), 0, 2);
            stream.Write(BitConverter.GetBytes((ushort)16), 0, 2);
            WriteStr("data");
            stream.Write(BitConverter.GetBytes(sampleCount * 2), 0, 4);

            foreach (var s in samples)
            {
                short v = (short)(Mathf.Clamp(s, -1f, 1f) * 32767f);
                stream.Write(BitConverter.GetBytes(v), 0, 2);
            }
            return stream.ToArray();
        }
    }
}
