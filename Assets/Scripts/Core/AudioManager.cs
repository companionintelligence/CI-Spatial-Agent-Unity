using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Manages audio input/output for the XREAL AI assistant.
    /// Handles microphone capture, audio preprocessing, and integration with STT/TTS systems.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("Audio Configuration")]
        [SerializeField] private int sampleRate = 16000;
        [SerializeField] private float recordingTime = 5.0f;
        [SerializeField] private float volumeThreshold = 0.01f;
        [SerializeField] private bool enableNoiseReduction = true;
        [SerializeField] private bool enableBackgroundRecording = false;
        
        [Header("Privacy Settings")]
        [SerializeField] private bool showRecordingIndicator = true;
        [SerializeField] private GameObject recordingIndicatorUI;
        
        // Audio components
        private AudioSource audioSource;
        private AudioClip recordingClip;
        private string microphoneDevice;
        private bool isListening = false;
        private bool isInitialized = false;
        
        // Audio processing
        private Queue<float[]> audioBufferQueue = new Queue<float[]>();
        private float[] currentSamples;
        private int bufferSize = 1024;
        
        // Events
        public event System.Action<AudioClip> OnAudioCaptured;
        public event System.Action<float> OnVolumeChanged;
        
        public bool IsInitialized => isInitialized;
        public bool IsListening => isListening;
        
        public void Initialize()
        {
            Debug.Log("[AudioManager] Initializing audio system...");
            
            // Setup audio source
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            // Check microphone permissions and availability
            StartCoroutine(RequestMicrophonePermission());
        }
        
        private IEnumerator RequestMicrophonePermission()
        {
            // Request microphone permission on Android
            #if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                
                // Wait for permission response
                yield return new WaitForSeconds(1.0f);
                
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
                {
                    Debug.LogError("[AudioManager] Microphone permission denied!");
                    yield break;
                }
            }
            #endif
            
            // Get available microphone devices
            if (Microphone.devices.Length > 0)
            {
                microphoneDevice = Microphone.devices[0];
                Debug.Log($"[AudioManager] Using microphone: {microphoneDevice}");
                
                // Get microphone capabilities
                int minFreq, maxFreq;
                Microphone.GetDeviceCaps(microphoneDevice, out minFreq, out maxFreq);
                
                // Adjust sample rate if needed
                if (maxFreq > 0 && sampleRate > maxFreq)
                {
                    sampleRate = maxFreq;
                }
                else if (minFreq > sampleRate)
                {
                    sampleRate = minFreq;
                }
                
                Debug.Log($"[AudioManager] Audio settings - Sample Rate: {sampleRate}Hz, Min: {minFreq}Hz, Max: {maxFreq}Hz");
                
                isInitialized = true;
                
                // Start background recording if enabled
                if (enableBackgroundRecording)
                {
                    StartBackgroundRecording();
                }
            }
            else
            {
                Debug.LogError("[AudioManager] No microphone devices found!");
            }
        }
        
        public void StartListening()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[AudioManager] Cannot start listening - system not initialized");
                return;
            }
            
            if (isListening)
            {
                Debug.LogWarning("[AudioManager] Already listening");
                return;
            }
            
            Debug.Log("[AudioManager] Starting voice recording...");
            
            // Show recording indicator
            if (showRecordingIndicator && recordingIndicatorUI != null)
            {
                recordingIndicatorUI.SetActive(true);
            }
            
            // Start recording
            recordingClip = Microphone.Start(microphoneDevice, false, (int)recordingTime, sampleRate);
            isListening = true;
            
            // Start monitoring audio levels
            StartCoroutine(MonitorAudioLevels());
        }
        
        public void StopListening()
        {
            if (!isListening)
                return;
                
            Debug.Log("[AudioManager] Stopping voice recording...");
            
            Microphone.End(microphoneDevice);
            isListening = false;
            
            // Hide recording indicator
            if (recordingIndicatorUI != null)
            {
                recordingIndicatorUI.SetActive(false);
            }
            
            // Process the recorded audio
            if (recordingClip != null)
            {
                ProcessRecordedAudio(recordingClip);
            }
        }
        
        private void StartBackgroundRecording()
        {
            if (!enableBackgroundRecording || isListening)
                return;
                
            Debug.Log("[AudioManager] Starting background audio capture for life logging...");
            
            // Create longer recording clip for background monitoring
            recordingClip = Microphone.Start(microphoneDevice, true, 30, sampleRate);
            
            StartCoroutine(BackgroundAudioProcessing());
        }
        
        private IEnumerator BackgroundAudioProcessing()
        {
            while (enableBackgroundRecording && isInitialized)
            {
                yield return new WaitForSeconds(5.0f);
                
                if (recordingClip != null && Microphone.IsRecording(microphoneDevice))
                {
                    // Analyze background audio for interesting events
                    float[] samples = new float[sampleRate * 2]; // 2 seconds of audio
                    recordingClip.GetData(samples, 0);
                    
                    // Check for speech or significant audio events
                    float averageVolume = CalculateAverageVolume(samples);
                    if (averageVolume > volumeThreshold * 2) // Higher threshold for background
                    {
                        Debug.Log($"[AudioManager] Background audio event detected - Volume: {averageVolume:F3}");
                        // TODO: Send to AI for analysis
                    }
                }
            }
        }
        
        private IEnumerator MonitorAudioLevels()
        {
            while (isListening)
            {
                if (recordingClip != null && Microphone.IsRecording(microphoneDevice))
                {
                    // Get current microphone position
                    int micPosition = Microphone.GetPosition(microphoneDevice);
                    
                    if (micPosition > 0)
                    {
                        // Get recent audio data
                        int sampleCount = Mathf.Min(bufferSize, micPosition);
                        float[] samples = new float[sampleCount];
                        recordingClip.GetData(samples, micPosition - sampleCount);
                        
                        // Calculate volume level
                        float volume = CalculateAverageVolume(samples);
                        OnVolumeChanged?.Invoke(volume);
                        
                        // Apply noise reduction if enabled
                        if (enableNoiseReduction)
                        {
                            ApplyNoiseReduction(samples);
                        }
                        
                        // Buffer audio data for processing
                        audioBufferQueue.Enqueue(samples);
                        
                        // Limit buffer size
                        while (audioBufferQueue.Count > 10)
                        {
                            audioBufferQueue.Dequeue();
                        }
                    }
                }
                
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        private void ProcessRecordedAudio(AudioClip clip)
        {
            if (clip == null)
                return;
                
            Debug.Log($"[AudioManager] Processing audio clip - Length: {clip.length:F2}s, Samples: {clip.samples}");
            
            // Get all audio data
            float[] samples = new float[clip.samples];
            clip.GetData(samples, 0);
            
            // Apply preprocessing
            if (enableNoiseReduction)
            {
                ApplyNoiseReduction(samples);
            }
            
            // Check if audio contains speech
            if (ContainsSpeech(samples))
            {
                Debug.Log("[AudioManager] Speech detected in audio clip");
                OnAudioCaptured?.Invoke(clip);
            }
            else
            {
                Debug.Log("[AudioManager] No speech detected in audio clip");
            }
        }
        
        private float CalculateAverageVolume(float[] samples)
        {
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                sum += Mathf.Abs(samples[i]);
            }
            return sum / samples.Length;
        }
        
        private bool ContainsSpeech(float[] samples)
        {
            float averageVolume = CalculateAverageVolume(samples);
            
            // Simple speech detection based on volume and variability
            if (averageVolume < volumeThreshold)
                return false;
                
            // Check for volume variability (speech has more variation than steady noise)
            float variance = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float deviation = Mathf.Abs(samples[i]) - averageVolume;
                variance += deviation * deviation;
            }
            variance /= samples.Length;
            
            // Speech typically has higher variance than background noise
            return variance > volumeThreshold * 0.1f;
        }
        
        private void ApplyNoiseReduction(float[] samples)
        {
            // Simple noise gate - reduce quiet sounds
            for (int i = 0; i < samples.Length; i++)
            {
                if (Mathf.Abs(samples[i]) < volumeThreshold * 0.1f)
                {
                    samples[i] *= 0.1f; // Reduce noise by 90%
                }
            }
        }
        
        public void PlayAudio(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.clip = clip;
                audioSource.Play();
            }
        }
        
        public void SetVolume(float volume)
        {
            if (audioSource != null)
            {
                audioSource.volume = Mathf.Clamp01(volume);
            }
        }
        
        public float[] GetCurrentAudioBuffer()
        {
            if (audioBufferQueue.Count > 0)
            {
                return audioBufferQueue.Dequeue();
            }
            return null;
        }
        
        public void Cleanup()
        {
            Debug.Log("[AudioManager] Cleaning up audio system...");
            
            if (isListening)
            {
                StopListening();
            }
            
            if (Microphone.IsRecording(microphoneDevice))
            {
                Microphone.End(microphoneDevice);
            }
            
            isInitialized = false;
        }
        
        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
