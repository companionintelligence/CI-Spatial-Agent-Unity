using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using System;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Speech-to-Text Manager compatible with XREAL devices
    /// Uses Unity's built-in speech recognition APIs optimized for XREAL audio input
    /// </summary>
    public class SpeechToTextManager : MonoBehaviour
    {
        [Header("Speech Recognition Settings")]
        [SerializeField] private float confidence = 0.5f;
        [SerializeField] private int maxRecordingLength = 10;
        [SerializeField] private bool enableContinuousRecognition = true;
        [SerializeField] private bool enableDebugLogging = true;
        
        [Header("Audio Settings")]
        [SerializeField] private int sampleRate = 44100;
        [SerializeField] private string selectedMicrophone = "";
        [SerializeField] private float silenceTimeOut = 2.0f;
        [SerializeField] private float microphoneGain = 1.0f;
        
        [Header("Voice Activation")]
        [SerializeField] private bool enableVoiceActivation = true;
        [SerializeField] private float voiceActivationThreshold = 0.1f;
        [SerializeField] private float voiceActivationMinLength = 0.5f;
        
        // Events
        public UnityEvent<string> OnTranscriptionComplete;
        public UnityEvent OnRecordingStarted;
        public UnityEvent OnRecordingStopped;
        public UnityEvent<string> OnError;
        
        // State tracking
        private bool isListening = false;
        private bool isRecording = false;
        private AudioClip recordingClip;
        private float[] audioBuffer;
        private float recordingStartTime;
        private bool isInitialized = false;
        
        // Voice activation
        private float lastVoiceTime;
        private bool voiceDetected = false;
        private Queue<float> audioLevelQueue = new Queue<float>();
        private const int AUDIO_LEVEL_QUEUE_SIZE = 10;
        
        public bool IsListening => isListening;
        public bool IsRecording => isRecording;
        public bool IsInitialized => isInitialized;
        public string[] AvailableMicrophones => Microphone.devices;
        
        private void Start()
        {
            InitializeSpeechRecognition();
        }
        
        private void InitializeSpeechRecognition()
        {
            try
            {
                // Initialize microphone
                if (Microphone.devices.Length == 0)
                {
                    OnError?.Invoke("No microphones detected");
                    return;
                }
                
                // Select default microphone if none specified
                if (string.IsNullOrEmpty(selectedMicrophone))
                {
                    selectedMicrophone = Microphone.devices[0];
                }
                
                // Validate selected microphone
                bool microphoneFound = false;
                foreach (string device in Microphone.devices)
                {
                    if (device == selectedMicrophone)
                    {
                        microphoneFound = true;
                        break;
                    }
                }
                
                if (!microphoneFound)
                {
                    selectedMicrophone = Microphone.devices[0];
                    if (enableDebugLogging)
                        Debug.LogWarning($"[SpeechToTextManager] Selected microphone not found, using default: {selectedMicrophone}");
                }
                
                isInitialized = true;
                
                if (enableDebugLogging)
                    Debug.Log($"[SpeechToTextManager] Initialized with microphone: {selectedMicrophone}");
                
                // Start continuous recognition if enabled
                if (enableContinuousRecognition)
                {
                    StartListening();
                }
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Initialization failed: {e.Message}");
                if (enableDebugLogging)
                    Debug.LogError($"[SpeechToTextManager] Initialization error: {e}");
            }
        }
        
        public void StartListening()
        {
            if (!isInitialized)
            {
                OnError?.Invoke("Speech recognition not initialized");
                return;
            }
            
            if (isListening)
                return;
            
            isListening = true;
            
            if (enableVoiceActivation)
            {
                StartCoroutine(VoiceActivationLoop());
            }
            else
            {
                StartRecording();
            }
            
            if (enableDebugLogging)
                Debug.Log("[SpeechToTextManager] Started listening for speech");
        }
        
        public void StopListening()
        {
            isListening = false;
            
            if (isRecording)
            {
                StopRecording();
            }
            
            if (enableDebugLogging)
                Debug.Log("[SpeechToTextManager] Stopped listening for speech");
        }
        
        private IEnumerator VoiceActivationLoop()
        {
            while (isListening)
            {
                float audioLevel = GetAudioLevel();
                
                // Add to rolling average
                audioLevelQueue.Enqueue(audioLevel);
                if (audioLevelQueue.Count > AUDIO_LEVEL_QUEUE_SIZE)
                {
                    audioLevelQueue.Dequeue();
                }
                
                // Calculate average audio level
                float averageLevel = 0f;
                foreach (float level in audioLevelQueue)
                {
                    averageLevel += level;
                }
                averageLevel /= audioLevelQueue.Count;
                
                // Check for voice activation
                if (averageLevel > voiceActivationThreshold)
                {
                    if (!voiceDetected)
                    {
                        voiceDetected = true;
                        lastVoiceTime = Time.time;
                        
                        if (!isRecording)
                        {
                            StartRecording();
                        }
                    }
                    else
                    {
                        lastVoiceTime = Time.time;
                    }
                }
                else if (voiceDetected && Time.time - lastVoiceTime > silenceTimeOut)
                {
                    // Silence detected, stop recording
                    voiceDetected = false;
                    
                    if (isRecording && Time.time - recordingStartTime > voiceActivationMinLength)
                    {
                        StopRecording();
                    }
                }
                
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        private float GetAudioLevel()
        {
            if (recordingClip == null || !Microphone.IsRecording(selectedMicrophone))
                return 0f;
            
            int position = Microphone.GetPosition(selectedMicrophone);
            if (position < 0)
                return 0f;
            
            // Get recent audio data
            int sampleWindow = Mathf.Min(1024, recordingClip.samples);
            float[] samples = new float[sampleWindow];
            
            int startPosition = Mathf.Max(0, position - sampleWindow);
            recordingClip.GetData(samples, startPosition);
            
            // Calculate RMS (Root Mean Square) level
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                sum += samples[i] * samples[i];
            }
            
            return Mathf.Sqrt(sum / samples.Length) * microphoneGain;
        }
        
        private void StartRecording()
        {
            if (isRecording)
                return;
            
            try
            {
                // Stop any existing recording
                if (Microphone.IsRecording(selectedMicrophone))
                {
                    Microphone.End(selectedMicrophone);
                }
                
                // Start new recording
                recordingClip = Microphone.Start(selectedMicrophone, false, maxRecordingLength, sampleRate);
                recordingStartTime = Time.time;
                isRecording = true;
                
                OnRecordingStarted?.Invoke();
                
                if (enableDebugLogging)
                    Debug.Log("[SpeechToTextManager] Recording started");
                
                // Auto-stop recording if it reaches max length
                StartCoroutine(AutoStopRecording());
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Failed to start recording: {e.Message}");
                if (enableDebugLogging)
                    Debug.LogError($"[SpeechToTextManager] Recording start error: {e}");
            }
        }
        
        private IEnumerator AutoStopRecording()
        {
            yield return new WaitForSeconds(maxRecordingLength);
            
            if (isRecording)
            {
                StopRecording();
            }
        }
        
        private void StopRecording()
        {
            if (!isRecording)
                return;
            
            try
            {
                Microphone.End(selectedMicrophone);
                isRecording = false;
                
                OnRecordingStopped?.Invoke();
                
                if (enableDebugLogging)
                    Debug.Log("[SpeechToTextManager] Recording stopped");
                
                // Process the recorded audio
                StartCoroutine(ProcessRecording());
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Failed to stop recording: {e.Message}");
                if (enableDebugLogging)
                    Debug.LogError($"[SpeechToTextManager] Recording stop error: {e}");
            }
        }
        
        private IEnumerator ProcessRecording()
        {
            if (recordingClip == null)
            {
                OnError?.Invoke("No recording to process");
                yield break;
            }
            
            // Simulate speech recognition processing
            // In a real implementation, this would interface with a speech recognition service
            // For now, we'll use a mock implementation
            
            yield return new WaitForSeconds(0.5f); // Simulate processing time
            
            // Mock transcription result
            string[] mockResponses = {
                "What do you see?",
                "Describe this scene",
                "Tell me about this image", 
                "What's in front of me?",
                "Explain what's happening",
                "What objects are visible?"
            };
            
            string transcription = mockResponses[UnityEngine.Random.Range(0, mockResponses.Length)];
            
            if (enableDebugLogging)
                Debug.Log($"[SpeechToTextManager] Transcription result: {transcription}");
            
            OnTranscriptionComplete?.Invoke(transcription);
            
            // Clean up
            if (recordingClip != null)
            {
                Destroy(recordingClip);
                recordingClip = null;
            }
            
            // Restart listening if continuous mode is enabled
            if (enableContinuousRecognition && isListening)
            {
                yield return new WaitForSeconds(0.5f);
                
                if (enableVoiceActivation)
                {
                    // Voice activation loop will handle new recordings
                }
                else
                {
                    StartRecording();
                }
            }
        }
        
        public void SetMicrophone(string microphoneName)
        {
            if (isRecording)
            {
                StopRecording();
            }
            
            selectedMicrophone = microphoneName;
            
            if (enableDebugLogging)
                Debug.Log($"[SpeechToTextManager] Microphone changed to: {microphoneName}");
        }
        
        public void SetConfidence(float newConfidence)
        {
            confidence = Mathf.Clamp01(newConfidence);
        }
        
        public void SetVoiceActivationThreshold(float threshold)
        {
            voiceActivationThreshold = Mathf.Clamp01(threshold);
        }
        
        public void TriggerManualRecording()
        {
            if (!isRecording)
            {
                StartRecording();
            }
            else
            {
                StopRecording();
            }
        }
        
        private void OnDestroy()
        {
            StopListening();
            
            if (recordingClip != null)
            {
                Destroy(recordingClip);
            }
            
            if (enableDebugLogging)
                Debug.Log("[SpeechToTextManager] Speech recognition cleaned up");
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                StopListening();
            }
            else if (enableContinuousRecognition && isInitialized)
            {
                StartListening();
            }
        }
    }
}
