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

        // Approximate first-formant frequencies of the vowels /a i o e u/. Cycling the spectral peak
        // through these at a syllable rate gives uLipSync a *changing* vowel to detect, so the mouth
        // moves through varied shapes — a single fixed tone locks one viseme and looks robotic.
        private static readonly float[] FormantHz = { 730f, 270f, 570f, 530f, 300f };
        private const float SyllableRate = 5f; // vowels per second

        private AudioClip GenerateTone(float duration)
        {
            const int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            var clip = AudioClip.Create("mock_tts", samples, 1, sampleRate, false);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float formant = FormantHz[(int)(t * SyllableRate) % FormantHz.Length];
                float env = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * SyllableRate * t); // syllable envelope
                // Voice-ish timbre: a fundamental pitch plus the moving formant and one harmonic, so the
                // spectral shape changes per syllable and uLipSync reads a sequence of vowels.
                float voice = 0.6f * Mathf.Sin(2f * Mathf.PI * frequency * t)
                              + Mathf.Sin(2f * Mathf.PI * formant * t)
                              + 0.3f * Mathf.Sin(2f * Mathf.PI * 2f * formant * t);
                data[i] = 0.18f * env * voice;
            }
            clip.SetData(data, 0);
            return clip;
        }
    }
}
