using UnityEngine.Events;

namespace XRScan.Core
{
    /// <summary>
    /// Push-to-talk transcription seam. Default impl is on-device (Sentis Whisper);
    /// a cloud Whisper impl and a mock are also provided.
    /// Generalized from CI-Spatial's IXREALSpeechToText.
    /// </summary>
    public interface ISpeechToText
    {
        bool IsInitialized { get; }
        bool IsListening { get; }
        bool IsProcessing { get; }

        UnityEvent OnListeningStarted { get; }
        UnityEvent OnListeningStopped { get; }
        UnityEvent<string> OnTranscriptionComplete { get; }
        UnityEvent<string> OnError { get; }

        void Initialize();
        void StartListening();
        void StopListening();
        void ToggleListening();
    }
}
