using UnityEngine;
using Unity.XR.XREAL;
using System.Text;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Main controller for the Companion Intelligence AI system.
    /// Orchestrates all subsystems including audio, visual, AI processing, and avatar interaction.
    /// </summary>
    public class CompanionAI : MonoBehaviour
    {
        [Header("Core System References")]
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private AvatarManager avatarManager;
        [SerializeField] private OllamaClient ollamaClient;
        [SerializeField] private NetworkDiscovery networkDiscovery;
        [SerializeField] private SpeechToTextManager sttManager;
        // [SerializeField] private ObjectDetectionManager objectDetectionManager;  // TODO: Fix compilation issue
        // [SerializeField] private TextToSpeechManager ttsManager;  // TODO: Fix compilation issue
        
        [Header("System Configuration")]
        [SerializeField] private bool enableLifeLogging = true;
        [SerializeField] private bool enableContinuousAnalysis = true;
        [SerializeField] private float analysisInterval = 5.0f;
        
        // System status
        private bool isInitialized = false;
        private bool isActive = false;
        
        private void Awake()
        {
            InitializeComponents();
        }
        
        private void Start()
        {
            StartCoroutine(InitializeSystemsSequentially());
        }
        
        private void InitializeComponents()
        {
            // Ensure all required components are present
            if (audioManager == null)
                audioManager = GetComponent<AudioManager>();
            
            if (cameraManager == null)
                cameraManager = GetComponent<CameraManager>();
                
            if (avatarManager == null)
                avatarManager = GetComponent<AvatarManager>();
                
            if (ollamaClient == null)
                ollamaClient = GetComponent<OllamaClient>();
                
            if (networkDiscovery == null)
                networkDiscovery = GetComponent<NetworkDiscovery>();
                
            if (sttManager == null)
                sttManager = GetComponent<SpeechToTextManager>();
                
            // if (ttsManager == null)
            //     ttsManager = GetComponent<TextToSpeechManager>();  // TODO: Fix compilation issue
        }
        
        private System.Collections.IEnumerator InitializeSystemsSequentially()
        {
            Debug.Log("[CompanionAI] Starting system initialization...");
            
            // Step 1: Initialize network discovery and find Ollama server
            yield return StartCoroutine(networkDiscovery.DiscoverOllamaServer());
            
            // Step 2: Initialize audio system
            if (audioManager != null)
            {
                audioManager.Initialize();
                yield return new WaitForSeconds(0.5f);
            }
            
            // Step 3: Initialize camera system
            if (cameraManager != null)
            {
                cameraManager.Initialize();
                yield return new WaitForSeconds(0.5f);
            }
            
            // Step 4: Initialize avatar system
            if (avatarManager != null)
            {
                avatarManager.Initialize();
                yield return new WaitForSeconds(1.0f);
            }
            
            // Step 5: Initialize Speech Processing
            if (sttManager != null)
            {
                Debug.Log("[CompanionAI] Initializing Speech-to-Text...");
                // STT manager auto-initializes, subscribe to events
                sttManager.OnSpeechRecognized += HandleSpeechRecognized;
                sttManager.OnSpeechStarted += HandleSpeechStarted;
                sttManager.OnSpeechEnded += HandleSpeechEnded;
                yield return new WaitForSeconds(0.5f);
            }
            
            // TODO: Enable TTS integration once compilation issue is resolved
            // if (ttsManager != null)
            // {
            //     Debug.Log("[CompanionAI] Initializing Text-to-Speech...");
            //     ttsManager.OnSpeechStarted += HandleTTSStarted;
            //     ttsManager.OnSpeechCompleted += HandleTTSCompleted;
            //     yield return new WaitForSeconds(0.5f);
            // }
            
            // Step 6: Initialize AI client
            if (ollamaClient != null)
            {
                ollamaClient.Initialize();
                yield return new WaitForSeconds(0.5f);
            }
            
            isInitialized = true;
            isActive = true;
            
            Debug.Log("[CompanionAI] System initialization complete!");
            
            // Start continuous analysis if enabled
            if (enableContinuousAnalysis)
            {
                StartCoroutine(ContinuousAnalysisLoop());
            }
        }
        
        private System.Collections.IEnumerator ContinuousAnalysisLoop()
        {
            while (isActive)
            {
                if (enableLifeLogging && cameraManager != null && cameraManager.IsInitialized)
                {
                    // Capture current frame for analysis
                    var frameData = cameraManager.CaptureCurrentFrame();
                    if (frameData != null)
                    {
                        // Send to AI for analysis
                        StartCoroutine(AnalyzeFrame(frameData));
                    }
                }
                
                yield return new WaitForSeconds(analysisInterval);
            }
        }
        
        private System.Collections.IEnumerator AnalyzeFrame(byte[] frameData)
        {
            if (ollamaClient != null && ollamaClient.IsConnected)
            {
                var analysisPrompt = "Analyze this image from my XREAL device. Identify any objects, people, or interesting activities. Provide a brief description of what you see.";
                
                yield return StartCoroutine(ollamaClient.SendMultiModalRequest(analysisPrompt, frameData, (response) =>
                {
                    if (!string.IsNullOrEmpty(response))
                    {
                        Debug.Log($"[CompanionAI] Scene Analysis: {response}");
                        // TODO: Store in life-logging database
                    }
                }));
            }
        }
        
        public void ActivateVoiceMode()
        {
            if (audioManager != null)
            {
                audioManager.StartListening();
            }
            
            if (sttManager != null)
            {
                sttManager.StartContinuousRecognition();
            }
            
            // TODO: Enable once TTS compilation is fixed
            // if (ttsManager != null)
            // {
            //     ttsManager.Speak("Voice mode activated. How can I help you?", true);
            // }
            
            Debug.Log("[CompanionAI] Voice mode activated");
        }
        
        public void DeactivateVoiceMode()
        {
            if (audioManager != null)
            {
                audioManager.StopListening();
            }
            
            if (sttManager != null)
            {
                sttManager.StopContinuousRecognition();
            }
            
            // TODO: Enable once TTS compilation is fixed
            // if (ttsManager != null)
            // {
            //     ttsManager.Speak("Voice mode deactivated.", true);
            // }
            
            Debug.Log("[CompanionAI] Voice mode deactivated");
        }
        
        // Speech Event Handlers
        private void HandleSpeechRecognized(string recognizedText)
        {
            Debug.Log($"[CompanionAI] Speech recognized: {recognizedText}");
            
            // Process the user's spoken request
            StartCoroutine(ProcessVoiceRequest(recognizedText));
        }
        
        private void HandleSpeechStarted()
        {
            // Update avatar to listening state
            if (avatarManager != null)
            {
                avatarManager.SetAnimationState(AvatarManager.AvatarState.Listening);
            }
        }
        
        private void HandleSpeechEnded()
        {
            // Avatar returns to idle
            if (avatarManager != null)
            {
                avatarManager.SetAnimationState(AvatarManager.AvatarState.Idle);
            }
        }
        
        private void HandleTTSStarted(string text)
        {
            // Update avatar to speaking state
            if (avatarManager != null)
            {
                avatarManager.SetAnimationState(AvatarManager.AvatarState.Speaking);
            }
        }
        
        private void HandleTTSCompleted(string text)
        {
            // Avatar returns to idle
            if (avatarManager != null)
            {
                avatarManager.SetAnimationState(AvatarManager.AvatarState.Idle);
            }
        }
        
        private System.Collections.IEnumerator ProcessVoiceRequest(string userInput)
        {
            if (ollamaClient == null || !ollamaClient.IsConnected)
            {
                Debug.LogWarning("[CompanionAI] AI not connected for voice request");
                // TODO: Enable TTS once compilation is fixed
                // if (ttsManager != null)
                // {
                //     ttsManager.Speak("I'm sorry, I'm not connected to my AI brain right now. Please check the connection.", true);
                // }
                yield break;
            }
            
            // Indicate processing
            Debug.Log("[CompanionAI] Processing voice request...");
            // TODO: Enable TTS once compilation is fixed
            // if (ttsManager != null)
            // {
            //     ttsManager.Speak("Let me think about that...", true);
            // }
            
            if (avatarManager != null)
            {
                avatarManager.SetAnimationState(AvatarManager.AvatarState.Thinking);
            }
            
            // Get current camera frame for context
            byte[] frameData = null;
            if (cameraManager != null && cameraManager.IsInitialized)
            {
                frameData = cameraManager.CaptureCurrentFrame();
            }
            
            // Build context-aware prompt
            string prompt = BuildContextualPrompt(userInput, frameData != null);
            
            // Send to AI
            bool responseReceived = false;
            yield return StartCoroutine(ollamaClient.SendMultiModalRequest(prompt, frameData, (response) =>
            {
                if (!string.IsNullOrEmpty(response))
                {
                    Debug.Log($"[CompanionAI] AI Response: {response}");
                    
                    // TODO: Enable TTS once compilation is fixed
                    // if (ttsManager != null)
                    // {
                    //     ttsManager.Speak(response, true);
                    // }
                }
                else
                {
                    Debug.LogWarning("[CompanionAI] Empty AI response");
                    // TODO: Enable TTS once compilation is fixed
                    // if (ttsManager != null)
                    // {
                    //     ttsManager.Speak("I'm sorry, I didn't understand that. Could you please rephrase?", true);
                    // }
                }
                responseReceived = true;
            }));
            
            // Wait for response or timeout
            float timeout = 10f;
            float elapsed = 0f;
            while (!responseReceived && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            if (!responseReceived)
            {
                Debug.LogWarning("[CompanionAI] AI response timeout");
                // TODO: Enable TTS once compilation is fixed
                // if (ttsManager != null)
                // {
                //     ttsManager.Speak("I'm taking longer than expected to respond. Please try again.", true);
                // }
            }
        }
        
        private string BuildContextualPrompt(string userInput, bool hasVisualContext)
        {
            StringBuilder prompt = new StringBuilder();
            
            prompt.AppendLine("You are a helpful AI assistant companion for an XREAL AR device user.");
            prompt.AppendLine("Respond naturally and conversationally in a helpful, friendly tone.");
            prompt.AppendLine("Keep responses concise but informative.");
            
            if (hasVisualContext)
            {
                prompt.AppendLine("I'm including an image of what the user is currently seeing through their XREAL device.");
                prompt.AppendLine("Use this visual context to provide more relevant and helpful responses.");
            }
            
            prompt.AppendLine($"User request: {userInput}");
            
            return prompt.ToString();
        }
        
        public bool IsSystemReady()
        {
            return isInitialized && isActive;
        }
        
        private void OnDestroy()
        {
            isActive = false;
            
            // Cleanup all systems
            if (audioManager != null)
                audioManager.Cleanup();
                
            if (cameraManager != null)
                cameraManager.Cleanup();
                
            if (avatarManager != null)
                avatarManager.Cleanup();
                
            if (ollamaClient != null)
                ollamaClient.Cleanup();
        }
    }
}
