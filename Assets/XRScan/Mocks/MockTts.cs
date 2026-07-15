using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using XRScan.Core;

namespace XRScan.Mocks
{
    /// <summary>Stand-in TTS that plays a short, amplitude-modulated tone through its AudioSource
    /// so the avatar's lip-sync has audio to react to. Swap for on-device Piper / cloud TTS later.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class MockTts : MonoBehaviour, ITextToSpeech
    {
        [System.Serializable] private class StringEvent : UnityEvent<string> { }

        [SerializeField] private float secondsPerChar = 0.06f;
        [SerializeField] private float frequency = 220f;

        private AudioSource _source;

        public bool IsInitialized { get; private set; }
        public bool IsPlaying => _source != null && _source.isPlaying;
        public AudioSource OutputSource => _source;

        public UnityEvent OnSpeechStarted { get; } = new UnityEvent();
        public UnityEvent OnSpeechCompleted { get; } = new UnityEvent();
        public UnityEvent<string> OnError { get; } = new StringEvent();

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (IsInitialized) return;
            _source = GetComponent<AudioSource>();
            IsInitialized = true;
        }

        public void SpeakText(string text)
        {
            if (string.IsNullOrEmpty(text) || _source == null) return;

            float duration = Mathf.Clamp(text.Length * secondsPerChar, 0.4f, 4f);
            _source.clip = GenerateTone(duration);
            _source.Play();
            OnSpeechStarted.Invoke();
            StartCoroutine(CompleteAfter(duration));
            Debug.Log($"[MockTts] speak: \"{text}\" ({duration:0.0}s)");
        }

        public void StopSpeaking() { if (_source != null) _source.Stop(); }

        public void SetVoiceSettings(string voice, float speed, float pitch)
        {
            if (_source != null) _source.pitch = Mathf.Clamp(pitch <= 0f ? 1f : pitch, 0.5f, 2f);
        }

        private IEnumerator CompleteAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            OnSpeechCompleted.Invoke();
        }

        private AudioClip GenerateTone(float duration)
        {
            const int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            var clip = AudioClip.Create("mock_tts", samples, 1, sampleRate, false);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float env = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 4f * t); // syllable-rate envelope
                data[i] = 0.25f * env * Mathf.Sin(2f * Mathf.PI * frequency * t);
            }
            clip.SetData(data, 0);
            return clip;
        }
    }
}
