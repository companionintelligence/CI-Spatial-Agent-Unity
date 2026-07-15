using UnityEngine;
using UnityEngine.Events;
using XRScan.Core;

namespace XRScan.Voice
{
    /// <summary>
    /// On-device Whisper STT via Unity Inference Engine (Sentis) — SCAFFOLD. Assign a Whisper
    /// .onnx model to enable fully-offline transcription; until then it reports unavailable so the
    /// hybrid orchestrator falls back to <see cref="CloudWhisperStt"/>. The full mic-&gt;mel-&gt;tensor
    /// pipeline lands in Phase 4 (needs the model asset, which is not committed).
    /// </summary>
    public class SentisWhisperStt : MonoBehaviour, ISpeechToText
    {
        [Tooltip("Assign a Whisper .onnx model (Unity Inference Engine) to enable on-device STT.")]
        [SerializeField] private UnityEngine.Object modelAsset;

        public UnityEvent OnListeningStarted { get; } = new UnityEvent();
        public UnityEvent OnListeningStopped { get; } = new UnityEvent();
        public UnityEvent<string> OnTranscriptionComplete { get; } = new StringUnityEvent();
        public UnityEvent<string> OnError { get; } = new StringUnityEvent();

        public bool IsInitialized { get; private set; }
        public bool IsListening { get; private set; }
        public bool IsProcessing => false;
        public bool IsModelAssigned => modelAsset != null;

        public void Initialize()
        {
            IsInitialized = IsModelAssigned;
            if (!IsModelAssigned) Debug.LogWarning("[SentisWhisperStt] No model assigned; use server STT.");
        }

        public void StartListening()
        {
            if (!IsModelAssigned) { OnError.Invoke("On-device STT model not assigned"); return; }
            IsListening = true;
            OnListeningStarted.Invoke();
        }

        public void StopListening()
        {
            IsListening = false;
            OnListeningStopped.Invoke();
        }

        public void ToggleListening()
        {
            if (IsListening) StopListening(); else StartListening();
        }
    }
}
