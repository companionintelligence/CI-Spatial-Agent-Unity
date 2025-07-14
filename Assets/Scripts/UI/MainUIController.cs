using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CompanionIntelligence.UI
{
    /// <summary>
    /// Main UI controller for the Companion Intelligence XREAL application.
    /// Manages all user interface elements and system status displays.
    /// </summary>
    public class MainUIController : MonoBehaviour
    {
        [Header("Status Indicators")]
        [SerializeField] private GameObject connectionStatusPanel;
        [SerializeField] private TextMeshProUGUI connectionStatusText;
        [SerializeField] private Image connectionStatusIcon;
        [SerializeField] private GameObject recordingIndicator;
        [SerializeField] private GameObject cameraIndicator;
        
        [Header("Control Buttons")]
        [SerializeField] private Button voiceModeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private TextMeshProUGUI voiceModeButtonText;
        
        [Header("Information Display")]
        [SerializeField] private TextMeshProUGUI systemStatusText;
        [SerializeField] private TextMeshProUGUI lastResponseText;
        [SerializeField] private ScrollRect conversationScrollView;
        [SerializeField] private Transform conversationContent;
        [SerializeField] private GameObject conversationEntryPrefab;
        
        [Header("Settings Panel")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle lifeLoggingToggle;
        [SerializeField] private Toggle continuousAnalysisToggle;
        [SerializeField] private TMP_InputField serverIPField;
        
        // System references
        private Core.CompanionAI companionAI;
        private Core.AudioManager audioManager;
        private Core.CameraManager cameraManager;
        private Core.OllamaClient ollamaClient;
        private Core.NetworkDiscovery networkDiscovery;
        private Core.AvatarManager avatarManager;
        
        // UI state
        private bool isVoiceModeActive = false;
        private bool isSettingsOpen = false;
        
        private void Start()
        {
            InitializeUI();
            FindSystemComponents();
            SetupEventListeners();
        }
        
        private void InitializeUI()
        {
            // Initialize UI elements
            if (connectionStatusText != null)
                connectionStatusText.text = "Initializing...";
                
            if (systemStatusText != null)
                systemStatusText.text = "System starting up...";
                
            if (voiceModeButtonText != null)
                voiceModeButtonText.text = "Start Voice Mode";
                
            // Hide indicators initially
            if (recordingIndicator != null)
                recordingIndicator.SetActive(false);
                
            if (cameraIndicator != null)
                cameraIndicator.SetActive(false);
                
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
        }
        
        private void FindSystemComponents()
        {
            // Find the main CompanionAI component
            companionAI = FindObjectOfType<Core.CompanionAI>();
            if (companionAI != null)
            {
                audioManager = companionAI.GetComponent<Core.AudioManager>();
                cameraManager = companionAI.GetComponent<Core.CameraManager>();
                ollamaClient = companionAI.GetComponent<Core.OllamaClient>();
                networkDiscovery = companionAI.GetComponent<Core.NetworkDiscovery>();
                avatarManager = companionAI.GetComponent<Core.AvatarManager>();
            }
        }
        
        private void SetupEventListeners()
        {
            // Button events
            if (voiceModeButton != null)
                voiceModeButton.onClick.AddListener(ToggleVoiceMode);
                
            if (settingsButton != null)
                settingsButton.onClick.AddListener(ToggleSettings);
                
            if (exitButton != null)
                exitButton.onClick.AddListener(ExitApplication);
            
            // Settings events
            if (volumeSlider != null)
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
                
            if (lifeLoggingToggle != null)
                lifeLoggingToggle.onValueChanged.AddListener(OnLifeLoggingToggled);
                
            if (continuousAnalysisToggle != null)
                continuousAnalysisToggle.onValueChanged.AddListener(OnContinuousAnalysisToggled);
            
            // System events
            if (ollamaClient != null)
            {
                ollamaClient.OnConnectionStatusChanged += OnConnectionStatusChanged;
                ollamaClient.OnResponseReceived += OnAIResponseReceived;
            }
            
            if (audioManager != null)
            {
                audioManager.OnVolumeChanged += OnMicVolumeChanged;
            }
        }
        
        private void Update()
        {
            UpdateSystemStatus();
        }
        
        private void UpdateSystemStatus()
        {
            if (systemStatusText == null || companionAI == null)
                return;
            
            string status = "";
            
            if (!companionAI.IsSystemReady())
            {
                status = "System initializing...";
            }
            else
            {
                var components = new System.Collections.Generic.List<string>();
                
                if (audioManager != null && audioManager.IsInitialized)
                    components.Add("Audio");
                    
                if (cameraManager != null && cameraManager.IsInitialized)
                    components.Add("Camera");
                    
                if (ollamaClient != null && ollamaClient.IsConnected)
                    components.Add("AI");
                    
                if (avatarManager != null && avatarManager.IsLoaded)
                    components.Add("Avatar");
                
                status = $"Ready - Active: {string.Join(", ", components)}";
            }
            
            systemStatusText.text = status;
        }
        
        private void ToggleVoiceMode()
        {
            if (companionAI == null || !companionAI.IsSystemReady())
            {
                Debug.LogWarning("[MainUI] Cannot toggle voice mode - system not ready");
                return;
            }
            
            isVoiceModeActive = !isVoiceModeActive;
            
            if (isVoiceModeActive)
            {
                companionAI.ActivateVoiceMode();
                voiceModeButtonText.text = "Stop Voice Mode";
                
                if (avatarManager != null)
                    avatarManager.StartListening();
            }
            else
            {
                companionAI.DeactivateVoiceMode();
                voiceModeButtonText.text = "Start Voice Mode";
                
                if (avatarManager != null)
                    avatarManager.StopListening();
            }
        }
        
        private void ToggleSettings()
        {
            isSettingsOpen = !isSettingsOpen;
            
            if (settingsPanel != null)
                settingsPanel.SetActive(isSettingsOpen);
            
            if (isSettingsOpen)
                LoadCurrentSettings();
        }
        
        private void LoadCurrentSettings()
        {
            // Load current system settings into UI
            if (volumeSlider != null && audioManager != null)
            {
                // TODO: Get current volume from audio manager
                volumeSlider.value = 0.8f; // Default value
            }
            
            if (serverIPField != null && networkDiscovery != null)
            {
                var selectedServer = networkDiscovery.GetSelectedServer();
                if (selectedServer != null)
                {
                    serverIPField.text = selectedServer.ipAddress;
                }
            }
        }
        
        private void OnVolumeChanged(float volume)
        {
            if (audioManager != null)
            {
                audioManager.SetVolume(volume);
            }
        }
        
        private void OnLifeLoggingToggled(bool enabled)
        {
            // TODO: Toggle life logging functionality
            Debug.Log($"[MainUI] Life logging toggled: {enabled}");
        }
        
        private void OnContinuousAnalysisToggled(bool enabled)
        {
            // TODO: Toggle continuous analysis
            Debug.Log($"[MainUI] Continuous analysis toggled: {enabled}");
        }
        
        private void OnConnectionStatusChanged(bool isConnected)
        {
            if (connectionStatusText != null)
            {
                connectionStatusText.text = isConnected ? "Connected" : "Disconnected";
            }
            
            if (connectionStatusIcon != null)
            {
                connectionStatusIcon.color = isConnected ? Color.green : Color.red;
            }
        }
        
        private void OnAIResponseReceived(string response)
        {
            if (lastResponseText != null)
            {
                lastResponseText.text = response;
            }
            
            // Add to conversation history
            AddConversationEntry("AI", response);
            
            // Trigger avatar speaking animation
            if (avatarManager != null)
            {
                avatarManager.StartSpeaking();
            }
        }
        
        private void OnMicVolumeChanged(float volume)
        {
            // Visual feedback for microphone input level
            // TODO: Implement volume meter UI
        }
        
        private void AddConversationEntry(string speaker, string message)
        {
            if (conversationContent == null || conversationEntryPrefab == null)
                return;
            
            var entry = Instantiate(conversationEntryPrefab, conversationContent);
            var entryText = entry.GetComponentInChildren<TextMeshProUGUI>();
            
            if (entryText != null)
            {
                entryText.text = $"{speaker}: {message}";
            }
            
            // Scroll to bottom
            if (conversationScrollView != null)
            {
                Canvas.ForceUpdateCanvases();
                conversationScrollView.verticalNormalizedPosition = 0f;
            }
        }
        
        private void ExitApplication()
        {
            Debug.Log("[MainUI] Exiting application...");
            
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
        
        // Public methods for external systems to update UI
        public void ShowRecordingIndicator(bool show)
        {
            if (recordingIndicator != null)
                recordingIndicator.SetActive(show);
        }
        
        public void ShowCameraIndicator(bool show)
        {
            if (cameraIndicator != null)
                cameraIndicator.SetActive(show);
        }
        
        public void UpdateConnectionStatus(string status, Color color)
        {
            if (connectionStatusText != null)
                connectionStatusText.text = status;
                
            if (connectionStatusIcon != null)
                connectionStatusIcon.color = color;
        }
        
        public void ShowMessage(string message)
        {
            if (systemStatusText != null)
                systemStatusText.text = message;
        }
        
        private void OnDestroy()
        {
            // Clean up event listeners
            if (ollamaClient != null)
            {
                ollamaClient.OnConnectionStatusChanged -= OnConnectionStatusChanged;
                ollamaClient.OnResponseReceived -= OnAIResponseReceived;
            }
            
            if (audioManager != null)
            {
                audioManager.OnVolumeChanged -= OnMicVolumeChanged;
            }
        }
    }
}
