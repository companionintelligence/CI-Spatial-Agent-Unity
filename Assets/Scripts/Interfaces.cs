using UnityEngine;
using UnityEngine.Events;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Interface for the main XREAL Vision System
    /// Enables better testing and dependency injection
    /// </summary>
    public interface IXREALVisionSystem
    {
        bool IsInitialized { get; }
        bool IsProcessing { get; }
        XREALVisionSystem.SystemState CurrentState { get; }
        
        UnityEvent<string> OnVoiceCommandReceived { get; }
        UnityEvent<Texture2D> OnImageCaptured { get; }
        UnityEvent<string> OnVisionResponse { get; }
        UnityEvent<string> OnSystemError { get; }
        UnityEvent OnSystemReady { get; }
        
        void ProcessTextCommand(string command);
        void StartListening();
        void StopListening();
        void ToggleListening();
    }

    /// <summary>
    /// Interface for camera management
    /// </summary>
    public interface IXREALCameraManager
    {
        bool IsInitialized { get; }
        bool HasCapturedImage { get; }
        
        UnityEvent OnCaptureStarted { get; }
        UnityEvent<Texture2D> OnImageCaptured { get; }
        UnityEvent<string> OnError { get; }
        
        void Initialize();
        void CaptureImage();
        Texture2D GetLastCapturedImage();
        Texture2D GetLastCapturedImageCopy();
        void ClearCapturedImage();
        void SetCaptureResolution(int width, int height);
    }

    /// <summary>
    /// Interface for Ollama connector
    /// </summary>
    public interface IXREALOllamaConnector
    {
        bool IsInitialized { get; }
        bool IsConnected { get; }
        bool IsProcessing { get; }
        string ServerAddress { get; }
        
        UnityEvent OnProcessingStarted { get; }
        UnityEvent<string> OnResponseReceived { get; }
        UnityEvent<string> OnError { get; }
        
        void Initialize();
        void SendVisionRequest(Texture2D image, string prompt);
        void SendTextRequest(string prompt);
    }

    /// <summary>
    /// Interface for speech-to-text functionality
    /// </summary>
    public interface IXREALSpeechToText
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

    /// <summary>
    /// Interface for text-to-speech functionality
    /// </summary>
    public interface IXREALTextToSpeech
    {
        bool IsInitialized { get; }
        bool IsPlaying { get; }
        
        UnityEvent OnSpeechStarted { get; }
        UnityEvent OnSpeechCompleted { get; }
        UnityEvent<string> OnError { get; }
        
        void Initialize();
        void SpeakText(string text);
        void StopSpeaking();
        void SetVoiceSettings(string voice, float speed, float pitch);
    }
}
