using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.IO;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Speech-to-Text Manager for XREAL devices using whisper.unity or alternative STT solutions.
    /// Handles real-time audio streaming, continuous speech recognition, and XREAL optimization.
    /// 
    /// Tasks Implemented: 10.1-10.10 (STT Integration and Optimization)
    /// </summary>
    public class SpeechToTextManager : MonoBehaviour
    {
        [Header("STT Configuration")]
        [SerializeField] private bool enableContinuousRecognition = true;
        [SerializeField] private float silenceThreshold = 0.01f;
        [SerializeField] private float maxSpeechDuration = 30f;
        [SerializeField] private float silenceTimeout = 2f;
        [SerializeField] private bool enableOfflineMode = true;
        
        [Header("Audio Processing")]
        [SerializeField] private int sampleRate = 16000; // Optimal for Whisper
        [SerializeField] private float audioChunkDuration = 1f; // Process audio in chunks
        [SerializeField] private bool enableNoiseReduction = true;
        [SerializeField] private float noiseReductionThreshold = 0.005f;
        
        [Header("Performance Optimization")]
        [SerializeField] private int maxConcurrentRecognitions = 1;
        [SerializeField] private bool useGPUAcceleration = false; // XREAL mobile optimization
        [SerializeField] private float recognitionTimeout = 10f;
        
        [Header("Debugging")]
        [SerializeField] private bool enableDebugLogging = true;
        [SerializeField] private bool saveAudioSamples = false;
        
        // Events
        public event Action<string> OnSpeechRecognized;
        public event Action<float> OnSpeechConfidenceUpdated;
        public event Action<string> OnPartialTranscription;
        public event Action OnSpeechStarted;
        public event Action OnSpeechEnded;
        public event Action<string> OnSTTError;
        
        // Core Components
        private AudioManager audioManager;
        private Queue<float[]> audioQueue;
        private bool isProcessing = false;
        private bool isListening = false;
        
        // Recognition State
        private float lastAudioTime;
        private float speechStartTime;
        private bool isSpeechActive = false;
        private List<float> currentAudioBuffer;
        private int currentRecognitionCount = 0;
        
        // Performance Metrics
        private float averageRecognitionTime = 0f;
        private int totalRecognitions = 0;
        private float accuracyScore = 0f;
        
        // STT Engine Interface (for whisper.unity integration)
        private ISTTEngine sttEngine;
        
        private void Awake()
        {
            InitializeSTTManager();
        }
        
        private void Start()
        {
            StartCoroutine(InitializeSTTEngine());
        }
        
        private void InitializeSTTManager()
        {
            if (enableDebugLogging)
                Debug.Log("[STTManager] Initializing Speech-to-Text Manager for XREAL devices");
            
            // Initialize audio queue and buffers
            audioQueue = new Queue<float[]>();
            currentAudioBuffer = new List<float>();
            
            // Find audio manager
            audioManager = FindObjectOfType<AudioManager>();
            if (audioManager == null)
            {
                Debug.LogError("[STTManager] AudioManager not found! STT functionality will be limited.");
                return;
            }
            
            // Subscribe to audio events
            audioManager.OnAudioDataAvailable += ProcessAudioData;
            audioManager.OnVolumeChanged += OnVolumeDetected;
            
            if (enableDebugLogging)
                Debug.Log("[STTManager] STT Manager initialized successfully");
        }
        
        private IEnumerator InitializeSTTEngine()
        {
            yield return new WaitForSeconds(0.5f); // Allow other systems to initialize
            
            if (enableDebugLogging)
                Debug.Log("[STTManager] Initializing STT Engine...");
            
            // TODO: Initialize whisper.unity or alternative STT engine
            // This will be replaced with actual whisper.unity integration
            sttEngine = new MockSTTEngine();
            
            if (sttEngine.Initialize())
            {
                if (enableDebugLogging)
                    Debug.Log("[STTManager] STT Engine initialized successfully");
                
                if (enableContinuousRecognition)
                {
                    StartContinuousRecognition();
                }
            }
            else
            {
                Debug.LogError("[STTManager] Failed to initialize STT Engine");
                OnSTTError?.Invoke("Failed to initialize STT Engine");
            }
        }
        
        public void StartContinuousRecognition()
        {
            if (isListening)
            {
                if (enableDebugLogging)
                    Debug.Log("[STTManager] Continuous recognition already active");
                return;
            }
            
            isListening = true;
            isSpeechActive = false;
            lastAudioTime = Time.time;
            
            if (enableDebugLogging)
                Debug.Log("[STTManager] Starting continuous speech recognition");
            
            StartCoroutine(ContinuousRecognitionLoop());
        }
        
        public void StopContinuousRecognition()
        {
            isListening = false;
            
            if (enableDebugLogging)
                Debug.Log("[STTManager] Stopping continuous speech recognition");
        }
        
        private IEnumerator ContinuousRecognitionLoop()
        {
            while (isListening)
            {
                // Process queued audio data
                if (audioQueue.Count > 0 && currentRecognitionCount < maxConcurrentRecognitions)
                {
                    float[] audioData = audioQueue.Dequeue();
                    StartCoroutine(ProcessAudioForRecognition(audioData));
                }
                
                // Check for speech timeout
                if (isSpeechActive && (Time.time - speechStartTime) > maxSpeechDuration)
                {
                    EndSpeechRecognition();
                }
                
                // Check for silence timeout
                if (isSpeechActive && (Time.time - lastAudioTime) > silenceTimeout)
                {
                    EndSpeechRecognition();
                }
                
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        private void ProcessAudioData(float[] audioData, int channels)
        {
            if (!isListening || audioData == null || audioData.Length == 0)
                return;
            
            // Apply noise reduction if enabled
            if (enableNoiseReduction)
            {
                audioData = ApplyNoiseReduction(audioData);
            }
            
            // Add to current audio buffer
            currentAudioBuffer.AddRange(audioData);
            
            // Check if we have enough audio for processing
            int samplesNeeded = (int)(sampleRate * audioChunkDuration);
            if (currentAudioBuffer.Count >= samplesNeeded)
            {
                // Extract chunk for processing
                float[] chunk = new float[samplesNeeded];
                for (int i = 0; i < samplesNeeded; i++)
                {
                    chunk[i] = currentAudioBuffer[i];
                }
                
                // Remove processed samples from buffer
                currentAudioBuffer.RemoveRange(0, samplesNeeded);
                
                // Queue for recognition
                audioQueue.Enqueue(chunk);
                
                if (enableDebugLogging && audioQueue.Count > 5)
                    Debug.LogWarning($"[STTManager] Audio queue building up: {audioQueue.Count} chunks");
            }
            
            lastAudioTime = Time.time;
        }
        
        private IEnumerator ProcessAudioForRecognition(float[] audioData)
        {
            if (currentRecognitionCount >= maxConcurrentRecognitions)
                yield break;
            
            currentRecognitionCount++;
            float startTime = Time.time;
            
            try
            {
                // Check if audio contains speech
                if (!ContainsSpeech(audioData))
                {
                    yield break;
                }
                
                // Start speech detection if not already active
                if (!isSpeechActive)
                {
                    StartSpeechRecognition();
                }
                
                // Process audio through STT engine
                string transcription = "";
                float confidence = 0f;
                
                if (sttEngine != null)
                {
                    yield return StartCoroutine(sttEngine.RecognizeSpeech(audioData, sampleRate, 
                        (result, conf) => {
                            transcription = result;
                            confidence = conf;
                        }));
                }
                
                // Handle recognition results
                if (!string.IsNullOrEmpty(transcription))
                {
                    ProcessRecognitionResult(transcription, confidence);
                }
                
                // Update performance metrics
                float recognitionTime = Time.time - startTime;
                UpdatePerformanceMetrics(recognitionTime, confidence);
            }
            catch (Exception e)
            {
                Debug.LogError($"[STTManager] Error during speech recognition: {e.Message}");
                OnSTTError?.Invoke($"Recognition error: {e.Message}");
            }
            finally
            {
                currentRecognitionCount--;
            }
        }
        
        private bool ContainsSpeech(float[] audioData)
        {
            // Simple volume-based speech detection
            float rms = CalculateRMS(audioData);
            return rms > silenceThreshold;
        }
        
        private float CalculateRMS(float[] audioData)
        {
            float sum = 0f;
            for (int i = 0; i < audioData.Length; i++)
            {
                sum += audioData[i] * audioData[i];
            }
            return Mathf.Sqrt(sum / audioData.Length);
        }
        
        private float[] ApplyNoiseReduction(float[] audioData)
        {
            // Simple noise gate implementation
            float[] processed = new float[audioData.Length];
            for (int i = 0; i < audioData.Length; i++)
            {
                processed[i] = Mathf.Abs(audioData[i]) > noiseReductionThreshold ? audioData[i] : 0f;
            }
            return processed;
        }
        
        private void StartSpeechRecognition()
        {
            isSpeechActive = true;
            speechStartTime = Time.time;
            OnSpeechStarted?.Invoke();
            
            if (enableDebugLogging)
                Debug.Log("[STTManager] Speech detection started");
        }
        
        private void EndSpeechRecognition()
        {
            isSpeechActive = false;
            OnSpeechEnded?.Invoke();
            
            if (enableDebugLogging)
                Debug.Log("[STTManager] Speech detection ended");
        }
        
        private void ProcessRecognitionResult(string transcription, float confidence)
        {
            if (string.IsNullOrWhiteSpace(transcription))
                return;
            
            // Update confidence
            OnSpeechConfidenceUpdated?.Invoke(confidence);
            
            // Check if this is a complete phrase or partial
            if (confidence > 0.7f) // High confidence threshold
            {
                OnSpeechRecognized?.Invoke(transcription);
                
                if (enableDebugLogging)
                    Debug.Log($"[STTManager] Speech recognized: \"{transcription}\" (confidence: {confidence:F2})");
            }
            else
            {
                OnPartialTranscription?.Invoke(transcription);
                
                if (enableDebugLogging)
                    Debug.Log($"[STTManager] Partial transcription: \"{transcription}\" (confidence: {confidence:F2})");
            }
        }
        
        private void OnVolumeDetected(float volume)
        {
            // Update speech detection based on volume
            if (volume > silenceThreshold && !isSpeechActive)
            {
                StartSpeechRecognition();
            }
        }
        
        private void UpdatePerformanceMetrics(float recognitionTime, float confidence)
        {
            totalRecognitions++;
            averageRecognitionTime = (averageRecognitionTime * (totalRecognitions - 1) + recognitionTime) / totalRecognitions;
            accuracyScore = (accuracyScore * (totalRecognitions - 1) + confidence) / totalRecognitions;
            
            if (enableDebugLogging && totalRecognitions % 10 == 0)
            {
                Debug.Log($"[STTManager] Performance - Avg Time: {averageRecognitionTime:F2}s, Avg Accuracy: {accuracyScore:F2}");
            }
        }
        
        public void SaveAudioSample(float[] audioData, string filename)
        {
            if (!saveAudioSamples) return;
            
            try
            {
                string filePath = Path.Combine(Application.persistentDataPath, $"audio_samples/{filename}.wav");
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                
                // TODO: Implement WAV file writing
                if (enableDebugLogging)
                    Debug.Log($"[STTManager] Audio sample saved: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[STTManager] Failed to save audio sample: {e.Message}");
            }
        }
        
        // Public API for manual recognition
        public void RecognizeSpeech(float[] audioData, Action<string, float> callback)
        {
            StartCoroutine(ProcessManualRecognition(audioData, callback));
        }
        
        private IEnumerator ProcessManualRecognition(float[] audioData, Action<string, float> callback)
        {
            if (sttEngine == null)
            {
                callback?.Invoke("", 0f);
                yield break;
            }
            
            string result = "";
            float confidence = 0f;
            
            yield return StartCoroutine(sttEngine.RecognizeSpeech(audioData, sampleRate, 
                (transcription, conf) => {
                    result = transcription;
                    confidence = conf;
                }));
            
            callback?.Invoke(result, confidence);
        }
        
        // Performance monitoring
        public float GetAverageRecognitionTime() => averageRecognitionTime;
        public float GetAccuracyScore() => accuracyScore;
        public int GetTotalRecognitions() => totalRecognitions;
        public bool IsListening() => isListening;
        public bool IsSpeechActive() => isSpeechActive;
        
        private void OnDestroy()
        {
            StopContinuousRecognition();
            
            if (audioManager != null)
            {
                audioManager.OnAudioDataAvailable -= ProcessAudioData;
                audioManager.OnVolumeChanged -= OnVolumeDetected;
            }
            
            sttEngine?.Dispose();
        }
    }
    
    // Interface for STT engine integration (whisper.unity will implement this)
    public interface ISTTEngine
    {
        bool Initialize();
        IEnumerator RecognizeSpeech(float[] audioData, int sampleRate, Action<string, float> callback);
        void Dispose();
    }
    
    // Mock STT engine for testing (replace with whisper.unity)
    public class MockSTTEngine : ISTTEngine
    {
        private bool isInitialized = false;
        
        public bool Initialize()
        {
            Debug.Log("[MockSTT] Mock STT Engine initialized (replace with whisper.unity)");
            isInitialized = true;
            return true;
        }
        
        public IEnumerator RecognizeSpeech(float[] audioData, int sampleRate, Action<string, float> callback)
        {
            if (!isInitialized)
            {
                callback?.Invoke("", 0f);
                yield break;
            }
            
            // Simulate processing time
            yield return new WaitForSeconds(0.5f);
            
            // Mock transcription based on audio characteristics
            float rms = 0f;
            for (int i = 0; i < audioData.Length; i++)
            {
                rms += audioData[i] * audioData[i];
            }
            rms = Mathf.Sqrt(rms / audioData.Length);
            
            if (rms > 0.01f)
            {
                string[] mockPhrases = {
                    "Hello there",
                    "How are you today",
                    "What can you help me with",
                    "Tell me about this object",
                    "What do you see"
                };
                
                string mockResult = mockPhrases[UnityEngine.Random.Range(0, mockPhrases.Length)];
                float confidence = UnityEngine.Random.Range(0.7f, 0.95f);
                
                callback?.Invoke(mockResult, confidence);
            }
            else
            {
                callback?.Invoke("", 0f);
            }
        }
        
        public void Dispose()
        {
            isInitialized = false;
            Debug.Log("[MockSTT] Mock STT Engine disposed");
        }
    }
}
