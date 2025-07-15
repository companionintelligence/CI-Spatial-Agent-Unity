using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Core XREAL Vision System - Consolidated architecture for XREAL + Ollama
    /// Based on Quest ImageLLM example but adapted for XREAL devices and Ollama vision models
    /// 
    /// Architecture:
    /// 1. Voice command detected via Speech-to-Text
    /// 2. Camera captures image after brief delay
    /// 3. Image + command sent to Ollama vision model
    /// 4. Response converted to speech via Text-to-Speech
    /// </summary>
    public class VisionSystem : MonoBehaviour, IXREALVisionSystem
    {
        [Header("System Configuration")]
        [SerializeField] private bool autoInitialize = true;
        [SerializeField] private bool enableDebugLogging = true;
        [SerializeField] private float captureDelay = 1.0f;
        
        [Header("Component References")]
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private SpeechToText speechToText;
        [SerializeField] private TextToSpeech textToSpeech;
        [SerializeField] private OllamaConnector ollamaConnector;
        
        [Header("System Events")]
        public UnityEvent<string> OnVoiceCommandReceived { get; private set; } = new UnityEvent<string>();
        public UnityEvent<Texture2D> OnImageCaptured { get; private set; } = new UnityEvent<Texture2D>();
        public UnityEvent<string> OnVisionResponse { get; private set; } = new UnityEvent<string>();
        public UnityEvent<string> OnSystemError { get; private set; } = new UnityEvent<string>();
        public UnityEvent OnSystemReady { get; private set; } = new UnityEvent();
        
        // System State
        private bool isInitialized = false;
        private bool isProcessing = false;
        private SystemState currentState = SystemState.Idle;
        
        public bool IsInitialized => isInitialized;
        public bool IsProcessing => isProcessing;
        public SystemState CurrentState => currentState;
        
        public enum SystemState
        {
            Idle,
            Listening,
            Capturing,
            Processing,
            Speaking,
            Error
        }
        
        private void Start()
        {
            if (autoInitialize)
            {
                StartCoroutine(InitializeSystem());
            }
        }
        
        private IEnumerator InitializeSystem()
        {
            LogDebug("Initializing XREAL Vision System...");
            SetState(SystemState.Idle);
            
            // Auto-find components if not assigned
            if (cameraManager == null)
                cameraManager = GetComponentInChildren<CameraManager>();
            if (speechToText == null)
                speechToText = GetComponentInChildren<SpeechToText>();
            if (textToSpeech == null)
                textToSpeech = GetComponentInChildren<TextToSpeech>();
            if (ollamaConnector == null)
                ollamaConnector = GetComponentInChildren<OllamaConnector>();
            
            // Initialize components in order
            yield return InitializeComponent(cameraManager, "Camera Manager");
            yield return InitializeComponent(speechToText, "Speech to Text");
            yield return InitializeComponent(textToSpeech, "Text to Speech");
            yield return InitializeComponent(ollamaConnector, "Ollama Connector");
            
            // Setup event subscriptions
            SetupEventSubscriptions();
            
            isInitialized = true;
            SetState(SystemState.Idle);
            LogDebug("XREAL Vision System initialized successfully!");
            OnSystemReady?.Invoke();
        }
        
        private IEnumerator InitializeComponent<T>(T component, string componentName) where T : MonoBehaviour
        {
            if (component == null)
            {
                LogError($"{componentName} component not found!");
                yield break;
            }
            
            LogDebug($"Initializing {componentName}...");
            
            // Initialize component if it has an Initialize method
            var initMethod = component.GetType().GetMethod("Initialize");
            if (initMethod != null)
            {
                initMethod.Invoke(component, null);
            }
            
            // Wait for initialization if component has IsInitialized property
            var isInitializedProperty = component.GetType().GetProperty("IsInitialized");
            if (isInitializedProperty != null)
            {
                while (!(bool)isInitializedProperty.GetValue(component))
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }
            
            LogDebug($"{componentName} initialized successfully");
        }
        
        private void SetupEventSubscriptions()
        {
            // Speech to Text Events
            if (speechToText != null)
            {
                speechToText.OnTranscriptionComplete.AddListener(OnVoiceCommandDetected);
                speechToText.OnListeningStarted.AddListener(() => SetState(SystemState.Listening));
                speechToText.OnError.AddListener(OnSystemErrorHandler);
            }
            
            // Camera Events
            if (cameraManager != null)
            {
                cameraManager.OnImageCaptured.AddListener(OnImageCapturedHandler);
                cameraManager.OnCaptureStarted.AddListener(() => SetState(SystemState.Capturing));
                cameraManager.OnError.AddListener(OnSystemErrorHandler);
            }
            
            // Ollama Events
            if (ollamaConnector != null)
            {
                ollamaConnector.OnResponseReceived.AddListener(OnVisionResponseReceived);
                ollamaConnector.OnProcessingStarted.AddListener(() => SetState(SystemState.Processing));
                ollamaConnector.OnError.AddListener(OnSystemErrorHandler);
            }
            
            // Text to Speech Events
            if (textToSpeech != null)
            {
                textToSpeech.OnSpeechStarted.AddListener(() => SetState(SystemState.Speaking));
                textToSpeech.OnSpeechCompleted.AddListener(() => SetState(SystemState.Idle));
                textToSpeech.OnError.AddListener(OnSystemErrorHandler);
            }
        }
        
        #region Event Handlers
        
        private void OnVoiceCommandDetected(string command)
        {
            if (isProcessing)
            {
                LogDebug("System busy, ignoring voice command");
                return;
            }
            
            LogDebug($"Voice command detected: {command}");
            OnVoiceCommandReceived?.Invoke(command);
            
            // Trigger the vision pipeline
            StartCoroutine(ProcessVisionCommand(command));
        }
        
        private void OnImageCapturedHandler(Texture2D image)
        {
            LogDebug("Image captured successfully");
            OnImageCaptured?.Invoke(image);
        }
        
        private void OnVisionResponseReceived(string response)
        {
            LogDebug($"Vision response received: {response}");
            OnVisionResponse?.Invoke(response);
            
            // Speak the response
            if (textToSpeech != null)
            {
                textToSpeech.SpeakText(response);
            }
        }
        
        private void OnSystemErrorHandler(string error)
        {
            LogError($"System error: {error}");
            OnSystemError?.Invoke(error);
            SetState(SystemState.Error);
            
            // Reset to idle after error
            StartCoroutine(ResetAfterError());
        }
        
        private IEnumerator ResetAfterError()
        {
            yield return new WaitForSeconds(2f);
            SetState(SystemState.Idle);
        }
        
        #endregion
        
        #region Core Vision Pipeline
        
        private IEnumerator ProcessVisionCommand(string command)
        {
            if (!isInitialized)
            {
                LogError("System not initialized");
                yield break;
            }
            
            isProcessing = true;
            
            try
            {
                // Wait for capture delay (to avoid capturing hands/controllers)
                yield return new WaitForSeconds(captureDelay);
                
                // Capture image from camera
                if (cameraManager != null)
                {
                    cameraManager.CaptureImage();
                    
                    // Wait for image capture to complete
                    yield return new WaitUntil(() => cameraManager.HasCapturedImage);
                    
                    Texture2D capturedImage = cameraManager.GetLastCapturedImage();
                    if (capturedImage != null)
                    {
                        // Send to Ollama vision model
                        if (ollamaConnector != null)
                        {
                            ollamaConnector.SendVisionRequest(capturedImage, command);
                        }
                    }
                    else
                    {
                        OnSystemErrorHandler("Failed to capture image");
                    }
                }
                else
                {
                    OnSystemErrorHandler("Camera manager not available");
                }
            }
            finally
            {
                isProcessing = false;
            }
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Manually trigger a voice command (for testing or external control)
        /// </summary>
        public void ProcessTextCommand(string command)
        {
            if (!isInitialized)
            {
                LogError("System not initialized");
                return;
            }
            
            LogDebug($"Processing manual text command: {command}");
            StartCoroutine(ProcessVisionCommand(command));
        }
        
        /// <summary>
        /// Start listening for voice commands
        /// </summary>
        public void StartListening()
        {
            if (speechToText != null)
            {
                speechToText.StartListening();
            }
        }
        
        /// <summary>
        /// Stop listening for voice commands
        /// </summary>
        public void StopListening()
        {
            if (speechToText != null)
            {
                speechToText.StopListening();
            }
        }
        
        /// <summary>
        /// Toggle voice listening
        /// </summary>
        public void ToggleListening()
        {
            if (speechToText != null)
            {
                if (currentState == SystemState.Listening)
                {
                    StopListening();
                }
                else
                {
                    StartListening();
                }
            }
        }
        
        #endregion
        
        #region Utility Methods
        
        private void SetState(SystemState newState)
        {
            if (currentState != newState)
            {
                LogDebug($"State changed: {currentState} -> {newState}");
                currentState = newState;
            }
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[XREALVisionSystem] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[XREALVisionSystem] {message}");
        }
        
        #endregion
        
        private void OnDestroy()
        {
            // Clean up event subscriptions
            if (speechToText != null)
            {
                speechToText.OnTranscriptionComplete.RemoveListener(OnVoiceCommandDetected);
                speechToText.OnError.RemoveListener(OnSystemErrorHandler);
            }
            
            if (cameraManager != null)
            {
                cameraManager.OnImageCaptured.RemoveListener(OnImageCapturedHandler);
                cameraManager.OnError.RemoveListener(OnSystemErrorHandler);
            }
            
            if (ollamaConnector != null)
            {
                ollamaConnector.OnResponseReceived.RemoveListener(OnVisionResponseReceived);
                ollamaConnector.OnError.RemoveListener(OnSystemErrorHandler);
            }
            
            if (textToSpeech != null)
            {
                textToSpeech.OnSpeechCompleted.RemoveListener(() => SetState(SystemState.Idle));
                textToSpeech.OnError.RemoveListener(OnSystemErrorHandler);
            }
        }
    }
}
