using UnityEngine;
using CompanionIntelligence.Core;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Example script demonstrating the improved XREAL Vision System usage
    /// Shows best practices for integration and common use cases
    /// </summary>
    public class XREALVisionSystemExample : MonoBehaviour
    {
        [Header("System Configuration")]
        [SerializeField] private bool enableDebugMode = true;
        [SerializeField] private bool autoStartListening = true;
        
        [Header("Performance Monitoring")]
        [SerializeField] private bool showPerformanceStats = true;
        [SerializeField] private float statsUpdateInterval = 1f;
        
        private VisionSystem visionSystem;
        private float lastStatsUpdate;
        private int commandsProcessed = 0;
        private float totalProcessingTime = 0f;
        
        private void Start()
        {
            InitializeVisionSystem();
        }
        
        private void InitializeVisionSystem()
        {
            // Find or create the vision system
            visionSystem = FindFirstObjectByType<VisionSystem>();
            
            if (visionSystem == null)
            {
                Debug.LogWarning("[XREALVisionExample] No XREALVisionSystem found. Please use XREALVisionSceneSetup to create one.");
                return;
            }
            
            // Subscribe to system events
            SetupEventHandlers();
            
            // Configure system based on preferences
            ConfigureSystem();
            
            if (enableDebugMode)
            {
                Debug.Log("[XREALVisionExample] Vision system initialized successfully");
            }
        }
        
        private void SetupEventHandlers()
        {
            // System lifecycle events
            visionSystem.OnSystemReady.AddListener(OnSystemReady);
            visionSystem.OnSystemError.AddListener(OnSystemError);
            
            // Processing pipeline events
            visionSystem.OnVoiceCommandReceived.AddListener(OnVoiceCommandReceived);
            visionSystem.OnImageCaptured.AddListener(OnImageCaptured);
            visionSystem.OnVisionResponse.AddListener(OnVisionResponse);
        }
        
        private void ConfigureSystem()
        {
            if (autoStartListening && visionSystem.IsInitialized)
            {
                visionSystem.StartListening();
            }
        }
        
        #region Event Handlers
        
        private void OnSystemReady()
        {
            Debug.Log("[XREALVisionExample] System is ready for use!");
            
            if (autoStartListening)
            {
                visionSystem.StartListening();
            }
            
            // Example: Send a test command
            if (enableDebugMode)
            {
                visionSystem.ProcessTextCommand("What do you see?");
            }
        }
        
        private void OnSystemError(string error)
        {
            Debug.LogError($"[XREALVisionExample] System error: {error}");
            
            // Example error recovery - restart listening after a delay
            Invoke(nameof(RecoverFromError), 3f);
        }
        
        private void OnVoiceCommandReceived(string command)
        {
            if (enableDebugMode)
            {
                Debug.Log($"[XREALVisionExample] Voice command: {command}");
            }
            
            commandsProcessed++;
        }
        
        private void OnImageCaptured(Texture2D image)
        {
            if (enableDebugMode)
            {
                Debug.Log($"[XREALVisionExample] Image captured: {image.width}x{image.height}");
            }
            
            // Example: Apply image filters or preprocessing here
            ProcessCapturedImage(image);
        }
        
        private void OnVisionResponse(string response)
        {
            if (enableDebugMode)
            {
                Debug.Log($"[XREALVisionExample] Vision response: {response}");
            }
            
            // Example: Process the response for specific actions
            ProcessVisionResponse(response);
        }
        
        #endregion
        
        #region Processing Methods
        
        private void ProcessCapturedImage(Texture2D image)
        {
            // Example: Validate image quality
            if (image.width < 256 || image.height < 256)
            {
                Debug.LogWarning("[XREALVisionExample] Captured image resolution is low");
            }
            
            // Example: Save image for debugging
            if (enableDebugMode)
            {
                SaveDebugImage(image);
            }
        }
        
        private void ProcessVisionResponse(string response)
        {
            // Example: Parse response for specific keywords
            if (response.ToLower().Contains("person"))
            {
                Debug.Log("[XREALVisionExample] Person detected in scene");
            }
            
            if (response.ToLower().Contains("text"))
            {
                Debug.Log("[XREALVisionExample] Text detected in scene");
            }
            
            // Example: Update UI or trigger actions based on response
            UpdateUIWithResponse(response);
        }
        
        private void SaveDebugImage(Texture2D image)
        {
            try
            {
                byte[] bytes = image.EncodeToJPG(80);
                string filename = $"debug_capture_{System.DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                string path = System.IO.Path.Combine(Application.persistentDataPath, filename);
                System.IO.File.WriteAllBytes(path, bytes);
                
                Debug.Log($"[XREALVisionExample] Debug image saved: {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[XREALVisionExample] Failed to save debug image: {e.Message}");
            }
        }
        
        private void UpdateUIWithResponse(string response)
        {
            // Example UI updates - implement based on your UI system
            // This could update text displays, trigger animations, etc.
        }
        
        private void RecoverFromError()
        {
            if (visionSystem != null && visionSystem.IsInitialized)
            {
                Debug.Log("[VisionExample] Attempting to recover from error");
                visionSystem.StartListening();
            }
        }
        
        #endregion
        
        #region Input Handlers
        
        private void Update()
        {
            HandleInput();
            UpdatePerformanceStats();
        }
        
        private void HandleInput()
        {
            if (visionSystem == null) return;
            
            // Example keyboard controls for testing
            if (Input.GetKeyDown(KeyCode.Space))
            {
                visionSystem.ToggleListening();
                Debug.Log("[XREALVisionExample] Toggled listening");
            }
            
            if (Input.GetKeyDown(KeyCode.T))
            {
                visionSystem.ProcessTextCommand("Describe what you see in detail");
                Debug.Log("[XREALVisionExample] Sent test command");
            }
            
            if (Input.GetKeyDown(KeyCode.S))
            {
                visionSystem.StopListening();
                Debug.Log("[XREALVisionExample] Stopped listening");
            }
        }
        
        private void UpdatePerformanceStats()
        {
            if (!showPerformanceStats) return;
            
            if (Time.time - lastStatsUpdate >= statsUpdateInterval)
            {
                DisplayPerformanceStats();
                lastStatsUpdate = Time.time;
            }
        }
        
        private void DisplayPerformanceStats()
        {
            if (visionSystem == null) return;
            
            var fps = 1f / Time.deltaTime;
            var memoryUsage = System.GC.GetTotalMemory(false) / (1024 * 1024); // MB
            
            Debug.Log($"[XREALVisionExample] Performance Stats - " +
                     $"FPS: {fps:F1}, " +
                     $"Memory: {memoryUsage}MB, " +
                     $"Commands Processed: {commandsProcessed}, " +
                     $"System State: {visionSystem.CurrentState}");
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Manually trigger a vision analysis with a custom prompt
        /// </summary>
        public void AnalyzeScene(string customPrompt = "What do you see?")
        {
            if (visionSystem != null && visionSystem.IsInitialized)
            {
                visionSystem.ProcessTextCommand(customPrompt);
            }
        }
        
        /// <summary>
        /// Toggle the system's listening state
        /// </summary>
        public void ToggleListening()
        {
            if (visionSystem != null)
            {
                visionSystem.ToggleListening();
            }
        }
        
        /// <summary>
        /// Get current system status information
        /// </summary>
        public string GetSystemStatus()
        {
            if (visionSystem == null) return "System not initialized";
            
            return $"State: {visionSystem.CurrentState}, " +
                   $"Initialized: {visionSystem.IsInitialized}, " +
                   $"Processing: {visionSystem.IsProcessing}";
        }
        
        #endregion
        
        private void OnDestroy()
        {
            // Clean up event subscriptions
            if (visionSystem != null)
            {
                visionSystem.OnSystemReady.RemoveListener(OnSystemReady);
                visionSystem.OnSystemError.RemoveListener(OnSystemError);
                visionSystem.OnVoiceCommandReceived.RemoveListener(OnVoiceCommandReceived);
                visionSystem.OnImageCaptured.RemoveListener(OnImageCaptured);
                visionSystem.OnVisionResponse.RemoveListener(OnVisionResponse);
            }
        }
        
        #region Unity Editor Helpers
        
        #if UNITY_EDITOR
        [UnityEditor.MenuItem("XREAL Vision/Test Vision Analysis")]
        private static void TestVisionAnalysis()
        {
            var example = FindFirstObjectByType<XREALVisionSystemExample>();
            if (example != null)
            {
                example.AnalyzeScene("Provide a detailed analysis of the current scene");
            }
            else
            {
                Debug.LogWarning("No XREALVisionSystemExample found in scene");
            }
        }
        
        [UnityEditor.MenuItem("XREAL Vision/Show System Status")]
        private static void ShowSystemStatus()
        {
            var example = FindFirstObjectByType<XREALVisionSystemExample>();
            if (example != null)
            {
                Debug.Log($"System Status: {example.GetSystemStatus()}");
            }
            else
            {
                Debug.LogWarning("No XREALVisionSystemExample found in scene");
            }
        }
        #endif
        
        #endregion
    }
}
