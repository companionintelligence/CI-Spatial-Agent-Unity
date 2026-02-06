using UnityEngine;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// XREAL Vision Scene Setup - Automatic scene configuration for the consolidated vision system
    /// Simplified setup script that creates and configures all necessary components
    /// </summary>
    public class XREALVisionSceneSetup : MonoBehaviour
    {
        [Header("Auto Setup Configuration")]
        [SerializeField] private bool setupOnAwake = true;
        [SerializeField] private bool createUICanvas = true;
        [SerializeField] private bool addDebugControls = true;
        
        [Header("Server Configuration")]
        [SerializeField] private string ollamaServerIP = "192.168.1.100";
        [SerializeField] private int ollamaServerPort = 11434;
        [SerializeField] private string ollamaModel = "llava:7b";
        
        [Header("API Keys")]
        [SerializeField] private string openAIApiKey = "YOUR_OPENAI_API_KEY";
        
        private void Awake()
        {
            if (setupOnAwake)
            {
                SetupVisionScene();
            }
        }
        
        [ContextMenu("Setup XREAL Vision Scene")]
        public void SetupVisionScene()
        {
            Debug.Log("[XREALVisionSceneSetup] Setting up XREAL Vision Scene...");
            
            // Create main vision system GameObject
            GameObject visionSystemGO = CreateVisionSystemGameObject();
            
            // Create UI if requested
            if (createUICanvas)
            {
                CreateSimpleUI();
            }
            
            // Add debug controls if requested
            if (addDebugControls)
            {
                AddDebugControls(visionSystemGO);
            }
            
            Debug.Log("[XREALVisionSceneSetup] XREAL Vision Scene setup complete!");
        }
        
        private GameObject CreateVisionSystemGameObject()
        {
            // Check if vision system already exists
            VisionSystem existingSystem = FindObjectOfType<VisionSystem>();
            if (existingSystem != null)
            {
                Debug.Log("[XREALVisionSceneSetup] Vision system already exists, configuring existing system");
                ConfigureExistingSystem(existingSystem.gameObject);
                return existingSystem.gameObject;
            }
            
            // Create new vision system GameObject
            GameObject visionSystemGO = new GameObject("XREAL Vision System");
            
            // Add core components
            var cameraManager = visionSystemGO.AddComponent<CameraManager>();
            var speechToText = visionSystemGO.AddComponent<SpeechToText>();
            var textToSpeech = visionSystemGO.AddComponent<TextToSpeech>();
            var ollamaConnector = visionSystemGO.AddComponent<OllamaConnector>();
            var visionSystem = visionSystemGO.AddComponent<VisionSystem>();
            var voiceCommandHandler = visionSystemGO.AddComponent<VoiceCommandHandler>();
            
            // Configure components
            ConfigureOllamaConnector(ollamaConnector);
            ConfigureSpeechToText(speechToText);
            ConfigureTextToSpeech(textToSpeech);
            ConfigureVisionSystem(visionSystem, cameraManager, speechToText, textToSpeech, ollamaConnector);
            ConfigureVoiceCommandHandler(voiceCommandHandler, visionSystem, cameraManager, ollamaConnector, speechToText);
            
            Debug.Log("[XREALVisionSceneSetup] Created new vision system with all components");
            return visionSystemGO;
        }
        
        private void ConfigureExistingSystem(GameObject visionSystemGO)
        {
            // Ensure all required components exist
            var cameraManager = GetOrAddComponent<CameraManager>(visionSystemGO);
            var speechToText = GetOrAddComponent<SpeechToText>(visionSystemGO);
            var textToSpeech = GetOrAddComponent<TextToSpeech>(visionSystemGO);
            var ollamaConnector = GetOrAddComponent<OllamaConnector>(visionSystemGO);
            var visionSystem = GetOrAddComponent<VisionSystem>(visionSystemGO);
            var voiceCommandHandler = GetOrAddComponent<VoiceCommandHandler>(visionSystemGO);
            
            // Configure components
            ConfigureOllamaConnector(ollamaConnector);
            ConfigureSpeechToText(speechToText);
            ConfigureTextToSpeech(textToSpeech);
            ConfigureVisionSystem(visionSystem, cameraManager, speechToText, textToSpeech, ollamaConnector);
            ConfigureVoiceCommandHandler(voiceCommandHandler, visionSystem, cameraManager, ollamaConnector, speechToText);
        }
        
        private T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null)
            {
                component = go.AddComponent<T>();
                Debug.Log($"[XREALVisionSceneSetup] Added missing component: {typeof(T).Name}");
            }
            return component;
        }
        
        private void ConfigureOllamaConnector(OllamaConnector connector)
        {
            // Use reflection to set private fields since they might not have public setters
            SetField(connector, "serverIP", ollamaServerIP);
            SetField(connector, "serverPort", ollamaServerPort);
            SetField(connector, "model", ollamaModel);
        }
        
        private void ConfigureSpeechToText(SpeechToText stt)
        {
            if (!string.IsNullOrEmpty(openAIApiKey) && openAIApiKey != "YOUR_OPENAI_API_KEY")
            {
                stt.SetOpenAIApiKey(openAIApiKey);
            }
        }
        
        private void ConfigureTextToSpeech(TextToSpeech tts)
        {
            if (!string.IsNullOrEmpty(openAIApiKey) && openAIApiKey != "YOUR_OPENAI_API_KEY")
            {
                tts.SetOpenAIApiKey(openAIApiKey);
            }
        }
        
        private void ConfigureVisionSystem(VisionSystem visionSystem, CameraManager cameraManager, 
            SpeechToText speechToText, TextToSpeech textToSpeech, OllamaConnector ollamaConnector)
        {
            // Set component references using reflection
            SetField(visionSystem, "cameraManager", cameraManager);
            SetField(visionSystem, "speechToText", speechToText);
            SetField(visionSystem, "textToSpeech", textToSpeech);
            SetField(visionSystem, "ollamaConnector", ollamaConnector);
        }
        
        private void ConfigureVoiceCommandHandler(VoiceCommandHandler handler, VisionSystem visionSystem,
            CameraManager cameraManager, OllamaConnector ollamaConnector, SpeechToText speechToText)
        {
            // Set component references using reflection
            SetField(handler, "visionSystem", visionSystem);
            SetField(handler, "cameraManager", cameraManager);
            SetField(handler, "ollamaConnector", ollamaConnector);
            SetField(handler, "speechToText", speechToText);
        }
        
        private void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
            }
            else
            {
                Debug.LogWarning($"[XREALVisionSceneSetup] Field '{fieldName}' not found on {obj.GetType().Name}");
            }
        }
        
        private void CreateSimpleUI()
        {
            // Check if canvas already exists
            Canvas existingCanvas = FindObjectOfType<Canvas>();
            if (existingCanvas != null)
            {
                Debug.Log("[XREALVisionSceneSetup] Canvas already exists, skipping UI creation");
                return;
            }
            
            // Create UI Canvas
            GameObject canvasGO = new GameObject("XREAL Vision UI");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            // Create simple status text
            CreateStatusText(canvasGO);
            
            Debug.Log("[XREALVisionSceneSetup] Created simple UI canvas");
        }
        
        private void CreateStatusText(GameObject canvasGO)
        {
            GameObject textGO = new GameObject("Status Text");
            textGO.transform.SetParent(canvasGO.transform, false);
            
            var text = textGO.AddComponent<UnityEngine.UI.Text>();
            text.text = "XREAL Vision System\nPress 'T' to test\nPress 'L' to toggle listening";
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            
            // Set default font
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            
            var rectTransform = textGO.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.pivot = new Vector2(0, 1);
            rectTransform.anchoredPosition = new Vector2(10, -10);
            rectTransform.sizeDelta = new Vector2(400, 100);
        }
        
        private void AddDebugControls(GameObject visionSystemGO)
        {
            var debugControls = visionSystemGO.AddComponent<XREALVisionDebugControls>();
            Debug.Log("[XREALVisionSceneSetup] Added debug controls");
        }
    }
    
    /// <summary>
    /// Simple debug controls for testing the vision system
    /// </summary>
    public class XREALVisionDebugControls : MonoBehaviour
    {
        private VoiceCommandHandler voiceHandler;
        private SpeechToText speechToText;
        private VisionSystem visionSystem;
        
        private void Start()
        {
            voiceHandler = GetComponent<VoiceCommandHandler>();
            speechToText = GetComponent<SpeechToText>();
            visionSystem = GetComponent<VisionSystem>();
        }
        
        private void Update()
        {
            // Test commands
            if (Input.GetKeyDown(KeyCode.T))
            {
                voiceHandler?.ProcessTextCommand("What do you see in this image?");
                Debug.Log("[XREALVisionDebugControls] Triggered test command");
            }
            
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                voiceHandler?.ProcessTextCommand("Describe what you see");
            }
            
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                voiceHandler?.ProcessTextCommand("What objects can you identify?");
            }
            
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                voiceHandler?.ProcessTextCommand("What colors do you see?");
            }
            
            // Voice controls
            if (Input.GetKeyDown(KeyCode.L))
            {
                speechToText?.ToggleListening();
                Debug.Log("[VisionDebugControls] Toggled voice listening");
            }
            
            if (Input.GetKeyDown(KeyCode.Space))
            {
                speechToText?.StartListening();
                Debug.Log("[VisionDebugControls] Started listening");
            }
            
            if (Input.GetKeyUp(KeyCode.Space))
            {
                speechToText?.StopListening();
                Debug.Log("[VisionDebugControls] Stopped listening");
            }
            
            // System controls
            if (Input.GetKeyDown(KeyCode.R))
            {
                // Restart system
                if (visionSystem != null)
                {
                    visionSystem.gameObject.SetActive(false);
                    visionSystem.gameObject.SetActive(true);
                    Debug.Log("[XREALVisionDebugControls] Restarted vision system");
                }
            }
        }
        
        private void OnGUI()
        {
            if (!Application.isEditor) return;
            
            GUI.Box(new Rect(10, Screen.height - 120, 300, 110), "XREAL Vision Debug Controls");
            GUI.Label(new Rect(20, Screen.height - 100, 280, 20), "T - Test vision command");
            GUI.Label(new Rect(20, Screen.height - 80, 280, 20), "1,2,3 - Quick test commands");
            GUI.Label(new Rect(20, Screen.height - 60, 280, 20), "L - Toggle voice listening");
            GUI.Label(new Rect(20, Screen.height - 40, 280, 20), "Space - Hold to listen");
            GUI.Label(new Rect(20, Screen.height - 20, 280, 20), "R - Restart system");
        }
    }
}
