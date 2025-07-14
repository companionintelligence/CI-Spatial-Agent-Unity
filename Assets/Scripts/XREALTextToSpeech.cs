using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// XREAL Text to Speech - Handles audio synthesis and playback
    /// Consolidated TTS component optimized for XREAL devices
    /// </summary>
    public class XREALTextToSpeech : MonoBehaviour
    {
        [Header("TTS Configuration")]
        [SerializeField] private TTSService ttsService = TTSService.OpenAI;
        [SerializeField] private string openAIApiKey = "YOUR_OPENAI_API_KEY";
        [SerializeField] private TTSModel model = TTSModel.TTS1;
        [SerializeField] private TTSVoice voice = TTSVoice.Alloy;
        [SerializeField] private float speechSpeed = 1.0f;
        
        [Header("Audio Settings")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private bool autoCreateAudioSource = true;
        [SerializeField] private float defaultVolume = 0.8f;
        [SerializeField] private bool enableSpatialAudio = false;
        [SerializeField] private bool enableDebugLogging = true;
        
        [Header("Queue Management")]
        [SerializeField] private bool allowQueueing = true;
        [SerializeField] private int maxQueueSize = 5;
        [SerializeField] private float requestTimeout = 30.0f;
        
        [Header("Events")]
        public UnityEvent OnSpeechStarted;
        public UnityEvent OnSpeechCompleted;
        public UnityEvent<string> OnError;
        
        public enum TTSService
        {
            OpenAI,
            Local, // For future local TTS implementation
            Unity  // Unity's built-in TTS (limited functionality)
        }
        
        public enum TTSModel
        {
            TTS1,
            TTS1_HD
        }
        
        public enum TTSVoice
        {
            Alloy,
            Echo,
            Fable,
            Onyx,
            Nova,
            Shimmer
        }
        
        // State
        private bool isInitialized = false;
        private bool isSpeaking = false;
        private Queue<TTSRequest> speechQueue = new Queue<TTSRequest>();
        private Coroutine currentSpeechCoroutine;
        
        public bool IsInitialized => isInitialized;
        public bool IsSpeaking => isSpeaking;
        public int QueuedItems => speechQueue.Count;
        
        [Serializable]
        private class TTSRequest
        {
            public string text;
            public TTSVoice voice;
            public float speed;
            public Action onComplete;
            public Action<string> onError;
            
            public TTSRequest(string text, TTSVoice voice = TTSVoice.Alloy, float speed = 1.0f)
            {
                this.text = text;
                this.voice = voice;
                this.speed = speed;
            }
        }
        
        [Serializable]
        private class OpenAITTSPayload
        {
            public string model;
            public string input;
            public string voice;
            public string response_format = "mp3";
            public float speed = 1.0f;
        }
        
        private void Start()
        {
            Initialize();
        }
        
        public void Initialize()
        {
            if (isInitialized) return;
            
            LogDebug("Initializing Text to Speech...");
            
            // Setup audio source
            SetupAudioSource();
            
            // Start processing queue
            StartCoroutine(ProcessSpeechQueue());
            
            isInitialized = true;
            LogDebug("Text to Speech initialized successfully");
        }
        
        private void SetupAudioSource()
        {
            if (audioSource == null && autoCreateAudioSource)
            {
                audioSource = gameObject.GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.volume = defaultVolume;
                
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
                
                LogDebug("Audio source configured");
            }
            else
            {
                LogError("No audio source available for TTS playback");
            }
        }
        
        /// <summary>
        /// Speak text with default settings
        /// </summary>
        public void SpeakText(string text)
        {
            SpeakText(text, voice, speechSpeed);
        }
        
        /// <summary>
        /// Speak text with custom voice and speed
        /// </summary>
        public void SpeakText(string text, TTSVoice customVoice, float customSpeed = 1.0f)
        {
            if (!isInitialized)
            {
                LogError("TTS not initialized");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(text))
            {
                LogDebug("Empty text provided for TTS");
                return;
            }
            
            var request = new TTSRequest(text, customVoice, customSpeed);
            
            if (allowQueueing)
            {
                if (speechQueue.Count >= maxQueueSize)
                {
                    LogDebug("Speech queue full, removing oldest item");
                    speechQueue.Dequeue();
                }
                speechQueue.Enqueue(request);
                LogDebug($"Added to speech queue: \"{text}\" (Queue size: {speechQueue.Count})");
            }
            else
            {
                // Clear queue and add only this request
                speechQueue.Clear();
                speechQueue.Enqueue(request);
                LogDebug($"Replaced speech queue with: \"{text}\"");
            }
        }
        
        /// <summary>
        /// Stop current speech and clear queue
        /// </summary>
        public void StopSpeaking()
        {
            if (currentSpeechCoroutine != null)
            {
                StopCoroutine(currentSpeechCoroutine);
                currentSpeechCoroutine = null;
            }
            
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            
            speechQueue.Clear();
            isSpeaking = false;
            
            LogDebug("Speech stopped and queue cleared");
        }
        
        /// <summary>
        /// Clear the speech queue without stopping current speech
        /// </summary>
        public void ClearQueue()
        {
            speechQueue.Clear();
            LogDebug("Speech queue cleared");
        }
        
        private IEnumerator ProcessSpeechQueue()
        {
            while (true)
            {
                if (speechQueue.Count > 0 && !isSpeaking)
                {
                    var request = speechQueue.Dequeue();
                    currentSpeechCoroutine = StartCoroutine(ProcessTTSRequest(request));
                    yield return currentSpeechCoroutine;
                    currentSpeechCoroutine = null;
                }
                
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        private IEnumerator ProcessTTSRequest(TTSRequest request)
        {
            isSpeaking = true;
            OnSpeechStarted?.Invoke();
            
            LogDebug($"Processing TTS request: \"{request.text}\"");
            
            try
            {
                yield return StartCoroutine(GenerateAndPlaySpeech(request));
            }
            catch (Exception e)
            {
                LogError($"TTS processing failed: {e.Message}");
                OnError?.Invoke($"TTS failed: {e.Message}");
                request.onError?.Invoke(e.Message);
            }
            finally
            {
                isSpeaking = false;
                OnSpeechCompleted?.Invoke();
                request.onComplete?.Invoke();
            }
        }
        
        private IEnumerator GenerateAndPlaySpeech(TTSRequest request)
        {
            switch (ttsService)
            {
                case TTSService.OpenAI:
                    yield return StartCoroutine(GenerateOpenAISpeech(request));
                    break;
                    
                case TTSService.Unity:
                    yield return StartCoroutine(GenerateUnitySpeech(request));
                    break;
                    
                case TTSService.Local:
                    LogError("Local TTS not implemented yet");
                    OnError?.Invoke("Local TTS not available");
                    break;
            }
        }
        
        private IEnumerator GenerateOpenAISpeech(TTSRequest request)
        {
            if (string.IsNullOrEmpty(openAIApiKey) || openAIApiKey == "YOUR_OPENAI_API_KEY")
            {
                LogError("OpenAI API key not configured");
                OnError?.Invoke("OpenAI API key required");
                yield break;
            }
            
            LogDebug("Generating speech with OpenAI TTS...");
            
            var payload = new OpenAITTSPayload
            {
                model = GetModelString(model),
                input = request.text,
                voice = request.voice.ToString().ToLower(),
                speed = Mathf.Clamp(request.speed, 0.25f, 4.0f)
            };
            
            string jsonPayload = JsonUtility.ToJson(payload);
            
            using (UnityWebRequest webRequest = new UnityWebRequest("https://api.openai.com/v1/audio/speech", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", $"Bearer {openAIApiKey}");
                webRequest.timeout = (int)requestTimeout;
                
                yield return webRequest.SendWebRequest();
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    byte[] audioData = webRequest.downloadHandler.data;
                    yield return StartCoroutine(PlayAudioFromBytes(audioData));
                }
                else
                {
                    LogError($"OpenAI TTS request failed: {webRequest.error}");
                    OnError?.Invoke($"TTS generation failed: {webRequest.error}");
                }
            }
        }
        
        private IEnumerator GenerateUnitySpeech(TTSRequest request)
        {
            LogDebug("Using Unity built-in TTS (placeholder implementation)");
            
            // Unity doesn't have built-in TTS, so this is a placeholder
            // You would implement platform-specific TTS here (iOS/Android native TTS)
            
            // For now, we'll create a simple beep pattern based on text length
            float duration = Mathf.Min(request.text.Length * 0.1f, 5.0f);
            AudioClip beepClip = GenerateBeepClip(duration);
            
            if (audioSource != null && beepClip != null)
            {
                audioSource.clip = beepClip;
                audioSource.Play();
                
                yield return new WaitForSeconds(duration);
                
                DestroyImmediate(beepClip);
            }
        }
        
        private AudioClip GenerateBeepClip(float duration)
        {
            int sampleRate = 22050;
            int samples = Mathf.RoundToInt(duration * sampleRate);
            float[] audioData = new float[samples];
            
            float frequency = 440f; // A4 note
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                audioData[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * 0.1f;
                audioData[i] *= Mathf.Exp(-t * 2f); // Fade out
            }
            
            AudioClip clip = AudioClip.Create("TTSBeep", samples, 1, sampleRate, false);
            clip.SetData(audioData, 0);
            return clip;
        }
        
        private IEnumerator PlayAudioFromBytes(byte[] audioData)
        {
            if (audioSource == null)
            {
                LogError("No audio source available for playback");
                yield break;
            }
            
            // Save audio data to temporary file
            string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, "tts_audio.mp3");
            System.IO.File.WriteAllBytes(tempPath, audioData);
            
            // Load and play audio
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG))
            {
                yield return www.SendWebRequest();
                
                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                    if (clip != null)
                    {
                        audioSource.clip = clip;
                        audioSource.Play();
                        
                        // Wait for playback to complete
                        yield return new WaitForSeconds(clip.length);
                        
                        // Clean up
                        DestroyImmediate(clip);
                    }
                }
                else
                {
                    LogError($"Failed to load TTS audio: {www.error}");
                }
            }
            
            // Clean up temporary file
            if (System.IO.File.Exists(tempPath))
            {
                System.IO.File.Delete(tempPath);
            }
        }
        
        private string GetModelString(TTSModel model)
        {
            return model switch
            {
                TTSModel.TTS1 => "tts-1",
                TTSModel.TTS1_HD => "tts-1-hd",
                _ => "tts-1"
            };
        }
        
        /// <summary>
        /// Set OpenAI API key
        /// </summary>
        public void SetOpenAIApiKey(string apiKey)
        {
            openAIApiKey = apiKey;
            LogDebug("OpenAI API key updated");
        }
        
        /// <summary>
        /// Set default voice
        /// </summary>
        public void SetVoice(TTSVoice newVoice)
        {
            voice = newVoice;
            LogDebug($"Voice changed to: {newVoice}");
        }
        
        /// <summary>
        /// Set speech speed
        /// </summary>
        public void SetSpeechSpeed(float speed)
        {
            speechSpeed = Mathf.Clamp(speed, 0.25f, 4.0f);
            LogDebug($"Speech speed set to: {speechSpeed}");
        }
        
        /// <summary>
        /// Set volume
        /// </summary>
        public void SetVolume(float volume)
        {
            defaultVolume = Mathf.Clamp01(volume);
            if (audioSource != null)
            {
                audioSource.volume = defaultVolume;
            }
            LogDebug($"Volume set to: {defaultVolume}");
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[XREALTextToSpeech] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[XREALTextToSpeech] {message}");
        }
        
        private void OnDestroy()
        {
            StopSpeaking();
        }
        
        #region Editor Utilities
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void OnValidate()
        {
            speechSpeed = Mathf.Clamp(speechSpeed, 0.25f, 4.0f);
            defaultVolume = Mathf.Clamp01(defaultVolume);
            maxQueueSize = Mathf.Max(1, maxQueueSize);
            requestTimeout = Mathf.Max(1.0f, requestTimeout);
        }
        
        #endregion
    }
}
