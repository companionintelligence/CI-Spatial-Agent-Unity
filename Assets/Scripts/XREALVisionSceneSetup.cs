using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// XREAL Vision Scene Setup Helper
    /// Automatically configures a scene for the vision-language system
    /// </summary>
    public class XREALVisionSceneSetup : MonoBehaviour
    {
        [Header("Auto Setup Configuration")]
        [SerializeField] private bool setupOnAwake = true;
        [SerializeField] private bool createUI = true;
        [SerializeField] private bool configureComponents = true;
        [SerializeField] private bool addTestingControls = true;
        
        [Header("Ollama Server Settings")]
        [SerializeField] private string defaultServerIP = "192.168.1.100";
        [SerializeField] private int defaultServerPort = 11434;
        [SerializeField] private string defaultModel = "llava:7b";
        
        [Header("Prefab References")]
        [SerializeField] private GameObject visionSystemPrefab;
        [SerializeField] private GameObject uiCanvasPrefab;
        
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
            Debug.Log("[XREALVisionSceneSetup] Setting up XREAL Vision scene...");
            
            // Create or find main vision system
            GameObject visionSystem = SetupVisionSystem();
            
            // Create UI if requested
            if (createUI)
            {
                SetupVisionUI(visionSystem);
            }
            
            // Configure components
            if (configureComponents)
            {
                ConfigureVisionComponents(visionSystem);
            }
            
            // Add testing controls
            if (addTestingControls)
            {
                SetupTestingControls(visionSystem);
            }
            
            Debug.Log("[XREALVisionSceneSetup] XREAL Vision scene setup complete!");
        }
        
        private GameObject SetupVisionSystem()
        {
            // Check if vision system already exists
            var existing = FindObjectOfType<VisionLanguageManager>();
            if (existing != null)
            {
                Debug.Log("[XREALVisionSceneSetup] Vision system already exists, using existing");
                return existing.gameObject;
            }
            
            // Create new vision system
            GameObject visionSystem;
            
            if (visionSystemPrefab != null)
            {
                visionSystem = Instantiate(visionSystemPrefab);
            }
            else
            {
                visionSystem = new GameObject("XREALVisionSystem");
            }
            
            visionSystem.name = "XREALVisionSystem";
            
            // Add core components if not present
            EnsureComponent<CameraManager>(visionSystem);
            EnsureComponent<SpeechToTextManager>(visionSystem);
            EnsureComponent<TextToSpeechManager>(visionSystem);
            EnsureComponent<OllamaClient>(visionSystem);
            EnsureComponent<VisionLanguageManager>(visionSystem);
            EnsureComponent<XREALVisionController>(visionSystem);
            
            return visionSystem;
        }
        
        private void SetupVisionUI(GameObject visionSystem)
        {
            // Check if UI already exists
            var existingCanvas = FindObjectOfType<Canvas>();
            if (existingCanvas != null && existingCanvas.name.Contains("Vision"))
            {
                Debug.Log("[XREALVisionSceneSetup] Vision UI already exists");
                return;
            }
            
            GameObject uiCanvas;
            
            if (uiCanvasPrefab != null)
            {
                uiCanvas = Instantiate(uiCanvasPrefab);
            }
            else
            {
                uiCanvas = CreateVisionUI();
            }
            
            uiCanvas.name = "XREALVisionUI";
            
            // Link UI to controller
            var controller = visionSystem.GetComponent<XREALVisionController>();
            if (controller != null)
            {
                LinkUIToController(uiCanvas, controller);
            }
        }
        
        private GameObject CreateVisionUI()
        {
            // Create main canvas
            GameObject canvasGO = new GameObject("XREALVisionCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            
            // Create status panel
            GameObject statusPanel = CreateUIPanel(canvasGO, "StatusPanel", new Vector2(0, 1), new Vector2(0, 1), new Vector2(400, 200));
            CreateUIText(statusPanel, "StatusText", "XREAL Vision System", 24, Color.white);
            CreateUIText(statusPanel, "CommandText", "", 18, Color.yellow);
            CreateUIText(statusPanel, "ResponseText", "", 16, Color.green);
            
            // Create control panel
            GameObject controlPanel = CreateUIPanel(canvasGO, "ControlPanel", new Vector2(1, 0), new Vector2(1, 0), new Vector2(200, 300));
            CreateUIButton(controlPanel, "CaptureButton", "Capture", new Vector2(0, 50));
            CreateUIButton(controlPanel, "SettingsButton", "Settings", new Vector2(0, 0));
            CreateUISlider(controlPanel, "ProgressSlider", new Vector2(0, -50));
            
            // Create settings panel (initially hidden)
            GameObject settingsPanel = CreateUIPanel(canvasGO, "SettingsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400, 400));
            settingsPanel.SetActive(false);
            
            CreateUIDropdown(settingsPanel, "ModelDropdown", new Vector2(0, 100), new string[] { "llava:7b", "llava:13b", "moondream:latest", "llama3.2-vision:11b" });
            CreateUIDropdown(settingsPanel, "ModeDropdown", new Vector2(0, 50), new string[] { "Image + Text", "Image Only", "Text Only" });
            CreateUISlider(settingsPanel, "TemperatureSlider", new Vector2(0, 0));
            CreateUIToggle(settingsPanel, "ContinuousModeToggle", "Continuous Mode", new Vector2(0, -50));
            
            // Create feedback indicators
            GameObject indicatorsPanel = CreateUIPanel(canvasGO, "FeedbackIndicators", new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), new Vector2(300, 100));
            CreateUIImage(indicatorsPanel, "ListeningIndicator", Color.blue, new Vector2(-100, 0), new Vector2(80, 80));
            CreateUIImage(indicatorsPanel, "ProcessingIndicator", Color.yellow, new Vector2(0, 0), new Vector2(80, 80));
            CreateUIImage(indicatorsPanel, "ErrorIndicator", Color.red, new Vector2(100, 0), new Vector2(80, 80));
            
            // Create camera feed display
            GameObject cameraPanel = CreateUIPanel(canvasGO, "CameraFeed", new Vector2(0, 0), new Vector2(0, 0), new Vector2(200, 150));
            CreateUIImage(cameraPanel, "CameraFeedImage", Color.gray, Vector2.zero, new Vector2(200, 150));
            
            return canvasGO;
        }
        
        private void ConfigureVisionComponents(GameObject visionSystem)
        {
            // Configure OllamaClient
            var ollamaClient = visionSystem.GetComponent<OllamaClient>();
            if (ollamaClient != null)
            {
                SetPrivateField(ollamaClient, "serverIP", defaultServerIP);
                SetPrivateField(ollamaClient, "serverPort", defaultServerPort);
                SetPrivateField(ollamaClient, "defaultModel", defaultModel);
            }
            
            // Configure CameraManager
            var cameraManager = visionSystem.GetComponent<CameraManager>();
            if (cameraManager != null)
            {
                SetPrivateField(cameraManager, "enableContinuousCapture", true);
                SetPrivateField(cameraManager, "captureInterval", 2.0f);
                SetPrivateField(cameraManager, "resolutionLevel", CameraManager.ResolutionLevel.Middle);
            }
            
            // Configure VisionLanguageManager
            var visionManager = visionSystem.GetComponent<VisionLanguageManager>();
            if (visionManager != null)
            {
                SetPrivateField(visionManager, "currentMode", VisionLanguageManager.VisionMode.ImageAndText);
                SetPrivateField(visionManager, "enableVoiceActivation", true);
                SetPrivateField(visionManager, "enableDebugLogging", true);
            }
        }
        
        private void SetupTestingControls(GameObject visionSystem)
        {
            // Add testing component
            var testingControls = visionSystem.GetComponent<VisionTestingControls>();
            if (testingControls == null)
            {
                testingControls = visionSystem.AddComponent<VisionTestingControls>();
            }
        }
        
        private void LinkUIToController(GameObject uiCanvas, XREALVisionController controller)
        {
            // Find and link UI components to controller
            var mainCanvas = uiCanvas.GetComponent<Canvas>();
            SetPrivateField(controller, "mainCanvas", mainCanvas);
            
            // Link text components
            LinkUIComponent<TextMeshProUGUI>(uiCanvas, "StatusText", controller, "statusText");
            LinkUIComponent<TextMeshProUGUI>(uiCanvas, "CommandText", controller, "commandText");
            LinkUIComponent<TextMeshProUGUI>(uiCanvas, "ResponseText", controller, "responseText");
            
            // Link buttons
            LinkUIComponent<Button>(uiCanvas, "CaptureButton", controller, "captureButton");
            LinkUIComponent<Button>(uiCanvas, "SettingsButton", controller, "settingsButton");
            
            // Link other components
            LinkUIComponent<Slider>(uiCanvas, "ProgressSlider", controller, "progressSlider");
            LinkUIComponent<Dropdown>(uiCanvas, "ModelDropdown", controller, "modelDropdown");
            LinkUIComponent<Dropdown>(uiCanvas, "ModeDropdown", controller, "modeDropdown");
            LinkUIComponent<Toggle>(uiCanvas, "ContinuousModeToggle", controller, "continuousModeToggle");
            
            // Link indicators
            var settingsPanel = FindChildByName(uiCanvas, "SettingsPanel");
            if (settingsPanel != null)
                SetPrivateField(controller, "settingsPanel", settingsPanel);
            
            var listeningIndicator = FindChildByName(uiCanvas, "ListeningIndicator");
            if (listeningIndicator != null)
                SetPrivateField(controller, "listeningIndicator", listeningIndicator);
            
            var processingIndicator = FindChildByName(uiCanvas, "ProcessingIndicator");
            if (processingIndicator != null)
                SetPrivateField(controller, "processingIndicator", processingIndicator);
            
            var errorIndicator = FindChildByName(uiCanvas, "ErrorIndicator");
            if (errorIndicator != null)
                SetPrivateField(controller, "errorIndicator", errorIndicator);
        }
        
        // Utility methods for creating UI elements
        private GameObject CreateUIPanel(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent.transform, false);
            
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = Vector2.zero;
            
            return panel;
        }
        
        private GameObject CreateUIText(GameObject parent, string name, string text, int fontSize, Color color)
        {
            GameObject textGO = new GameObject(name);
            textGO.transform.SetParent(parent.transform, false);
            
            TextMeshProUGUI textComp = textGO.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.fontSize = fontSize;
            textComp.color = color;
            textComp.alignment = TextAlignmentOptions.Center;
            
            RectTransform rect = textGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            
            return textGO;
        }
        
        private GameObject CreateUIButton(GameObject parent, string name, string text, Vector2 position)
        {
            GameObject buttonGO = new GameObject(name);
            buttonGO.transform.SetParent(parent.transform, false);
            
            Image image = buttonGO.AddComponent<Image>();
            image.color = new Color(0.2f, 0.3f, 0.5f, 0.8f);
            
            Button button = buttonGO.AddComponent<Button>();
            
            RectTransform rect = buttonGO.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150, 40);
            rect.anchoredPosition = position;
            
            // Add text child
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform, false);
            
            TextMeshProUGUI textComp = textGO.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.fontSize = 16;
            textComp.color = Color.white;
            textComp.alignment = TextAlignmentOptions.Center;
            
            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;
            
            return buttonGO;
        }
        
        private GameObject CreateUISlider(GameObject parent, string name, Vector2 position)
        {
            GameObject sliderGO = new GameObject(name);
            sliderGO.transform.SetParent(parent.transform, false);
            
            Slider slider = sliderGO.AddComponent<Slider>();
            
            RectTransform rect = sliderGO.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150, 20);
            rect.anchoredPosition = position;
            
            return sliderGO;
        }
        
        private GameObject CreateUIDropdown(GameObject parent, string name, Vector2 position, string[] options)
        {
            GameObject dropdownGO = new GameObject(name);
            dropdownGO.transform.SetParent(parent.transform, false);
            
            Image image = dropdownGO.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            Dropdown dropdown = dropdownGO.AddComponent<Dropdown>();
            dropdown.ClearOptions();
            dropdown.AddOptions(new System.Collections.Generic.List<string>(options));
            
            RectTransform rect = dropdownGO.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 30);
            rect.anchoredPosition = position;
            
            return dropdownGO;
        }
        
        private GameObject CreateUIToggle(GameObject parent, string name, string labelText, Vector2 position)
        {
            GameObject toggleGO = new GameObject(name);
            toggleGO.transform.SetParent(parent.transform, false);
            
            Toggle toggle = toggleGO.AddComponent<Toggle>();
            
            RectTransform rect = toggleGO.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 30);
            rect.anchoredPosition = position;
            
            return toggleGO;
        }
        
        private GameObject CreateUIImage(GameObject parent, string name, Color color, Vector2 position, Vector2 size)
        {
            GameObject imageGO = new GameObject(name);
            imageGO.transform.SetParent(parent.transform, false);
            
            Image image = imageGO.AddComponent<Image>();
            image.color = color;
            
            RectTransform rect = imageGO.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            
            imageGO.SetActive(false); // Hide indicators initially
            
            return imageGO;
        }
        
        // Utility methods
        private T EnsureComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null)
            {
                component = go.AddComponent<T>();
            }
            return component;
        }
        
        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
            }
            else
            {
                Debug.LogWarning($"[XREALVisionSceneSetup] Field '{fieldName}' not found on {obj.GetType().Name}");
            }
        }
        
        private void LinkUIComponent<T>(GameObject uiRoot, string componentName, object target, string fieldName) where T : Component
        {
            var component = FindChildByName(uiRoot, componentName)?.GetComponent<T>();
            if (component != null)
            {
                SetPrivateField(target, fieldName, component);
            }
        }
        
        private GameObject FindChildByName(GameObject parent, string name)
        {
            Transform[] transforms = parent.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in transforms)
            {
                if (t.name == name)
                    return t.gameObject;
            }
            return null;
        }
    }
    
    /// <summary>
    /// Testing controls for the vision system
    /// </summary>
    public class VisionTestingControls : MonoBehaviour
    {
        [Header("Test Commands")]
        [SerializeField] private string[] testCommands = {
            "What do you see?",
            "Describe this image",
            "What colors are visible?",
            "Tell me about this object"
        };
        
        private VisionLanguageManager visionManager;
        
        private void Start()
        {
            visionManager = GetComponent<VisionLanguageManager>();
        }
        
        private void Update()
        {
            // Test commands using number keys
            for (int i = 0; i < testCommands.Length && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    if (visionManager != null)
                    {
                        visionManager.ProcessTextCommand(testCommands[i]);
                        Debug.Log($"[VisionTestingControls] Triggered test command: {testCommands[i]}");
                    }
                }
            }
        }
    }
}
