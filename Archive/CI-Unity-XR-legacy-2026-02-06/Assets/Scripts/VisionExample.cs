using UnityEngine;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Example scene configuration for XREAL Vision System
    /// Demonstrates how to set up and use the consolidated vision system
    /// </summary>
    public class XREALVisionExample : MonoBehaviour
    {
        [Header("Example Configuration")]
        [SerializeField] private bool autoSetupScene = true;
        [SerializeField] private bool enableExampleUI = true;
        
        [Header("Server Settings")]
        [SerializeField] private string ollamaServerIP = "192.168.1.100";
        [SerializeField] private int ollamaServerPort = 11434;
        [SerializeField] private string ollamaModel = "llava:7b";
        
        [Header("API Configuration")]
        [SerializeField] private string openAIApiKey = "";
        
        [Header("Example Commands")]
        [SerializeField] private string[] exampleCommands = {
            "What do you see?",
            "Describe the scene",
            "What objects can you identify?",
            "What colors are visible?",
            "Tell me about the environment"
        };
        
        private VisionSystem visionSystem;
        private int currentCommandIndex = 0;
        
        private void Start()
        {
            if (autoSetupScene)
            {
                SetupExampleScene();
            }
        }
        
        private void SetupExampleScene()
        {
            Debug.Log("[XREALVisionExample] Setting up example scene...");
            
            // Use the scene setup helper to create the vision system
            var sceneSetup = gameObject.AddComponent<XREALVisionSceneSetup>();
            
            // Configure the setup component
            var setupType = typeof(XREALVisionSceneSetup);
            SetPrivateField(sceneSetup, "ollamaServerIP", ollamaServerIP);
            SetPrivateField(sceneSetup, "ollamaServerPort", ollamaServerPort);
            SetPrivateField(sceneSetup, "ollamaModel", ollamaModel);
            SetPrivateField(sceneSetup, "openAIApiKey", openAIApiKey);
            SetPrivateField(sceneSetup, "createUICanvas", enableExampleUI);
            SetPrivateField(sceneSetup, "addDebugControls", true);
            
            // Setup the scene
            sceneSetup.SetupVisionScene();
            
            // Get reference to the created vision system
            visionSystem = FindObjectOfType<VisionSystem>();
            
            if (visionSystem != null)
            {
                // Subscribe to vision system events for demonstration
                visionSystem.OnVoiceCommandReceived.AddListener(OnVoiceCommandReceived);
                visionSystem.OnVisionResponse.AddListener(OnVisionResponse);
                visionSystem.OnSystemError.AddListener(OnSystemError);
                visionSystem.OnSystemReady.AddListener(OnSystemReady);
                
                Debug.Log("[XREALVisionExample] Example scene setup complete!");
            }
            else
            {
                Debug.LogError("[XREALVisionExample] Failed to create vision system!");
            }
        }
        
        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
            }
        }
        
        #region Event Handlers
        
        private void OnVoiceCommandReceived(string command)
        {
            Debug.Log($"[XREALVisionExample] Voice command: {command}");
        }
        
        private void OnVisionResponse(string response)
        {
            Debug.Log($"[XREALVisionExample] Vision response: {response}");
        }
        
        private void OnSystemError(string error)
        {
            Debug.LogError($"[XREALVisionExample] System error: {error}");
        }
        
        private void OnSystemReady()
        {
            Debug.Log("[XREALVisionExample] Vision system is ready!");
        }
        
        #endregion
        
        #region Public API for Testing
        
        /// <summary>
        /// Test the system with a random example command
        /// </summary>
        [ContextMenu("Test Random Command")]
        public void TestRandomCommand()
        {
            if (visionSystem == null || exampleCommands.Length == 0) return;
            
            string command = exampleCommands[Random.Range(0, exampleCommands.Length)];
            Debug.Log($"[XREALVisionExample] Testing command: {command}");
            visionSystem.ProcessTextCommand(command);
        }
        
        /// <summary>
        /// Test the system with the next example command
        /// </summary>
        [ContextMenu("Test Next Command")]
        public void TestNextCommand()
        {
            if (visionSystem == null || exampleCommands.Length == 0) return;
            
            string command = exampleCommands[currentCommandIndex];
            currentCommandIndex = (currentCommandIndex + 1) % exampleCommands.Length;
            
            Debug.Log($"[XREALVisionExample] Testing command: {command}");
            visionSystem.ProcessTextCommand(command);
        }
        
        /// <summary>
        /// Start voice listening
        /// </summary>
        [ContextMenu("Start Listening")]
        public void StartListening()
        {
            if (visionSystem != null)
            {
                visionSystem.StartListening();
                Debug.Log("[XREALVisionExample] Started voice listening");
            }
        }
        
        /// <summary>
        /// Stop voice listening
        /// </summary>
        [ContextMenu("Stop Listening")]
        public void StopListening()
        {
            if (visionSystem != null)
            {
                visionSystem.StopListening();
                Debug.Log("[XREALVisionExample] Stopped voice listening");
            }
        }
        
        #endregion
        
        private void Update()
        {
            // Additional example controls
            if (Input.GetKeyDown(KeyCode.E))
            {
                TestRandomCommand();
            }
            
            if (Input.GetKeyDown(KeyCode.N))
            {
                TestNextCommand();
            }
        }
        
        private void OnGUI()
        {
            if (!Application.isEditor) return;
            
            // Additional GUI for example
            float yOffset = 130; // Below the debug controls
            GUI.Box(new Rect(10, Screen.height - yOffset - 90, 300, 80), "XREAL Vision Example");
            GUI.Label(new Rect(20, Screen.height - yOffset - 70, 280, 20), "E - Test random command");
            GUI.Label(new Rect(20, Screen.height - yOffset - 50, 280, 20), "N - Test next command");
            GUI.Label(new Rect(20, Screen.height - yOffset - 30, 280, 20), $"Current model: {ollamaModel}");
            
            if (visionSystem != null)
            {
                string status = visionSystem.IsInitialized ? "Ready" : "Initializing...";
                GUI.Label(new Rect(20, Screen.height - yOffset - 10, 280, 20), $"Status: {status}");
            }
        }
        
        private void OnDestroy()
        {
            // Cleanup event subscriptions
            if (visionSystem != null)
            {
                visionSystem.OnVoiceCommandReceived.RemoveListener(OnVoiceCommandReceived);
                visionSystem.OnVisionResponse.RemoveListener(OnVisionResponse);
                visionSystem.OnSystemError.RemoveListener(OnSystemError);
                visionSystem.OnSystemReady.RemoveListener(OnSystemReady);
            }
        }
    }
}
