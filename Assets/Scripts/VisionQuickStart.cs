using UnityEngine;
using CompanionIntelligence.Core;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Simplified XREAL Vision System Setup for Quick Testing
    /// Use this script to quickly test the improved vision system
    /// </summary>
    public class XREALVisionQuickStart : MonoBehaviour
    {
        [Header("Quick Setup Options")]
        [SerializeField] private bool createCompleteSystem = true;
        [SerializeField] private bool enableAutoListening = true;
        [SerializeField] private bool showDebugUI = true;
        
        [Header("Test Configuration")]
        [SerializeField] private string testPrompt = "What do you see?";
        [SerializeField] private KeyCode testKey = KeyCode.T;
        [SerializeField] private KeyCode toggleListeningKey = KeyCode.L;
        
        private VisionSystem visionSystem;
        private bool systemReady = false;
        
        private void Start()
        {
            if (createCompleteSystem)
            {
                SetupVisionSystem();
            }
            else
            {
                FindExistingSystem();
            }
        }
        
        private void SetupVisionSystem()
        {
            Debug.Log("[XREALVisionQuickStart] Setting up complete vision system...");
            
            // Use the scene setup script if available
            var sceneSetup = FindFirstObjectByType<XREALVisionSceneSetup>();
            if (sceneSetup != null)
            {
                sceneSetup.SetupVisionScene();
            }
            else
            {
                // Manual setup
                CreateManualSetup();
            }
            
            FindExistingSystem();
        }
        
        private void CreateManualSetup()
        {
            Debug.Log("[XREALVisionQuickStart] Creating manual setup...");
            
            var visionSystemGO = new GameObject("XREAL Vision System");
            visionSystemGO.AddComponent<VisionSystem>();
            visionSystemGO.AddComponent<CameraManager>();
            visionSystemGO.AddComponent<OllamaConnector>();
            visionSystemGO.AddComponent<SpeechToText>();
            visionSystemGO.AddComponent<TextToSpeech>();
            visionSystemGO.AddComponent<VoiceCommandHandler>();
            
            Debug.Log("[XREALVisionQuickStart] Manual setup complete");
        }
        
        private void FindExistingSystem()
        {
            visionSystem = FindFirstObjectByType<VisionSystem>();
            
            if (visionSystem != null)
            {
                SetupEventHandlers();
                Debug.Log("[XREALVisionQuickStart] Found and configured vision system");
            }
            else
            {
                Debug.LogWarning("[XREALVisionQuickStart] No vision system found!");
            }
        }
        
        private void SetupEventHandlers()
        {
            visionSystem.OnSystemReady.AddListener(OnSystemReady);
            visionSystem.OnVoiceCommandReceived.AddListener(OnVoiceCommand);
            visionSystem.OnVisionResponse.AddListener(OnVisionResponse);
            visionSystem.OnSystemError.AddListener(OnSystemError);
        }
        
        private void OnSystemReady()
        {
            systemReady = true;
            Debug.Log("[XREALVisionQuickStart] ✅ System ready!");
            
            if (enableAutoListening)
            {
                visionSystem.StartListening();
                Debug.Log("[XREALVisionQuickStart] 🎤 Auto-listening enabled");
            }
        }
        
        private void OnVoiceCommand(string command)
        {
            Debug.Log($"[XREALVisionQuickStart] 🗣️ Voice: {command}");
        }
        
        private void OnVisionResponse(string response)
        {
            Debug.Log($"[XREALVisionQuickStart] 👁️ Vision: {response}");
        }
        
        private void OnSystemError(string error)
        {
            Debug.LogError($"[XREALVisionQuickStart] ❌ Error: {error}");
        }
        
        private void Update()
        {
            if (visionSystem == null) return;
            
            // Test controls
            if (Input.GetKeyDown(testKey))
            {
                TestVisionSystem();
            }
            
            if (Input.GetKeyDown(toggleListeningKey))
            {
                ToggleListening();
            }
        }
        
        private void TestVisionSystem()
        {
            if (systemReady)
            {
                Debug.Log($"[XREALVisionQuickStart] 🧪 Testing with prompt: {testPrompt}");
                visionSystem.ProcessTextCommand(testPrompt);
            }
            else
            {
                Debug.LogWarning("[XREALVisionQuickStart] System not ready for testing");
            }
        }
        
        private void ToggleListening()
        {
            if (systemReady)
            {
                visionSystem.ToggleListening();
                Debug.Log("[XREALVisionQuickStart] 🔄 Toggled listening");
            }
        }
        
        private void OnGUI()
        {
            if (!showDebugUI) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("XREAL Vision System - Quick Start", EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).label);
            
            if (visionSystem != null)
            {
                GUILayout.Label($"Status: {visionSystem.CurrentState}");
                GUILayout.Label($"Ready: {systemReady}");
                GUILayout.Label($"Processing: {visionSystem.IsProcessing}");
                
                GUILayout.Space(10);
                
                if (GUILayout.Button($"Test Vision ({testKey})"))
                {
                    TestVisionSystem();
                }
                
                if (GUILayout.Button($"Toggle Listening ({toggleListeningKey})"))
                {
                    ToggleListening();
                }
            }
            else
            {
                GUILayout.Label("No Vision System Found");
                if (GUILayout.Button("Setup System"))
                {
                    SetupVisionSystem();
                }
            }
            
            GUILayout.EndArea();
        }
        
        private void OnDestroy()
        {
            if (visionSystem != null)
            {
                visionSystem.OnSystemReady.RemoveListener(OnSystemReady);
                visionSystem.OnVoiceCommandReceived.RemoveListener(OnVoiceCommand);
                visionSystem.OnVisionResponse.RemoveListener(OnVisionResponse);
                visionSystem.OnSystemError.RemoveListener(OnSystemError);
            }
        }
    }
}
