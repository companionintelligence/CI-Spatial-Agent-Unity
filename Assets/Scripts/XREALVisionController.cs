using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// XREAL Vision Language Scene Controller
    /// Manages the UI and interaction flow for the vision-language system.
    /// Based on Quest Camera Kit's VoiceCommandHandler but adapted for XREAL + Ollama
    /// </summary>
    public class XREALVisionController : MonoBehaviour
    {
        [Header("Core Systems")]
        [SerializeField] private VisionLanguageManager visionManager;
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private OllamaClient ollamaClient;
        
        [Header("UI Components")]
        [SerializeField] private Canvas mainCanvas;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI responseText;
        [SerializeField] private TextMeshProUGUI commandText;
        [SerializeField] private Button captureButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Slider progressSlider;
        
        [Header("Settings Panel")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Dropdown modelDropdown;
        [SerializeField] private Dropdown modeDropdown;
        [SerializeField] private Slider temperatureSlider;
        [SerializeField] private TextMeshProUGUI temperatureLabel;
        [SerializeField] private Toggle continuousModeToggle;
        
        [Header("Visual Feedback")]
        [SerializeField] private GameObject listeningIndicator;
        [SerializeField] private GameObject processingIndicator;
        [SerializeField] private GameObject errorIndicator;
        [SerializeField] private Image cameraFeedImage;
        
        [Header("Audio Feedback")]
        [SerializeField] private AudioSource feedbackAudioSource;
        [SerializeField] private AudioClip listeningSound;
        [SerializeField] private AudioClip successSound;
        [SerializeField] private AudioClip errorSound;
        
        [Header("Configuration")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool enableUIFeedback = true;
        [SerializeField] private float statusDisplayDuration = 3f;
        
        // Available models for Ollama (XREAL optimized)
        private readonly string[] availableModels = {
            "llava:7b",
            "llava:13b", 
            "moondream:latest",
            "llama3.2-vision:11b"
        };
        
        // State tracking
        private bool isInitialized = false;
        private Coroutine statusCoroutine;
        
        private void Start()
        {
            StartCoroutine(InitializeController());
        }
        
        private IEnumerator InitializeController()
        {
            UpdateStatus("Initializing XREAL Vision System...", Color.yellow);
            
            // Wait for core systems to initialize
            while (!IsSystemReady())
            {
                yield return new WaitForSeconds(0.5f);
            }
            
            // Setup UI components
            SetupUI();
            
            // Subscribe to events
            SetupEventSubscriptions();
            
            isInitialized = true;
            UpdateStatus("XREAL Vision System Ready", Color.green);
            
            // Auto-hide status after a few seconds
            yield return new WaitForSeconds(2f);
            if (statusText != null)
                statusText.text = "";
        }
        
        private bool IsSystemReady()
        {
            return visionManager != null &&
                   cameraManager != null && cameraManager.IsInitialized &&
                   ollamaClient != null && ollamaClient.IsInitialized;
        }
        
        private void SetupUI()
        {
            // Setup model dropdown
            if (modelDropdown != null)
            {
                modelDropdown.ClearOptions();
                modelDropdown.AddOptions(new System.Collections.Generic.List<string>(availableModels));
                modelDropdown.onValueChanged.AddListener(OnModelChanged);
                modelDropdown.value = 0; // Default to first model
            }
            
            // Setup vision mode dropdown
            if (modeDropdown != null)
            {
                modeDropdown.ClearOptions();
                var modeOptions = new System.Collections.Generic.List<string>
                {
                    "Image + Text",
                    "Image Only", 
                    "Text Only"
                };
                modeDropdown.AddOptions(modeOptions);
                modeDropdown.onValueChanged.AddListener(OnModeChanged);
                modeDropdown.value = 0; // Default to Image + Text
            }
            
            // Setup temperature slider
            if (temperatureSlider != null)
            {
                temperatureSlider.minValue = 0.1f;
                temperatureSlider.maxValue = 2.0f;
                temperatureSlider.value = 0.7f;
                temperatureSlider.onValueChanged.AddListener(OnTemperatureChanged);
                UpdateTemperatureLabel(0.7f);
            }
            
            // Setup buttons
            if (captureButton != null)
                captureButton.onClick.AddListener(OnCaptureButtonClicked);
            
            if (settingsButton != null)
                settingsButton.onClick.AddListener(ToggleSettingsPanel);
            
            // Setup continuous mode toggle
            if (continuousModeToggle != null)
                continuousModeToggle.onValueChanged.AddListener(OnContinuousModeToggled);
            
            // Hide settings panel initially
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
            
            // Hide indicators initially
            HideAllIndicators();
        }
        
        private void SetupEventSubscriptions()
        {
            if (visionManager != null)
            {
                visionManager.OnVoiceCommandReceived.AddListener(OnVoiceCommandReceived);
                visionManager.OnVisionResponseReceived.AddListener(OnVisionResponseReceived);
                visionManager.OnProcessingProgress.AddListener(OnProcessingProgress);
                visionManager.OnErrorOccurred.AddListener(OnErrorOccurred);
            }
            
            if (cameraManager != null)
            {
                cameraManager.OnFrameTextureReady += OnCameraFrameReady;
            }
        }
        
        private void OnVoiceCommandReceived(string command)
        {
            if (enableUIFeedback)
            {
                UpdateCommandText($"Voice: \"{command}\"");
                ShowListeningIndicator();
                PlayFeedbackSound(listeningSound);
            }
        }
        
        private void OnVisionResponseReceived(string response)
        {
            if (enableUIFeedback)
            {
                UpdateResponseText(response);
                HideAllIndicators();
                PlayFeedbackSound(successSound);
                UpdateStatus("Response received", Color.green);
            }
        }
        
        private void OnProcessingProgress(float progress)
        {
            if (progressSlider != null)
            {
                progressSlider.value = progress;
            }
            
            if (!processingIndicator.activeInHierarchy)
            {
                ShowProcessingIndicator();
            }
        }
        
        private void OnErrorOccurred(string error)
        {
            UpdateStatus($"Error: {error}", Color.red);
            ShowErrorIndicator();
            PlayFeedbackSound(errorSound);
            
            if (showDebugInfo)
                Debug.LogError($"[XREALVisionController] {error}");
        }
        
        private void OnCameraFrameReady(Texture2D frameTexture)
        {
            if (cameraFeedImage != null && frameTexture != null)
            {
                // Convert texture to sprite for UI display
                var sprite = Sprite.Create(frameTexture, 
                    new Rect(0, 0, frameTexture.width, frameTexture.height), 
                    new Vector2(0.5f, 0.5f));
                cameraFeedImage.sprite = sprite;
            }
        }
        
        // UI Event Handlers
        private void OnModelChanged(int modelIndex)
        {
            if (modelIndex >= 0 && modelIndex < availableModels.Length)
            {
                string selectedModel = availableModels[modelIndex];
                
                if (visionManager != null)
                    visionManager.SetModel(selectedModel);
                
                UpdateStatus($"Model changed to: {selectedModel}", Color.blue);
            }
        }
        
        private void OnModeChanged(int modeIndex)
        {
            VisionLanguageManager.VisionMode mode = (VisionLanguageManager.VisionMode)modeIndex;
            
            if (visionManager != null)
                visionManager.SetVisionMode(mode);
            
            UpdateStatus($"Mode changed to: {mode}", Color.blue);
        }
        
        private void OnTemperatureChanged(float temperature)
        {
            UpdateTemperatureLabel(temperature);
            // TODO: Update Ollama client temperature setting
        }
        
        private void OnContinuousModeToggled(bool enabled)
        {
            if (visionManager != null)
            {
                if (enabled)
                    visionManager.StartContinuousMode();
                else
                    visionManager.StopContinuousMode();
            }
            
            UpdateStatus($"Continuous mode: {(enabled ? "ON" : "OFF")}", Color.blue);
        }
        
        private void OnCaptureButtonClicked()
        {
            if (visionManager != null)
            {
                visionManager.TriggerManualCapture();
                UpdateStatus("Manual capture triggered", Color.yellow);
            }
        }
        
        private void ToggleSettingsPanel()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(!settingsPanel.activeInHierarchy);
            }
        }
        
        // UI Update Methods
        private void UpdateStatus(string message, Color color)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = color;
            }
            
            // Auto-clear status after duration
            if (statusCoroutine != null)
                StopCoroutine(statusCoroutine);
            
            statusCoroutine = StartCoroutine(ClearStatusAfterDelay());
        }
        
        private IEnumerator ClearStatusAfterDelay()
        {
            yield return new WaitForSeconds(statusDisplayDuration);
            
            if (statusText != null)
                statusText.text = "";
        }
        
        private void UpdateCommandText(string command)
        {
            if (commandText != null)
            {
                commandText.text = command;
            }
        }
        
        private void UpdateResponseText(string response)
        {
            if (responseText != null)
            {
                responseText.text = response;
            }
        }
        
        private void UpdateTemperatureLabel(float temperature)
        {
            if (temperatureLabel != null)
            {
                temperatureLabel.text = $"Temperature: {temperature:F1}";
            }
        }
        
        // Visual Feedback Methods
        private void ShowListeningIndicator()
        {
            HideAllIndicators();
            if (listeningIndicator != null)
                listeningIndicator.SetActive(true);
        }
        
        private void ShowProcessingIndicator()
        {
            HideAllIndicators();
            if (processingIndicator != null)
                processingIndicator.SetActive(true);
        }
        
        private void ShowErrorIndicator()
        {
            HideAllIndicators();
            if (errorIndicator != null)
                errorIndicator.SetActive(true);
            
            // Auto-hide error indicator
            StartCoroutine(HideErrorIndicatorAfterDelay());
        }
        
        private IEnumerator HideErrorIndicatorAfterDelay()
        {
            yield return new WaitForSeconds(3f);
            HideAllIndicators();
        }
        
        private void HideAllIndicators()
        {
            if (listeningIndicator != null)
                listeningIndicator.SetActive(false);
            if (processingIndicator != null)
                processingIndicator.SetActive(false);
            if (errorIndicator != null)
                errorIndicator.SetActive(false);
        }
        
        private void PlayFeedbackSound(AudioClip clip)
        {
            if (feedbackAudioSource != null && clip != null)
            {
                feedbackAudioSource.PlayOneShot(clip);
            }
        }
        
        // Public API for external control
        public void ProcessTextCommand(string command)
        {
            if (visionManager != null && !string.IsNullOrWhiteSpace(command))
            {
                visionManager.ProcessTextCommand(command);
                UpdateCommandText($"Manual: \"{command}\"");
            }
        }
        
        public void SetSystemEnabled(bool enabled)
        {
            if (mainCanvas != null)
                mainCanvas.enabled = enabled;
        }
        
        // Debug and Testing
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void Update()
        {
            if (!isInitialized)
                return;
            
            // Debug keyboard shortcuts
            if (Input.GetKeyDown(KeyCode.C))
                OnCaptureButtonClicked();
            
            if (Input.GetKeyDown(KeyCode.S))
                ToggleSettingsPanel();
            
            if (Input.GetKeyDown(KeyCode.T))
                ProcessTextCommand("What do you see in this image?");
        }
        
        private void OnDestroy()
        {
            // Cleanup event subscriptions
            if (visionManager != null)
            {
                visionManager.OnVoiceCommandReceived.RemoveListener(OnVoiceCommandReceived);
                visionManager.OnVisionResponseReceived.RemoveListener(OnVisionResponseReceived);
                visionManager.OnProcessingProgress.RemoveListener(OnProcessingProgress);
                visionManager.OnErrorOccurred.RemoveListener(OnErrorOccurred);
            }
            
            if (cameraManager != null)
            {
                cameraManager.OnFrameTextureReady -= OnCameraFrameReady;
            }
        }
    }
}
