using UnityEngine;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Centralized configuration for XREAL Vision System
    /// Provides environment-specific settings and best practices
    /// </summary>
    [CreateAssetMenu(fileName = "XREALVisionConfig", menuName = "CompanionIntelligence/XREAL Vision Config")]
    public class XREALVisionConfig : ScriptableObject
    {
        [Header("System Settings")]
        [SerializeField] private bool enableDebugLogging = true;
        [SerializeField] private float systemTimeout = 30.0f;
        [SerializeField] private int maxRetries = 3;
        [SerializeField] private float retryDelay = 1.0f;
        
        [Header("Camera Settings")]
        [SerializeField] private int defaultCaptureWidth = 512;
        [SerializeField] private int defaultCaptureHeight = 512;
        [SerializeField] private TextureFormat captureFormat = TextureFormat.RGB24;
        [SerializeField] private float captureDelay = 1.0f;
        
        [Header("Ollama Settings")]
        [SerializeField] private string defaultServerIP = "192.168.1.100";
        [SerializeField] private int defaultServerPort = 11434;
        [SerializeField] private string defaultModel = "llava:7b";
        [SerializeField] private int maxTokens = 300;
        [SerializeField] private float temperature = 0.7f;
        [SerializeField] private string systemPrompt = "You are a helpful vision assistant for XREAL glasses. Provide clear, concise descriptions of what you see.";
        
        [Header("Speech Settings")]
        [SerializeField] private int maxRecordingLength = 10;
        [SerializeField] private int sampleRate = 16000;
        [SerializeField] private float voiceThreshold = 0.01f;
        [SerializeField] private string openAIApiKey = "YOUR_OPENAI_API_KEY";
        
        [Header("Environment Profiles")]
        [SerializeField] private EnvironmentProfile development;
        [SerializeField] private EnvironmentProfile production;
        [SerializeField] private EnvironmentProfile testing;
        
        [System.Serializable]
        public class EnvironmentProfile
        {
            public string name;
            public string ollamaServerIP;
            public int ollamaServerPort;
            public bool enableDebugLogging;
            public bool enableMockServices;
            public float requestTimeout;
        }
        
        public bool EnableDebugLogging => enableDebugLogging;
        public float SystemTimeout => systemTimeout;
        public int MaxRetries => maxRetries;
        public float RetryDelay => retryDelay;
        
        public int DefaultCaptureWidth => defaultCaptureWidth;
        public int DefaultCaptureHeight => defaultCaptureHeight;
        public TextureFormat CaptureFormat => captureFormat;
        public float CaptureDelay => captureDelay;
        
        public string DefaultServerIP => defaultServerIP;
        public int DefaultServerPort => defaultServerPort;
        public string DefaultModel => defaultModel;
        public int MaxTokens => maxTokens;
        public float Temperature => temperature;
        public string SystemPrompt => systemPrompt;
        
        public int MaxRecordingLength => maxRecordingLength;
        public int SampleRate => sampleRate;
        public float VoiceThreshold => voiceThreshold;
        public string OpenAIApiKey => openAIApiKey;
        
        public EnvironmentProfile GetCurrentProfile()
        {
#if UNITY_EDITOR
            return development;
#elif DEVELOPMENT_BUILD
            return testing;
#else
            return production;
#endif
        }
        
        /// <summary>
        /// Apply configuration profile to system
        /// </summary>
        public void ApplyProfile(EnvironmentProfile profile)
        {
            if (profile == null) return;
            
            enableDebugLogging = profile.enableDebugLogging;
            systemTimeout = profile.requestTimeout;
            defaultServerIP = profile.ollamaServerIP;
            defaultServerPort = profile.ollamaServerPort;
        }
        
        /// <summary>
        /// Validate configuration settings
        /// </summary>
        public bool ValidateConfig()
        {
            bool isValid = true;
            
            if (defaultCaptureWidth <= 0 || defaultCaptureHeight <= 0)
            {
                Debug.LogError("[XREALVisionConfig] Invalid capture dimensions");
                isValid = false;
            }
            
            if (string.IsNullOrEmpty(defaultServerIP))
            {
                Debug.LogError("[XREALVisionConfig] Server IP cannot be empty");
                isValid = false;
            }
            
            if (defaultServerPort <= 0 || defaultServerPort > 65535)
            {
                Debug.LogError("[XREALVisionConfig] Invalid server port");
                isValid = false;
            }
            
            if (maxTokens <= 0)
            {
                Debug.LogError("[XREALVisionConfig] Max tokens must be positive");
                isValid = false;
            }
            
            return isValid;
        }
        
        private void OnValidate()
        {
            // Ensure valid values
            defaultCaptureWidth = Mathf.Max(64, defaultCaptureWidth);
            defaultCaptureHeight = Mathf.Max(64, defaultCaptureHeight);
            defaultServerPort = Mathf.Clamp(defaultServerPort, 1, 65535);
            maxTokens = Mathf.Max(1, maxTokens);
            temperature = Mathf.Clamp01(temperature);
            systemTimeout = Mathf.Max(1.0f, systemTimeout);
        }
    }
}
