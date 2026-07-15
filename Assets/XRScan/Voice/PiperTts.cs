using UnityEngine;
using UnityEngine.Events;
using XRScan.Core;

namespace XRScan.Voice
{
    /// <summary>
    /// On-device Piper TTS (Sentis) — SCAFFOLD. Assign a Piper model to enable offline speech;
    /// until then it reports unavailable so the hybrid orchestrator falls back to
    /// <see cref="CloudTts"/>. Full pipeline lands in Phase 4 (needs the model asset).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PiperTts : MonoBehaviour, ITextToSpeech
    {
        [SerializeField] private UnityEngine.Object modelAsset;
        private AudioSource _source;

        public UnityEvent OnSpeechStarted { get; } = new UnityEvent();
        public UnityEvent OnSpeechCompleted { get; } = new UnityEvent();
        public UnityEvent<string> OnError { get; } = new StringUnityEvent();

        public bool IsInitialized { get; private set; }
        public bool IsPlaying => _source != null && _source.isPlaying;
        public AudioSource OutputSource => _source;
        public bool IsModelAssigned => modelAsset != null;

        public void Initialize()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            IsInitialized = true;
            if (!IsModelAssigned) Debug.LogWarning("[PiperTts] No model assigned; use server TTS.");
        }

        public void SpeakText(string text)
        {
            if (!IsModelAssigned) OnError.Invoke("On-device TTS model not assigned");
        }

        public void StopSpeaking() { if (_source != null) _source.Stop(); }
        public void SetVoiceSettings(string voice, float speed, float pitch) { }
    }
}
