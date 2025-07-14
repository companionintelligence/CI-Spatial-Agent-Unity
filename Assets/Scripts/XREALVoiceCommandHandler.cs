using System.Collections;
using UnityEngine;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// XREAL Voice Command Handler - Manages the voice interaction flow
    /// Based on Quest ImageLLM VoiceCommandHandler but adapted for XREAL + Ollama
    /// </summary>
    public class XREALVoiceCommandHandler : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private XREALVisionSystem visionSystem;
        [SerializeField] private XREALCameraManager cameraManager;
        [SerializeField] private XREALOllamaConnector ollamaConnector;
        [SerializeField] private XREALSpeechToText speechToText;
        
        [Header("Interaction Settings")]
        [SerializeField] private bool autoStartListening = true;
        [SerializeField] private float captureDelay = 1.0f; // Delay to avoid capturing hands/controllers
        [SerializeField] private bool enableContinuousMode = false;
        [SerializeField] private bool enableDebugLogging = true;
        
        [Header("Testing (Editor Only)")]
        [SerializeField] private Texture2D dummyImage; // For testing in editor
        [SerializeField] private string testCommand = "What do you see?";
        
        // State
        private bool isInitialized = false;
        private bool isProcessingCommand = false;
        private Texture2D lastCapturedImage;
        
        public bool IsInitialized => isInitialized;
        public bool IsProcessingCommand => isProcessingCommand;
        
        private void Start()
        {
            StartCoroutine(Initialize());
        }
        
        private IEnumerator Initialize()
        {
            LogDebug("Initializing Voice Command Handler...");
            
            // Auto-find components if not assigned
            if (visionSystem == null)
                visionSystem = FindObjectOfType<XREALVisionSystem>();
            if (cameraManager == null)
                cameraManager = FindObjectOfType<XREALCameraManager>();
            if (ollamaConnector == null)
                ollamaConnector = FindObjectOfType<XREALOllamaConnector>();
            if (speechToText == null)
                speechToText = FindObjectOfType<XREALSpeechToText>();
            
            // Wait for components to initialize
            yield return new WaitUntil(() => 
                (visionSystem == null || visionSystem.IsInitialized) &&
                (cameraManager == null || cameraManager.IsInitialized) &&
                (ollamaConnector == null || ollamaConnector.IsInitialized) &&
                (speechToText == null || speechToText.IsInitialized)
            );
            
            // Subscribe to speech-to-text events
            if (speechToText != null)
            {
                speechToText.OnTranscriptionComplete.AddListener(OnTranscriptionReceived);
                LogDebug("Subscribed to speech-to-text events");
            }
            
            isInitialized = true;
            LogDebug("Voice Command Handler initialized successfully");
            
            // Start listening if enabled
            if (autoStartListening && speechToText != null)
            {
                speechToText.StartListening();
                LogDebug("Auto-started voice listening");
            }
        }
        
        /// <summary>
        /// Called when speech transcription is complete
        /// </summary>
        private void OnTranscriptionReceived(string transcription)
        {
            if (string.IsNullOrWhiteSpace(transcription))
            {
                LogDebug("Empty transcription received, ignoring");
                return;
            }
            
            if (isProcessingCommand)
            {
                LogDebug("Already processing a command, ignoring new transcription");
                return;
            }
            
            LogDebug($"Voice command received: \"{transcription}\"");
            StartCoroutine(ProcessVoiceCommand(transcription));
        }
        
        /// <summary>
        /// Process a voice command by capturing image and sending to Ollama
        /// </summary>
        private IEnumerator ProcessVoiceCommand(string command)
        {
            isProcessingCommand = true;
            
            try
            {
                LogDebug($"Processing voice command: \"{command}\"");
                
                // Wait for capture delay to avoid capturing hands/controllers
                yield return new WaitForSeconds(captureDelay);
                
                // Capture image
                yield return StartCoroutine(CaptureImageCoroutine());
                Texture2D capturedImage = lastCapturedImage;
                
                if (capturedImage == null)
                {
                    LogError("Failed to capture image");
                    yield break;
                }
                
                // Send to Ollama vision model
                if (ollamaConnector != null)
                {
                    ollamaConnector.SendVisionRequest(capturedImage, command);
                }
                else
                {
                    LogError("Ollama connector not available");
                }
            }
            finally
            {
                isProcessingCommand = false;
                
                // Resume listening in continuous mode
                if (enableContinuousMode && speechToText != null && !speechToText.IsListening)
                {
                    yield return new WaitForSeconds(1.0f); // Brief pause before resuming
                    speechToText.StartListening();
                }
            }
        }
        
        /// <summary>
        /// Capture image from camera or use dummy image in editor
        /// </summary>
        private IEnumerator CaptureImageCoroutine()
        {
            lastCapturedImage = null;
            
            // In editor or if no camera available, use dummy image
            if (Application.isEditor || cameraManager == null)
            {
                if (dummyImage != null)
                {
                    LogDebug("Using dummy image for testing");
                    lastCapturedImage = dummyImage;
                }
                else
                {
                    LogDebug("Creating gray placeholder image");
                    lastCapturedImage = CreatePlaceholderImage();
                }
            }
            else
            {
                // Capture from real camera
                LogDebug("Capturing image from camera...");
                
                cameraManager.CaptureImage();
                
                // Wait for capture to complete
                yield return new WaitUntil(() => cameraManager.HasCapturedImage);
                
                lastCapturedImage = cameraManager.GetLastCapturedImageCopy();
                cameraManager.ClearCapturedImage();
            }
            
            if (lastCapturedImage != null)
            {
                LogDebug($"Image captured: {lastCapturedImage.width}x{lastCapturedImage.height}");
            }
        }
        
        /// <summary>
        /// Create a placeholder image for testing
        /// </summary>
        private Texture2D CreatePlaceholderImage()
        {
            Texture2D texture = new Texture2D(512, 512, TextureFormat.RGB24, false);
            Color[] pixels = new Color[512 * 512];
            
            // Create a simple gradient pattern
            for (int y = 0; y < 512; y++)
            {
                for (int x = 0; x < 512; x++)
                {
                    float r = (float)x / 512f;
                    float g = (float)y / 512f;
                    float b = 0.5f;
                    pixels[y * 512 + x] = new Color(r, g, b);
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
        
        /// <summary>
        /// Manually process a text command (for testing or UI)
        /// </summary>
        public void ProcessTextCommand(string command)
        {
            if (!isInitialized)
            {
                LogError("Voice Command Handler not initialized");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(command))
            {
                LogError("Empty command provided");
                return;
            }
            
            LogDebug($"Processing manual text command: \"{command}\"");
            StartCoroutine(ProcessVoiceCommand(command));
        }
        
        /// <summary>
        /// Start listening for voice commands
        /// </summary>
        public void StartListening()
        {
            if (speechToText != null)
            {
                speechToText.StartListening();
                LogDebug("Started voice listening");
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
                LogDebug("Stopped voice listening");
            }
        }
        
        /// <summary>
        /// Toggle voice listening
        /// </summary>
        public void ToggleListening()
        {
            if (speechToText != null)
            {
                if (speechToText.IsListening)
                {
                    StopListening();
                }
                else
                {
                    StartListening();
                }
            }
        }
        
        /// <summary>
        /// Enable or disable continuous mode
        /// </summary>
        public void SetContinuousMode(bool enabled)
        {
            enableContinuousMode = enabled;
            LogDebug($"Continuous mode {(enabled ? "enabled" : "disabled")}");
            
            if (enabled && !speechToText.IsListening)
            {
                StartListening();
            }
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[XREALVoiceCommandHandler] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[XREALVoiceCommandHandler] {message}");
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from events
            if (speechToText != null)
            {
                speechToText.OnTranscriptionComplete.RemoveListener(OnTranscriptionReceived);
            }
        }
        
        #region Editor Testing
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void Update()
        {
            // Editor testing shortcuts
            if (!Application.isPlaying) return;
            
            // Test voice command with 'T' key
            if (Input.GetKeyDown(KeyCode.T))
            {
                ProcessTextCommand(testCommand);
            }
            
            // Toggle listening with 'L' key
            if (Input.GetKeyDown(KeyCode.L))
            {
                ToggleListening();
            }
            
            // Test continuous mode with 'C' key
            if (Input.GetKeyDown(KeyCode.C))
            {
                SetContinuousMode(!enableContinuousMode);
            }
        }
        
        #endregion
    }
}
