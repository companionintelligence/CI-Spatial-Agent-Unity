using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.IO;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Text-to-Speech Manager for XREAL devices using piper.unity or alternative TTS solutions.
    /// Handles natural speech synthesis, SSML support, and XREAL audio optimization.
    /// 
    /// Tasks Implemented: 11.1-11.10 (TTS Integration and Optimization)
    /// </summary>
    public class TextToSpeechManager : MonoBehaviour
    {
        [Header("TTS Configuration")]
        [SerializeField] private string defaultVoiceModel = "en_US-lessac-medium";
        [SerializeField] private float speakingRate = 1.0f;
        [SerializeField] private float pitch = 1.0f;
        [SerializeField] private float volume = 0.8f;
        [SerializeField] private bool enableSSMLSupport = true;
        
        [Header("Voice Models")]
        [SerializeField] private VoiceModel[] availableVoices;
        [SerializeField] private string currentVoiceId = "default";
        
        [Header("Audio Processing")]
        [SerializeField] private int sampleRate = 22050; // Optimal for Piper
        [SerializeField] private AudioClip.LoadType loadType = AudioClip.LoadType.CompressedInMemory;
        [SerializeField] private bool enableBackgroundProcessing = true;
        [SerializeField] private int maxConcurrentSynthesis = 2;
        
        [Header("Performance Optimization")]
        [SerializeField] private bool enableAudioCaching = true;
        [SerializeField] private int maxCacheSize = 50;
        [SerializeField] private float synthesisTimeout = 15f;
        [SerializeField] private bool preloadCommonPhrases = true;
        
        [Header("XREAL Audio Optimization")]
        [SerializeField] private bool optimizeForXREAL = true;
        [SerializeField] private float xrealVolumeBoost = 1.2f;
        [SerializeField] private bool enable3DAudio = false;
        [SerializeField] private Transform avatarHead; // For positional audio
        
        [Header("Debugging")]
        [SerializeField] private bool enableDebugLogging = true;
        [SerializeField] private bool saveGeneratedAudio = false;
        
        // Events
        public event Action<string> OnSpeechStarted;
        public event Action<string> OnSpeechCompleted;
        public event Action<float> OnSynthesisProgress;
        public event Action<string> OnTTSError;
        public event Action OnQueueEmpty;
        
        // Core Components
        private AudioSource audioSource;
        private AudioSource backgroundAudioSource;
        private ITTSEngine ttsEngine;
        
        // Speech Queue Management
        private Queue<SpeechRequest> speechQueue;
        private Queue<SpeechRequest> priorityQueue;
        private bool isSpeaking = false;
        private bool isProcessing = false;
        private int currentSynthesisCount = 0;
        
        // Audio Caching
        private Dictionary<string, AudioClip> audioCache;
        private Dictionary<string, float> cacheUsageCount;
        
        // Performance Metrics
        private float averageSynthesisTime = 0f;
        private int totalSyntheses = 0;
        private float averageQueueWaitTime = 0f;
        
        // Common phrases for preloading
        private string[] commonPhrases = {
            "Hello",
            "I understand",
            "Let me help you with that",
            "I can see",
            "That's interesting",
            "Processing your request",
            "I'm here to assist you",
            "Is there anything else I can help with?"
        };
        
        [System.Serializable]
        public class VoiceModel
        {
            public string id;
            public string displayName;
            public string modelPath;
            public string language;
            public float baselineSpeed;
            public float baselinePitch;
            public bool isMale;
        }
        
        public class SpeechRequest
        {
            public string text;
            public string voiceId;
            public float priority;
            public bool useSSML;
            public Action<AudioClip> callback;
            public float requestTime;
            public TTSSettings settings;
        }
        
        [System.Serializable]
        public class TTSSettings
        {
            public float rate = 1.0f;
            public float pitch = 1.0f;
            public float volume = 1.0f;
            public string emotion = "neutral";
            public bool interruptible = true;
        }
        
        private void Awake()
        {
            InitializeTTSManager();
        }
        
        private void Start()
        {
            StartCoroutine(InitializeTTSEngine());
        }
        
        private void InitializeTTSManager()
        {
            if (enableDebugLogging)
                Debug.Log("[TTSManager] Initializing Text-to-Speech Manager for XREAL devices");
            
            // Initialize queues and caches
            speechQueue = new Queue<SpeechRequest>();
            priorityQueue = new Queue<SpeechRequest>();
            audioCache = new Dictionary<string, AudioClip>();
            cacheUsageCount = new Dictionary<string, float>();
            
            // Setup audio sources
            SetupAudioSources();
            
            // Initialize default voice models if not set
            if (availableVoices == null || availableVoices.Length == 0)
            {
                InitializeDefaultVoices();
            }
            
            if (enableDebugLogging)
                Debug.Log("[TTSManager] TTS Manager initialized successfully");
        }
        
        private void SetupAudioSources()
        {
            // Primary audio source for speech
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = volume * (optimizeForXREAL ? xrealVolumeBoost : 1.0f);
            audioSource.spatialBlend = enable3DAudio ? 1.0f : 0.0f;
            
            // Background audio source for queued speech
            if (enableBackgroundProcessing)
            {
                GameObject backgroundObj = new GameObject("TTS_Background_AudioSource");
                backgroundObj.transform.SetParent(transform);
                backgroundAudioSource = backgroundObj.AddComponent<AudioSource>();
                backgroundAudioSource.playOnAwake = false;
                backgroundAudioSource.volume = audioSource.volume;
                backgroundAudioSource.spatialBlend = audioSource.spatialBlend;
            }
            
            // Position for 3D audio if enabled
            if (enable3DAudio && avatarHead != null)
            {
                audioSource.transform.position = avatarHead.position;
                if (backgroundAudioSource != null)
                    backgroundAudioSource.transform.position = avatarHead.position;
            }
        }
        
        private void InitializeDefaultVoices()
        {
            availableVoices = new VoiceModel[]
            {
                new VoiceModel
                {
                    id = "default",
                    displayName = "Default Voice",
                    modelPath = "en_US-lessac-medium",
                    language = "en-US",
                    baselineSpeed = 1.0f,
                    baselinePitch = 1.0f,
                    isMale = false
                },
                new VoiceModel
                {
                    id = "male",
                    displayName = "Male Voice",
                    modelPath = "en_US-danny-low",
                    language = "en-US",
                    baselineSpeed = 0.9f,
                    baselinePitch = 0.8f,
                    isMale = true
                }
            };
        }
        
        private IEnumerator InitializeTTSEngine()
        {
            yield return new WaitForSeconds(0.5f);
            
            if (enableDebugLogging)
                Debug.Log("[TTSManager] Initializing TTS Engine...");
            
            // TODO: Initialize piper.unity or alternative TTS engine
            ttsEngine = new MockTTSEngine();
            
            if (ttsEngine.Initialize())
            {
                if (enableDebugLogging)
                    Debug.Log("[TTSManager] TTS Engine initialized successfully");
                
                // Preload common phrases
                if (preloadCommonPhrases)
                {
                    StartCoroutine(PreloadCommonPhrases());
                }
                
                // Start processing queue
                StartCoroutine(ProcessSpeechQueue());
            }
            else
            {
                Debug.LogError("[TTSManager] Failed to initialize TTS Engine");
                OnTTSError?.Invoke("Failed to initialize TTS Engine");
            }
        }
        
        private IEnumerator PreloadCommonPhrases()
        {
            if (enableDebugLogging)
                Debug.Log("[TTSManager] Preloading common phrases...");
            
            foreach (string phrase in commonPhrases)
            {
                yield return StartCoroutine(SynthesizeForCache(phrase, currentVoiceId));
                yield return new WaitForSeconds(0.1f); // Small delay between preloads
            }
            
            if (enableDebugLogging)
                Debug.Log($"[TTSManager] Preloaded {commonPhrases.Length} common phrases");
        }
        
        public void Speak(string text, bool highPriority = false, TTSSettings settings = null, Action<AudioClip> callback = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                if (enableDebugLogging)
                    Debug.LogWarning("[TTSManager] Attempted to speak empty text");
                return;
            }
            
            // Create speech request
            SpeechRequest request = new SpeechRequest
            {
                text = text,
                voiceId = currentVoiceId,
                priority = highPriority ? 100f : 1f,
                useSSML = enableSSMLSupport && ContainsSSML(text),
                callback = callback,
                requestTime = Time.time,
                settings = settings ?? new TTSSettings()
            };
            
            // Add to appropriate queue
            if (highPriority)
            {
                priorityQueue.Enqueue(request);
            }
            else
            {
                speechQueue.Enqueue(request);
            }
            
            if (enableDebugLogging)
                Debug.Log($"[TTSManager] Queued speech: \"{text}\" (Priority: {(highPriority ? "High" : "Normal")})");
        }
        
        public void SpeakImmediate(string text, bool interrupt = true, TTSSettings settings = null)
        {
            if (interrupt && isSpeaking)
            {
                StopCurrentSpeech();
            }
            
            StartCoroutine(ProcessImmediateSpeech(text, settings));
        }
        
        private IEnumerator ProcessImmediateSpeech(string text, TTSSettings settings)
        {
            isSpeaking = true;
            OnSpeechStarted?.Invoke(text);
            
            // Check cache first
            string cacheKey = GenerateCacheKey(text, currentVoiceId, settings);
            if (audioCache.ContainsKey(cacheKey))
            {
                PlayAudioClip(audioCache[cacheKey]);
                UpdateCacheUsage(cacheKey);
            }
            else
            {
                // Synthesize new audio
                AudioClip clip = null;
                yield return StartCoroutine(SynthesizeAudio(text, currentVoiceId, settings, 
                    (result) => clip = result));
                
                if (clip != null)
                {
                    if (enableAudioCaching)
                    {
                        CacheAudioClip(cacheKey, clip);
                    }
                    PlayAudioClip(clip);
                }
                else
                {
                    OnTTSError?.Invoke($"Failed to synthesize: {text}");
                }
            }
            
            // Wait for audio to complete
            if (audioSource.clip != null)
            {
                yield return new WaitForSeconds(audioSource.clip.length);
            }
            
            isSpeaking = false;
            OnSpeechCompleted?.Invoke(text);
        }
        
        private IEnumerator ProcessSpeechQueue()
        {
            while (true)
            {
                // Process priority queue first
                while (priorityQueue.Count > 0 && currentSynthesisCount < maxConcurrentSynthesis)
                {
                    SpeechRequest request = priorityQueue.Dequeue();
                    StartCoroutine(ProcessSpeechRequest(request));
                }
                
                // Process normal queue
                while (speechQueue.Count > 0 && currentSynthesisCount < maxConcurrentSynthesis && !isSpeaking)
                {
                    SpeechRequest request = speechQueue.Dequeue();
                    StartCoroutine(ProcessSpeechRequest(request));
                }
                
                // Check if queue is empty
                if (speechQueue.Count == 0 && priorityQueue.Count == 0 && currentSynthesisCount == 0)
                {
                    OnQueueEmpty?.Invoke();
                }
                
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        private IEnumerator ProcessSpeechRequest(SpeechRequest request)
        {
            currentSynthesisCount++;
            float startTime = Time.time;
            
            try
            {
                float waitTime = Time.time - request.requestTime;
                
                string cacheKey = GenerateCacheKey(request.text, request.voiceId, request.settings);
                AudioClip clip = null;
                
                // Check cache
                if (audioCache.ContainsKey(cacheKey))
                {
                    clip = audioCache[cacheKey];
                    UpdateCacheUsage(cacheKey);
                }
                else
                {
                    // Synthesize new audio
                    yield return StartCoroutine(SynthesizeAudio(request.text, request.voiceId, request.settings,
                        (result) => clip = result));
                    
                    if (clip != null && enableAudioCaching)
                    {
                        CacheAudioClip(cacheKey, clip);
                    }
                }
                
                // Play or callback
                if (clip != null)
                {
                    if (request.callback != null)
                    {
                        request.callback.Invoke(clip);
                    }
                    else
                    {
                        yield return StartCoroutine(PlaySpeechClip(clip, request.text));
                    }
                }
                
                // Update metrics
                float totalTime = Time.time - startTime;
                UpdatePerformanceMetrics(totalTime, waitTime);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TTSManager] Error processing speech request: {e.Message}");
                OnTTSError?.Invoke($"Speech processing error: {e.Message}");
            }
            finally
            {
                currentSynthesisCount--;
            }
        }
        
        private IEnumerator PlaySpeechClip(AudioClip clip, string text)
        {
            while (isSpeaking)
            {
                yield return new WaitForSeconds(0.1f);
            }
            
            isSpeaking = true;
            OnSpeechStarted?.Invoke(text);
            
            PlayAudioClip(clip);
            
            yield return new WaitForSeconds(clip.length);
            
            isSpeaking = false;
            OnSpeechCompleted?.Invoke(text);
        }
        
        private void PlayAudioClip(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.clip = clip;
                audioSource.Play();
                
                if (enableDebugLogging)
                    Debug.Log($"[TTSManager] Playing audio clip: {clip.name} ({clip.length:F2}s)");
            }
        }
        
        private IEnumerator SynthesizeAudio(string text, string voiceId, TTSSettings settings, Action<AudioClip> callback)
        {
            if (ttsEngine == null)
            {
                callback?.Invoke(null);
                yield break;
            }
            
            VoiceModel voice = GetVoiceModel(voiceId);
            if (voice == null)
            {
                Debug.LogWarning($"[TTSManager] Voice model not found: {voiceId}, using default");
                voice = GetVoiceModel("default");
            }
            
            AudioClip result = null;
            yield return StartCoroutine(ttsEngine.SynthesizeSpeech(text, voice, settings, 
                (clip) => result = clip));
            
            callback?.Invoke(result);
        }
        
        private IEnumerator SynthesizeForCache(string text, string voiceId)
        {
            string cacheKey = GenerateCacheKey(text, voiceId, new TTSSettings());
            if (audioCache.ContainsKey(cacheKey))
                yield break;
            
            AudioClip clip = null;
            yield return StartCoroutine(SynthesizeAudio(text, voiceId, new TTSSettings(), 
                (result) => clip = result));
            
            if (clip != null)
            {
                CacheAudioClip(cacheKey, clip);
            }
        }
        
        private bool ContainsSSML(string text)
        {
            return text.Contains("<") && text.Contains(">");
        }
        
        private string GenerateCacheKey(string text, string voiceId, TTSSettings settings)
        {
            return $"{text}_{voiceId}_{settings.rate}_{settings.pitch}_{settings.emotion}";
        }
        
        private void CacheAudioClip(string key, AudioClip clip)
        {
            if (audioCache.ContainsKey(key))
                return;
            
            // Manage cache size
            if (audioCache.Count >= maxCacheSize)
            {
                CleanupCache();
            }
            
            audioCache[key] = clip;
            cacheUsageCount[key] = 1f;
            
            if (enableDebugLogging)
                Debug.Log($"[TTSManager] Cached audio clip: {key}");
        }
        
        private void UpdateCacheUsage(string key)
        {
            if (cacheUsageCount.ContainsKey(key))
            {
                cacheUsageCount[key]++;
            }
        }
        
        private void CleanupCache()
        {
            // Remove least used items
            string leastUsedKey = "";
            float minUsage = float.MaxValue;
            
            foreach (var kvp in cacheUsageCount)
            {
                if (kvp.Value < minUsage)
                {
                    minUsage = kvp.Value;
                    leastUsedKey = kvp.Key;
                }
            }
            
            if (!string.IsNullOrEmpty(leastUsedKey))
            {
                if (audioCache.ContainsKey(leastUsedKey))
                {
                    DestroyImmediate(audioCache[leastUsedKey]);
                    audioCache.Remove(leastUsedKey);
                }
                cacheUsageCount.Remove(leastUsedKey);
                
                if (enableDebugLogging)
                    Debug.Log($"[TTSManager] Removed cached audio: {leastUsedKey}");
            }
        }
        
        private VoiceModel GetVoiceModel(string voiceId)
        {
            foreach (var voice in availableVoices)
            {
                if (voice.id == voiceId)
                    return voice;
            }
            return null;
        }
        
        private void UpdatePerformanceMetrics(float synthesisTime, float waitTime)
        {
            totalSyntheses++;
            averageSynthesisTime = (averageSynthesisTime * (totalSyntheses - 1) + synthesisTime) / totalSyntheses;
            averageQueueWaitTime = (averageQueueWaitTime * (totalSyntheses - 1) + waitTime) / totalSyntheses;
            
            if (enableDebugLogging && totalSyntheses % 10 == 0)
            {
                Debug.Log($"[TTSManager] Performance - Avg Synthesis: {averageSynthesisTime:F2}s, Avg Wait: {averageQueueWaitTime:F2}s");
            }
        }
        
        // Public API
        public void StopCurrentSpeech()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
                isSpeaking = false;
                
                if (enableDebugLogging)
                    Debug.Log("[TTSManager] Speech interrupted");
            }
        }
        
        public void ClearQueue()
        {
            speechQueue.Clear();
            priorityQueue.Clear();
            
            if (enableDebugLogging)
                Debug.Log("[TTSManager] Speech queue cleared");
        }
        
        public void SetVoice(string voiceId)
        {
            if (GetVoiceModel(voiceId) != null)
            {
                currentVoiceId = voiceId;
                
                if (enableDebugLogging)
                    Debug.Log($"[TTSManager] Voice changed to: {voiceId}");
            }
            else
            {
                Debug.LogWarning($"[TTSManager] Voice not found: {voiceId}");
            }
        }
        
        public void SetTTSSettings(TTSSettings settings)
        {
            speakingRate = settings.rate;
            pitch = settings.pitch;
            volume = settings.volume;
            
            if (audioSource != null)
            {
                audioSource.volume = volume * (optimizeForXREAL ? xrealVolumeBoost : 1.0f);
            }
        }
        
        // Performance monitoring
        public float GetAverageSynthesisTime() => averageSynthesisTime;
        public float GetAverageQueueWaitTime() => averageQueueWaitTime;
        public int GetTotalSyntheses() => totalSyntheses;
        public int GetQueueLength() => speechQueue.Count + priorityQueue.Count;
        public bool IsSpeaking() => isSpeaking;
        public int GetCacheSize() => audioCache.Count;
        
        private void Update()
        {
            // Update 3D audio position if enabled
            if (enable3DAudio && avatarHead != null)
            {
                audioSource.transform.position = avatarHead.position;
                if (backgroundAudioSource != null)
                    backgroundAudioSource.transform.position = avatarHead.position;
            }
        }
        
        private void OnDestroy()
        {
            StopCurrentSpeech();
            ClearQueue();
            
            // Cleanup cache
            foreach (var clip in audioCache.Values)
            {
                if (clip != null)
                    DestroyImmediate(clip);
            }
            audioCache.Clear();
            
            ttsEngine?.Dispose();
        }
    }
    
    // Interface for TTS engine integration (piper.unity will implement this)
    public interface ITTSEngine
    {
        bool Initialize();
        IEnumerator SynthesizeSpeech(string text, TextToSpeechManager.VoiceModel voice, 
            TextToSpeechManager.TTSSettings settings, Action<AudioClip> callback);
        void Dispose();
    }
    
    // Mock TTS engine for testing (replace with piper.unity)
    public class MockTTSEngine : ITTSEngine
    {
        private bool isInitialized = false;
        
        public bool Initialize()
        {
            Debug.Log("[MockTTS] Mock TTS Engine initialized (replace with piper.unity)");
            isInitialized = true;
            return true;
        }
        
        public IEnumerator SynthesizeSpeech(string text, TextToSpeechManager.VoiceModel voice, 
            TextToSpeechManager.TTSSettings settings, Action<AudioClip> callback)
        {
            if (!isInitialized)
            {
                callback?.Invoke(null);
                yield break;
            }
            
            // Simulate synthesis time
            yield return new WaitForSeconds(0.3f);
            
            // Generate mock audio clip
            int sampleRate = 22050;
            float duration = Mathf.Max(0.5f, text.Length * 0.05f * (1f / settings.rate));
            int samples = (int)(sampleRate * duration);
            
            float[] audioData = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                // Generate simple sine wave with some variation
                float freq = 440f * settings.pitch;
                audioData[i] = Mathf.Sin(2 * Mathf.PI * freq * i / sampleRate) * 0.1f;
            }
            
            AudioClip clip = AudioClip.Create($"TTS_{text.Substring(0, Mathf.Min(10, text.Length))}", 
                samples, 1, sampleRate, false);
            clip.SetData(audioData, 0);
            
            callback?.Invoke(clip);
        }
        
        public void Dispose()
        {
            isInitialized = false;
            Debug.Log("[MockTTS] Mock TTS Engine disposed");
        }
    }
}
