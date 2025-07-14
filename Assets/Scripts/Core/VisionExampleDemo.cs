using System.Collections;
using UnityEngine;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Example demonstration of the XREAL Vision-Language system
    /// Shows complete workflow from voice command to AI response
    /// </summary>
    public class VisionExampleDemo : MonoBehaviour
    {
        [Header("Demo Configuration")]
        [SerializeField] private bool autoStartDemo = false;
        [SerializeField] private float demoInterval = 10f;
        [SerializeField] private bool enableVoiceDemo = true;
        [SerializeField] private bool enableContinuousDemo = false;
        
        [Header("Demo Commands")]
        [SerializeField] private string[] demoCommands = {
            "What do you see in front of me?",
            "Describe the objects around me",
            "What colors are in this scene?",
            "Tell me about the lighting conditions",
            "Are there any people visible?",
            "What's the overall mood of this environment?"
        };
        
        [Header("Components")]
        [SerializeField] private VisionLanguageManager visionManager;
        [SerializeField] private XREALVisionController visionController;
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private OllamaClient ollamaClient;
        
        // Demo state
        private bool isDemoRunning = false;
        private int currentCommandIndex = 0;
        private Coroutine demoCoroutine;
        
        // Performance tracking
        private float lastCommandTime;
        private int successfulCommands = 0;
        private int totalCommands = 0;
        
        private void Start()
        {
            StartCoroutine(WaitForSystemAndStartDemo());
        }
        
        private IEnumerator WaitForSystemAndStartDemo()
        {
            Debug.Log("[VisionExampleDemo] Waiting for XREAL Vision system to initialize...");
            
            // Wait for all systems to be ready
            while (!IsSystemReady())
            {
                yield return new WaitForSeconds(1f);
            }
            
            Debug.Log("[VisionExampleDemo] XREAL Vision system ready!");
            
            // Subscribe to events for demo tracking
            SetupEventSubscriptions();
            
            // Start demo if auto-start enabled
            if (autoStartDemo)
            {
                yield return new WaitForSeconds(2f); // Give user time to see ready message
                StartDemo();
            }
            
            // Show demo instructions
            ShowDemoInstructions();
        }
        
        private bool IsSystemReady()
        {
            return visionManager != null &&
                   cameraManager != null && cameraManager.IsInitialized &&
                   ollamaClient != null && ollamaClient.IsInitialized;
        }
        
        private void SetupEventSubscriptions()
        {
            if (visionManager != null)
            {
                visionManager.OnVoiceCommandReceived.AddListener(OnVoiceCommandReceived);
                visionManager.OnVisionResponseReceived.AddListener(OnVisionResponseReceived);
                visionManager.OnErrorOccurred.AddListener(OnErrorOccurred);
            }
        }
        
        private void ShowDemoInstructions()
        {
            Debug.Log("=== XREAL Vision Demo Instructions ===");
            Debug.Log("Voice Commands:");
            Debug.Log("- Speak naturally: 'What do you see?'");
            Debug.Log("- Try: 'Describe this image'");
            Debug.Log("- Ask: 'What colors are visible?'");
            Debug.Log("");
            Debug.Log("Keyboard Controls:");
            Debug.Log("- D: Start/Stop Demo");
            Debug.Log("- C: Manual Capture");
            Debug.Log("- Space: Manual Capture (alternative)");
            Debug.Log("- 1-6: Test demo commands");
            Debug.Log("- S: Toggle Settings Panel");
            Debug.Log("");
            Debug.Log("Demo Features:");
            Debug.Log("- Real-time camera capture from XREAL");
            Debug.Log("- Speech-to-Text for voice commands");
            Debug.Log("- Ollama vision model analysis");
            Debug.Log("- Text-to-Speech audio responses");
            Debug.Log("=====================================");
        }
        
        [ContextMenu("Start Demo")]
        public void StartDemo()
        {
            if (isDemoRunning)
            {
                Debug.Log("[VisionExampleDemo] Demo already running");
                return;
            }
            
            isDemoRunning = true;
            currentCommandIndex = 0;
            
            if (enableContinuousDemo)
            {
                demoCoroutine = StartCoroutine(ContinuousDemoLoop());
                Debug.Log("[VisionExampleDemo] Started continuous demo mode");
            }
            else
            {
                Debug.Log("[VisionExampleDemo] Demo mode active - use keyboard controls");
            }
        }
        
        [ContextMenu("Stop Demo")]
        public void StopDemo()
        {
            if (!isDemoRunning)
                return;
            
            isDemoRunning = false;
            
            if (demoCoroutine != null)
            {
                StopCoroutine(demoCoroutine);
                demoCoroutine = null;
            }
            
            Debug.Log("[VisionExampleDemo] Demo stopped");
            ShowDemoStats();
        }
        
        private IEnumerator ContinuousDemoLoop()
        {
            while (isDemoRunning)
            {
                if (visionManager != null && !visionManager.IsProcessing)
                {
                    // Execute next demo command
                    string command = demoCommands[currentCommandIndex];
                    ExecuteDemoCommand(command);
                    
                    currentCommandIndex = (currentCommandIndex + 1) % demoCommands.Length;
                }
                
                yield return new WaitForSeconds(demoInterval);
            }
        }
        
        public void ExecuteDemoCommand(string command)
        {
            if (visionManager == null || visionManager.IsProcessing)
                return;
            
            Debug.Log($"[VisionExampleDemo] Executing demo command: \"{command}\"");
            
            totalCommands++;
            lastCommandTime = Time.time;
            
            // Process the command
            visionManager.ProcessTextCommand(command);
        }
        
        public void ExecuteTestCommand(int commandIndex)
        {
            if (commandIndex >= 0 && commandIndex < demoCommands.Length)
            {
                ExecuteDemoCommand(demoCommands[commandIndex]);
            }
        }
        
        // Event handlers for tracking demo performance
        private void OnVoiceCommandReceived(string command)
        {
            Debug.Log($"[VisionExampleDemo] Voice command detected: \"{command}\"");
        }
        
        private void OnVisionResponseReceived(string response)
        {
            successfulCommands++;
            float responseTime = Time.time - lastCommandTime;
            
            Debug.Log($"[VisionExampleDemo] Response received in {responseTime:F1}s: \"{response.Substring(0, Mathf.Min(50, response.Length))}...\"");
        }
        
        private void OnErrorOccurred(string error)
        {
            Debug.LogError($"[VisionExampleDemo] Error during demo: {error}");
        }
        
        private void ShowDemoStats()
        {
            Debug.Log("=== Demo Statistics ===");
            Debug.Log($"Total Commands: {totalCommands}");
            Debug.Log($"Successful Responses: {successfulCommands}");
            Debug.Log($"Success Rate: {(totalCommands > 0 ? (successfulCommands * 100f / totalCommands) : 0):F1}%");
            Debug.Log("=======================");
        }
        
        // Testing and demo controls
        private void Update()
        {
            if (!IsSystemReady())
                return;
            
            // Demo controls
            if (Input.GetKeyDown(KeyCode.D))
            {
                if (isDemoRunning)
                    StopDemo();
                else
                    StartDemo();
            }
            
            // Manual capture
            if (Input.GetKeyDown(KeyCode.C))
            {
                if (visionManager != null)
                    visionManager.TriggerManualCapture();
            }
            
            // Test commands (1-6 keys)
            for (int i = 0; i < 6 && i < demoCommands.Length; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    ExecuteTestCommand(i);
                }
            }
            
            // Voice activation simulation (for testing without actual voice)
            if (Input.GetKeyDown(KeyCode.V))
            {
                SimulateVoiceCommand();
            }
            
            // Show current status
            if (Input.GetKeyDown(KeyCode.I))
            {
                ShowSystemInfo();
            }
        }
        
        private void SimulateVoiceCommand()
        {
            // Simulate receiving a voice command for testing
            string[] quickCommands = {
                "What's in this image?",
                "Describe what you see",
                "Tell me about the scene"
            };
            
            string command = quickCommands[Random.Range(0, quickCommands.Length)];
            Debug.Log($"[VisionExampleDemo] Simulating voice command: \"{command}\"");
            
            if (visionManager != null)
            {
                // Directly call the voice command handler
                StartCoroutine(visionManager.ProcessVisionLanguageRequest(command));
            }
        }
        
        private void ShowSystemInfo()
        {
            Debug.Log("=== System Status ===");
            Debug.Log($"Camera Ready: {(cameraManager != null ? cameraManager.IsInitialized : false)}");
            Debug.Log($"Ollama Connected: {(ollamaClient != null ? ollamaClient.IsConnected : false)}");
            Debug.Log($"Vision Manager Ready: {(visionManager != null)}");
            Debug.Log($"Currently Processing: {(visionManager != null ? visionManager.IsProcessing : false)}");
            Debug.Log($"Demo Running: {isDemoRunning}");
            Debug.Log("====================");
        }
        
        // Public API for external testing
        public void TestImageAnalysis()
        {
            ExecuteDemoCommand("Analyze this image and tell me what you see");
        }
        
        public void TestSceneDescription()
        {
            ExecuteDemoCommand("Describe the current scene in detail");
        }
        
        public void TestColorAnalysis()
        {
            ExecuteDemoCommand("What are the dominant colors in this image?");
        }
        
        public void TestObjectDetection()
        {
            ExecuteDemoCommand("What objects can you identify in this scene?");
        }
        
        // Performance monitoring
        public float GetSuccessRate()
        {
            return totalCommands > 0 ? (successfulCommands * 100f / totalCommands) : 0f;
        }
        
        public int GetTotalCommands()
        {
            return totalCommands;
        }
        
        public bool IsDemoRunning()
        {
            return isDemoRunning;
        }
        
        private void OnDestroy()
        {
            StopDemo();
            
            // Unsubscribe from events
            if (visionManager != null)
            {
                visionManager.OnVoiceCommandReceived.RemoveListener(OnVoiceCommandReceived);
                visionManager.OnVisionResponseReceived.RemoveListener(OnVisionResponseReceived);
                visionManager.OnErrorOccurred.RemoveListener(OnErrorOccurred);
            }
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && isDemoRunning)
            {
                // Pause demo when app is paused
                StopDemo();
            }
        }
    }
}

// Example usage in a MonoBehaviour for quick testing
public class QuickVisionTest : MonoBehaviour
{
    private VisionExampleDemo demo;
    
    private void Start()
    {
        demo = FindObjectOfType<VisionExampleDemo>();
        
        // Quick test after 5 seconds
        Invoke(nameof(RunQuickTest), 5f);
    }
    
    private void RunQuickTest()
    {
        if (demo != null)
        {
            demo.TestSceneDescription();
        }
    }
}
