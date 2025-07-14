using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Scene setup manager that configures the XREAL environment and initializes all systems.
    /// This should be placed in the main scene to automatically setup the AI companion.
    /// </summary>
    public class XREALSceneSetup : MonoBehaviour
    {
        [Header("Scene Configuration")]
        [SerializeField] private bool autoInitialize = true;
        [SerializeField] private Transform avatarSpawnPoint;
        [SerializeField] private float avatarDistanceFromUser = 2.0f;
        [SerializeField] private float avatarHeight = 0.0f; // Relative to user eye level
        
        [Header("XREAL AR Setup")]
        [SerializeField] private ARSession arSession;
        [SerializeField] private ARSessionOrigin arSessionOrigin;
        [SerializeField] private Camera arCamera;
        
        [Header("System Prefabs")]
        [SerializeField] private GameObject companionAIPrefab;
        [SerializeField] private GameObject uiCanvasPrefab;
        
        // System instances
        private CompanionAI companionAIInstance;
        private UI.MainUIController uiController;
        
        private void Awake()
        {
            ConfigureXREALEnvironment();
        }
        
        private void Start()
        {
            if (autoInitialize)
            {
                InitializeAllSystems();
            }
        }
        
        private void ConfigureXREALEnvironment()
        {
            Debug.Log("[XREALSceneSetup] Configuring XREAL AR environment...");
            
            // Ensure we have AR components
            if (arSession == null)
                arSession = FindObjectOfType<ARSession>();
            
            if (arSessionOrigin == null)
                arSessionOrigin = FindObjectOfType<ARSessionOrigin>();
            
            if (arCamera == null && arSessionOrigin != null)
                arCamera = arSessionOrigin.camera;
            
            // Create AR components if they don't exist
            if (arSession == null)
            {
                var sessionGO = new GameObject("AR Session");
                arSession = sessionGO.AddComponent<ARSession>();
            }
            
            if (arSessionOrigin == null)
            {
                var originGO = new GameObject("AR Session Origin");
                arSessionOrigin = originGO.AddComponent<ARSessionOrigin>();
                
                // Setup AR camera
                var cameraGO = new GameObject("AR Camera");
                cameraGO.transform.SetParent(originGO.transform);
                arCamera = cameraGO.AddComponent<Camera>();
                
                // Configure AR camera
                arCamera.clearFlags = CameraClearFlags.SolidColor;
                arCamera.backgroundColor = Color.black;
                arCamera.nearClipPlane = 0.1f;
                arCamera.farClipPlane = 20f;
                
                // Add AR camera manager
                var arCameraManager = cameraGO.AddComponent<ARCameraManager>();
                arSessionOrigin.camera = arCamera;
                
                // Add AR camera background
                var arCameraBackground = cameraGO.AddComponent<ARCameraBackground>();
            }
            
            // Setup avatar spawn point relative to camera
            if (avatarSpawnPoint == null)
            {
                var spawnPointGO = new GameObject("AvatarSpawnPoint");
                avatarSpawnPoint = spawnPointGO.transform;
            }
            
            PositionAvatarSpawnPoint();
        }
        
        private void PositionAvatarSpawnPoint()
        {
            if (avatarSpawnPoint != null && arCamera != null)
            {
                // Position avatar in front of user
                Vector3 cameraPosition = arCamera.transform.position;
                Vector3 cameraForward = arCamera.transform.forward;
                
                Vector3 avatarPosition = cameraPosition + cameraForward * avatarDistanceFromUser;
                avatarPosition.y = cameraPosition.y + avatarHeight;
                
                avatarSpawnPoint.position = avatarPosition;
                avatarSpawnPoint.LookAt(new Vector3(cameraPosition.x, avatarPosition.y, cameraPosition.z));
            }
        }
        
        private void InitializeAllSystems()
        {
            Debug.Log("[XREALSceneSetup] Initializing all companion AI systems...");
            
            // Create main CompanionAI system
            if (companionAIPrefab != null)
            {
                var companionGO = Instantiate(companionAIPrefab);
                companionGO.name = "CompanionAI_System";
                companionAIInstance = companionGO.GetComponent<CompanionAI>();
            }
            else
            {
                // Create from scratch if no prefab
                var companionGO = new GameObject("CompanionAI_System");
                companionAIInstance = companionGO.AddComponent<CompanionAI>();
                
                // Add all required components
                companionGO.AddComponent<AudioManager>();
                companionGO.AddComponent<CameraManager>();
                companionGO.AddComponent<OllamaClient>();
                companionGO.AddComponent<NetworkDiscovery>();
                companionGO.AddComponent<AvatarManager>();
            }
            
            // Create UI system
            if (uiCanvasPrefab != null)
            {
                var uiGO = Instantiate(uiCanvasPrefab);
                uiGO.name = "CompanionAI_UI";
                uiController = uiGO.GetComponent<UI.MainUIController>();
            }
            else
            {
                CreateBasicUI();
            }
            
            // Position avatar manager at spawn point
            if (companionAIInstance != null && avatarSpawnPoint != null)
            {
                var avatarManager = companionAIInstance.GetComponent<AvatarManager>();
                if (avatarManager != null)
                {
                    avatarManager.transform.position = avatarSpawnPoint.position;
                    avatarManager.transform.rotation = avatarSpawnPoint.rotation;
                }
            }
        }
        
        private void CreateBasicUI()
        {
            Debug.Log("[XREALSceneSetup] Creating basic UI system...");
            
            // Create main canvas
            var canvasGO = new GameObject("CompanionAI_UI");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            
            // Add canvas scaler
            var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            // Add graphic raycaster for UI interaction
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            // Create basic UI elements
            CreateBasicUIElements(canvasGO.transform);
            
            // Add UI controller
            uiController = canvasGO.AddComponent<UI.MainUIController>();
        }
        
        private void CreateBasicUIElements(Transform canvasTransform)
        {
            // Create status panel
            var statusPanel = CreateUIPanel("StatusPanel", canvasTransform);
            statusPanel.anchorMin = new Vector2(0, 0.8f);
            statusPanel.anchorMax = new Vector2(1, 1);
            statusPanel.offsetMin = Vector2.zero;
            statusPanel.offsetMax = Vector2.zero;
            
            // Create status text
            CreateUIText("StatusText", statusPanel.transform, "Initializing Companion AI...");
            
            // Create control panel
            var controlPanel = CreateUIPanel("ControlPanel", canvasTransform);
            controlPanel.anchorMin = new Vector2(0, 0);
            controlPanel.anchorMax = new Vector2(1, 0.2f);
            controlPanel.offsetMin = Vector2.zero;
            controlPanel.offsetMax = Vector2.zero;
            
            // Create voice button
            CreateUIButton("VoiceButton", controlPanel.transform, "Start Voice Mode");
        }
        
        private RectTransform CreateUIPanel(string name, Transform parent)
        {
            var panelGO = new GameObject(name);
            panelGO.transform.SetParent(parent);
            
            var rectTransform = panelGO.AddComponent<RectTransform>();
            var image = panelGO.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0, 0, 0, 0.3f);
            
            return rectTransform;
        }
        
        private UnityEngine.UI.Text CreateUIText(string name, Transform parent, string text)
        {
            var textGO = new GameObject(name);
            textGO.transform.SetParent(parent);
            
            var rectTransform = textGO.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            var textComponent = textGO.AddComponent<UnityEngine.UI.Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComponent.fontSize = 24;
            textComponent.color = Color.white;
            textComponent.alignment = TextAnchor.MiddleCenter;
            
            return textComponent;
        }
        
        private UnityEngine.UI.Button CreateUIButton(string name, Transform parent, string text)
        {
            var buttonGO = new GameObject(name);
            buttonGO.transform.SetParent(parent);
            
            var rectTransform = buttonGO.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.3f, 0.3f);
            rectTransform.anchorMax = new Vector2(0.7f, 0.7f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            var image = buttonGO.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.2f, 0.4f, 0.8f, 0.8f);
            
            var button = buttonGO.AddComponent<UnityEngine.UI.Button>();
            
            // Create button text
            CreateUIText("Text", buttonGO.transform, text);
            
            return button;
        }
        
        public void ManualInitialize()
        {
            InitializeAllSystems();
        }
        
        public CompanionAI GetCompanionAI()
        {
            return companionAIInstance;
        }
        
        public UI.MainUIController GetUIController()
        {
            return uiController;
        }
        
        private void Update()
        {
            // Update avatar position to stay in front of user
            if (Application.isPlaying && Time.frameCount % 60 == 0) // Update every 60 frames
            {
                PositionAvatarSpawnPoint();
            }
        }
    }
}
