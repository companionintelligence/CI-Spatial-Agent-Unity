using UnityEngine;
using UnityEngine.Events;
using XRScan.Core;

namespace XRScan.Tests.Config
{
    /// <summary>Records calls instead of touching the microphone.</summary>
    public class FakeSpeechToText : MonoBehaviour, ISpeechToText
    {
        public int InitializeCalls;
        public int StartCalls;

        public bool IsInitialized { get; private set; }
        public bool IsListening { get; private set; }
        public bool IsProcessing => false;

        public UnityEvent OnListeningStarted { get; } = new UnityEvent();
        public UnityEvent OnListeningStopped { get; } = new UnityEvent();
        public UnityEvent<string> OnTranscriptionComplete { get; } = new StringUnityEvent();
        public UnityEvent<string> OnError { get; } = new StringUnityEvent();

        public void Initialize()
        {
            InitializeCalls++;
            IsInitialized = true;
        }

        public void StartListening()
        {
            StartCalls++;
            IsListening = true;
        }

        public void StopListening() => IsListening = false;

        public void ToggleListening()
        {
            if (IsListening) StopListening();
            else StartListening();
        }
    }
}
