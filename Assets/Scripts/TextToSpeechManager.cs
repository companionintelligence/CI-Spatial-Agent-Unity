using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using System;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Text-to-Speech Manager compatible with XREAL devices and Ollama
    /// Uses Unity's AudioSource for playback, supports multiple voices and speeds
    /// </summary>
    public class TextToSpeechManager : MonoBehaviour
    {
        [Header("TTS Configuration")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private float defaultSpeed = 1.0f;
        [SerializeField] private float defaultPitch = 1.0f;
        [SerializeField] private float defaultVolume = 0.8f;
        [SerializeField] private bool enableDebugLogging = true;
        
        [Header("Voice Settings")]
        [SerializeField] private TTSVoice selectedVoice = TTSVoice.Default;
        [SerializeField] private bool enableSpatialAudio = false;
        [SerializeField] private float audioFadeInTime = 0.1f;
        [SerializeField] private float audioFadeOutTime = 0.2f;
        
        [Header("Queue Management")]
        [SerializeField] private bool allowQueueing = true;
        [SerializeField] private int maxQueueSize = 10;
        [SerializeField] private bool interruptOnNewRequest = false;
        
        // Events
        public UnityEvent<string> OnSpeechStarted;
        public UnityEvent<string> OnSpeechCompleted;
        public UnityEvent<string> OnSpeechError;
        public UnityEvent OnSpeechQueueEmpty;
        
        // TTS Queue and State
        private Queue<TTSRequest> speechQueue = new Queue<TTSRequest>();
        private bool isSpeaking = false;
        private bool isInitialized = false;
        private Coroutine currentSpeechCoroutine;
        private TTSRequest currentRequest;
        
        // Audio management
        private float originalVolume;
        private float originalPitch;
        
        public bool IsSpeaking => isSpeaking;
        public bool IsInitialized => isInitialized;
        public int QueueCount => speechQueue.Count;
        
        public enum TTSVoice
        {
            Default,
            Male,
            Female,
            Robotic,
            Natural
        }
        
        [System.Serializable]
        public class TTSRequest
        {
            public string text;
            public float speed;
            public float pitch;
            public float volume;
            public TTSVoice voice;
            public Action onComplete;
            public Action<string> onError;
            
            public TTSRequest(string text, float speed = 1.0f, float pitch = 1.0f, float volume = 0.8f, TTSVoice voice = TTSVoice.Default)
            {
                this.text = text;
                this.speed = speed;
                this.pitch = pitch;
                this.volume = volume;
                this.voice = voice;
            }
        }
        
        private void Awake()
        {
            // Ensure we have an AudioSource
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            
            // Store original audio settings
            originalVolume = audioSource.volume;
            originalPitch = audioSource.pitch;
        }
        
        private void Start()
        {
            InitializeTTS();
        }
        
        private void InitializeTTS()
        {
            try
            {
                // Configure AudioSource for speech
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.volume = defaultVolume;
                audioSource.pitch = defaultPitch;
                
                // Configure spatial audio if enabled
                if (enableSpatialAudio)
                {
                    audioSource.spatialBlend = 1.0f; // 3D audio
                    audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                    audioSource.maxDistance = 10f;
                }
                else
                {
                    audioSource.spatialBlend = 0.0f; // 2D audio
                }
                
                isInitialized = true;
                
                if (enableDebugLogging)
                    Debug.Log("[TextToSpeechManager] TTS system initialized successfully");
                
                // Start processing queue
                StartCoroutine(ProcessSpeechQueue());
            }
            catch (Exception e)
            {
                OnSpeechError?.Invoke($"TTS initialization failed: {e.Message}");
                if (enableDebugLogging)
                    Debug.LogError($"[TextToSpeechManager] Initialization error: {e}");
            }
        }
        
        public void SpeakText(string text)
        {
            SpeakText(text, defaultSpeed, defaultPitch, defaultVolume, selectedVoice);
        }
        
        public void SpeakText(string text, float speed)
        {
            SpeakText(text, speed, defaultPitch, defaultVolume, selectedVoice);
        }
        
        public void SpeakText(string text, float speed, float pitch, float volume, TTSVoice voice = TTSVoice.Default, Action onComplete = null, Action<string> onError = null)
        {
            if (!isInitialized)
            {
                onError?.Invoke("TTS system not initialized");
                OnSpeechError?.Invoke("TTS system not initialized");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(text))
            {
                onError?.Invoke("Empty text provided");
                return;
            }
            
            var request = new TTSRequest(text, speed, pitch, volume, voice)
            {
                onComplete = onComplete,
                onError = onError
            };
            
            // Handle interruption if enabled
            if (interruptOnNewRequest && isSpeaking)
            {
                StopCurrentSpeech();
                speechQueue.Clear();
            }
            
            // Add to queue if queueing is allowed or if not currently speaking
            if (allowQueueing || !isSpeaking)
            {
                if (speechQueue.Count < maxQueueSize)
                {
                    speechQueue.Enqueue(request);
                    
                    if (enableDebugLogging)
                        Debug.Log($"[TextToSpeechManager] Added to speech queue: \"{text.Substring(0, Mathf.Min(50, text.Length))}...\"");
                }
                else
                {
                    onError?.Invoke("Speech queue is full");
                    OnSpeechError?.Invoke("Speech queue is full");
                }
            }
            else
            {
                onError?.Invoke("Currently speaking and queueing is disabled");
            }
        }
        
        private IEnumerator ProcessSpeechQueue()
        {
            while (isInitialized)
            {
                if (speechQueue.Count > 0 && !isSpeaking)
                {
                    currentRequest = speechQueue.Dequeue();
                    currentSpeechCoroutine = StartCoroutine(SpeakTextInternal(currentRequest));
                }
                else if (speechQueue.Count == 0 && !isSpeaking)
                {
                    OnSpeechQueueEmpty?.Invoke();
                }
                
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        private IEnumerator SpeakTextInternal(TTSRequest request)
        {
            isSpeaking = true;
            
            try
            {
                OnSpeechStarted?.Invoke(request.text);
                
                if (enableDebugLogging)
                    Debug.Log($"[TextToSpeechManager] Speaking: \"{request.text}\"");
                
                // Generate speech audio clip
                AudioClip speechClip = GenerateSpeechClip(request);
                
                if (speechClip != null)
                {
                    // Apply audio settings
                    audioSource.pitch = request.pitch;
                    audioSource.volume = 0f; // Start silent for fade in
                    audioSource.clip = speechClip;
                    
                    // Play audio
                    audioSource.Play();
                    
                    // Fade in
                    yield return StartCoroutine(FadeAudio(0f, request.volume, audioFadeInTime));
                    
                    // Wait for speech to complete (adjusted for speed)
                    float adjustedLength = speechClip.length / request.speed;
                    yield return new WaitForSeconds(adjustedLength - audioFadeInTime - audioFadeOutTime);
                    
                    // Fade out
                    yield return StartCoroutine(FadeAudio(request.volume, 0f, audioFadeOutTime));
                    
                    // Stop audio
                    audioSource.Stop();
                    audioSource.clip = null;
                    
                    // Clean up
                    if (speechClip != null)
                    {
                        Destroy(speechClip);
                    }
                    
                    // Notify completion
                    request.onComplete?.Invoke();
                    OnSpeechCompleted?.Invoke(request.text);
                    
                    if (enableDebugLogging)
                        Debug.Log($"[TextToSpeechManager] Speech completed: \"{request.text}\"");
                }
                else
                {
                    string error = "Failed to generate speech audio";
                    request.onError?.Invoke(error);
                    OnSpeechError?.Invoke(error);
                }
            }
            catch (Exception e)
            {
                string error = $"Speech synthesis error: {e.Message}";
                request.onError?.Invoke(error);
                OnSpeechError?.Invoke(error);
                
                if (enableDebugLogging)
                    Debug.LogError($"[TextToSpeechManager] {error}");
            }
            finally
            {
                isSpeaking = false;
                currentRequest = null;
                currentSpeechCoroutine = null;
                
                // Restore original audio settings
                audioSource.pitch = originalPitch;
                audioSource.volume = originalVolume;
            }
        }
        
        private AudioClip GenerateSpeechClip(TTSRequest request)
        {
            // This is a mock implementation for demonstration
            // In a real implementation, this would interface with a TTS engine
            // or generate audio from text using speech synthesis
            
            // For now, generate a simple tone sequence based on text length
            float duration = Mathf.Max(1f, request.text.Length * 0.1f); // Estimate duration
            int sampleRate = 22050;
            int samples = Mathf.RoundToInt(duration * sampleRate);
            
            float[] audioData = new float[samples];
            
            // Generate simple tone pattern (placeholder for real TTS)
            float frequency = GetVoiceFrequency(request.voice);
            
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                audioData[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * 0.1f;
                
                // Add some variation to make it sound more speech-like
                audioData[i] += Mathf.Sin(2 * Mathf.PI * (frequency * 1.5f) * t) * 0.05f;
                audioData[i] *= Mathf.Exp(-t * 0.5f); // Fade out over time
            }
            
            AudioClip clip = AudioClip.Create($"TTS_{DateTime.Now.Ticks}", samples, 1, sampleRate, false);
            clip.SetData(audioData, 0);
            
            return clip;
        }
        
        private float GetVoiceFrequency(TTSVoice voice)
        {
            switch (voice)
            {
                case TTSVoice.Male:
                    return 110f; // Lower frequency for male voice
                case TTSVoice.Female:
                    return 220f; // Higher frequency for female voice
                case TTSVoice.Robotic:
                    return 440f; // Electronic frequency
                case TTSVoice.Natural:
                    return 165f; // Natural speaking frequency
                default:
                    return 165f; // Default frequency
            }
        }
        
        private IEnumerator FadeAudio(float startVolume, float endVolume, float duration)
        {
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                audioSource.volume = Mathf.Lerp(startVolume, endVolume, t);
                yield return null;
            }
            
            audioSource.volume = endVolume;
        }
        
        public void StopCurrentSpeech()
        {
            if (isSpeaking && currentSpeechCoroutine != null)
            {
                StopCoroutine(currentSpeechCoroutine);
                currentSpeechCoroutine = null;
                
                audioSource.Stop();
                audioSource.clip = null;
                
                isSpeaking = false;
                currentRequest = null;
                
                if (enableDebugLogging)
                    Debug.Log("[TextToSpeechManager] Speech interrupted");
            }
        }
        
        public void ClearQueue()
        {
            speechQueue.Clear();
            
            if (enableDebugLogging)
                Debug.Log("[TextToSpeechManager] Speech queue cleared");
        }
        
        public void StopAllSpeech()
        {
            StopCurrentSpeech();
            ClearQueue();
        }
        
        public void SetVoice(TTSVoice voice)
        {
            selectedVoice = voice;
            
            if (enableDebugLogging)
                Debug.Log($"[TextToSpeechManager] Voice changed to: {voice}");
        }
        
        public void SetSpeed(float speed)
        {
            defaultSpeed = Mathf.Clamp(speed, 0.25f, 4.0f);
        }
        
        public void SetPitch(float pitch)
        {
            defaultPitch = Mathf.Clamp(pitch, 0.5f, 2.0f);
        }
        
        public void SetVolume(float volume)
        {
            defaultVolume = Mathf.Clamp01(volume);
        }
        
        private void OnDestroy()
        {
            StopAllSpeech();
            
            if (enableDebugLogging)
                Debug.Log("[TextToSpeechManager] TTS system cleaned up");
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                StopCurrentSpeech();
            }
        }
    }
}
