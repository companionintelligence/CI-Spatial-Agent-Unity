using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// XREAL Speech to Text - Handles voice input and transcription
    /// Consolidated STT component optimized for XREAL devices
    /// </summary>
    public class XREALSpeechToText : MonoBehaviour
    {
        [Header("Recording Configuration")]
        [SerializeField] private int maxRecordingLength = 10; // seconds
        [SerializeField] private int sampleRate = 16000; // Optimal for speech recognition
        [SerializeField] private bool autoStartListening = false;
        [SerializeField] private bool enableVoiceActivation = true;
        [SerializeField] private float voiceThreshold = 0.01f;
        
        [Header("Transcription Service")]
        [SerializeField] private TranscriptionService transcriptionService = TranscriptionService.OpenAI;
        [SerializeField] private string openAIApiKey = "YOUR_OPENAI_API_KEY";
        [SerializeField] private string whisperModel = "whisper-1";
        [SerializeField] private float requestTimeout = 30.0f;
        
        [Header("Audio Processing")]
        [SerializeField] private bool enableNoiseReduction = true;
        [SerializeField] private bool enableAudioCompression = true;
        [SerializeField] private bool enableDebugLogging = true;
        
        [Header("Events")]
        public UnityEvent OnListeningStarted;
        public UnityEvent OnListeningStopped;
        public UnityEvent<string> OnTranscriptionComplete;
        public UnityEvent<string> OnError;
        
        public enum TranscriptionService
        {
            OpenAI,
            Local // For future local Whisper implementation
        }
        
        // State
        private bool isInitialized = false;
        private bool isListening = false;
        private bool isProcessing = false;
        private AudioClip recordedClip;
        private string selectedMicrophone;
        
        // Voice activation
        private float[] audioSamples;
        private bool voiceDetected = false;
        private float voiceDetectionTime = 0f;
        private const float voiceActivationDelay = 0.5f; // Wait for voice to start
        private const float silenceTimeout = 2.0f; // Stop after silence
        
        public bool IsInitialized => isInitialized;
        public bool IsListening => isListening;
        public bool IsProcessing => isProcessing;
        
        private void Start()
        {
            Initialize();
        }
        
        public void Initialize()
        {
            if (isInitialized) return;
            
            LogDebug("Initializing Speech to Text...");
            
            // Check for microphone permissions
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                StartCoroutine(RequestMicrophonePermission());
                return;
            }
            
            // Select best available microphone
            SelectMicrophone();
            
            isInitialized = true;
            LogDebug("Speech to Text initialized successfully");
            
            if (autoStartListening)
            {
                StartListening();
            }
        }
        
        private IEnumerator RequestMicrophonePermission()
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            
            if (Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                SelectMicrophone();
                isInitialized = true;
                LogDebug("Microphone permission granted");
                
                if (autoStartListening)
                {
                    StartListening();
                }
            }
            else
            {
                LogError("Microphone permission denied");
                OnError?.Invoke("Microphone permission denied");
            }
        }
        
        private void SelectMicrophone()
        {
            if (Microphone.devices.Length == 0)
            {
                LogError("No microphone devices found");
                OnError?.Invoke("No microphone devices found");
                return;
            }
            
            // Use the first available microphone (could be enhanced to select best quality)
            selectedMicrophone = Microphone.devices[0];
            LogDebug($"Selected microphone: {selectedMicrophone}");
        }
        
        /// <summary>
        /// Start listening for voice input
        /// </summary>
        public void StartListening()
        {
            if (!isInitialized)
            {
                LogError("STT not initialized");
                return;
            }
            
            if (isListening)
            {
                LogDebug("Already listening");
                return;
            }
            
            if (string.IsNullOrEmpty(selectedMicrophone))
            {
                LogError("No microphone selected");
                OnError?.Invoke("No microphone available");
                return;
            }
            
            LogDebug("Starting to listen...");
            
            // Start recording
            recordedClip = Microphone.Start(selectedMicrophone, false, maxRecordingLength, sampleRate);
            
            if (recordedClip == null)
            {
                LogError("Failed to start microphone recording");
                OnError?.Invoke("Failed to start recording");
                return;
            }
            
            isListening = true;
            voiceDetected = false;
            voiceDetectionTime = 0f;
            
            OnListeningStarted?.Invoke();
            
            if (enableVoiceActivation)
            {
                StartCoroutine(MonitorVoiceActivation());
            }
            else
            {
                // If voice activation is disabled, stop after max recording length
                StartCoroutine(StopListeningAfterTimeout());
            }
        }
        
        /// <summary>
        /// Stop listening and process the recorded audio
        /// </summary>
        public void StopListening()
        {
            if (!isListening) return;
            
            LogDebug("Stopping listening...");
            
            isListening = false;
            Microphone.End(selectedMicrophone);
            
            OnListeningStopped?.Invoke();
            
            if (recordedClip != null)
            {
                StartCoroutine(ProcessRecordedAudio());
            }
        }
        
        /// <summary>
        /// Toggle listening state
        /// </summary>
        public void ToggleListening()
        {
            if (isListening)
            {
                StopListening();
            }
            else
            {
                StartListening();
            }
        }
        
        private IEnumerator MonitorVoiceActivation()
        {
            audioSamples = new float[sampleRate / 10]; // 0.1 second chunks
            
            while (isListening)
            {
                // Get current microphone position
                int micPosition = Microphone.GetPosition(selectedMicrophone);
                
                if (micPosition > 0 && recordedClip.channels > 0)
                {
                    // Read audio data
                    int startPosition = Mathf.Max(0, micPosition - audioSamples.Length);
                    recordedClip.GetData(audioSamples, startPosition);
                    
                    // Calculate RMS (Root Mean Square) for volume detection
                    float rms = CalculateRMS(audioSamples);
                    
                    if (rms > voiceThreshold)
                    {
                        if (!voiceDetected)
                        {
                            voiceDetected = true;
                            voiceDetectionTime = Time.time;
                            LogDebug("Voice detected");
                        }
                    }
                    else if (voiceDetected)
                    {
                        // Check for silence timeout
                        if (Time.time - voiceDetectionTime > silenceTimeout)
                        {
                            LogDebug("Silence detected, stopping listening");
                            StopListening();
                            break;
                        }
                    }
                }
                
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        private IEnumerator StopListeningAfterTimeout()
        {
            yield return new WaitForSeconds(maxRecordingLength);
            
            if (isListening)
            {
                LogDebug("Recording timeout reached");
                StopListening();
            }
        }
        
        private float CalculateRMS(float[] samples)
        {
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                sum += samples[i] * samples[i];
            }
            return Mathf.Sqrt(sum / samples.Length);
        }
        
        private IEnumerator ProcessRecordedAudio()
        {
            if (recordedClip == null)
            {
                LogError("No recorded audio to process");
                yield break;
            }
            
            isProcessing = true;
            LogDebug("Processing recorded audio...");
            
            try
            {
                // Convert audio to WAV format
                byte[] audioData = ConvertAudioToWAV(recordedClip);
                
                if (audioData == null || audioData.Length == 0)
                {
                    LogError("Failed to convert audio data");
                    OnError?.Invoke("Audio conversion failed");
                    yield break;
                }
                
                // Send to transcription service
                yield return StartCoroutine(SendAudioForTranscription(audioData));
            }
            finally
            {
                isProcessing = false;
                
                // Clean up recorded clip
                if (recordedClip != null)
                {
                    DestroyImmediate(recordedClip);
                    recordedClip = null;
                }
            }
        }
        
        private byte[] ConvertAudioToWAV(AudioClip clip)
        {
            try
            {
                // Get audio data
                float[] samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);
                
                // Convert to 16-bit PCM
                short[] intData = new short[samples.Length];
                for (int i = 0; i < samples.Length; i++)
                {
                    intData[i] = (short)(samples[i] * 32767f);
                }
                
                // Create WAV file in memory
                using (MemoryStream stream = new MemoryStream())
                {
                    // WAV header
                    WriteWAVHeader(stream, clip);
                    
                    // Audio data
                    foreach (short sample in intData)
                    {
                        byte[] bytes = BitConverter.GetBytes(sample);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                    
                    return stream.ToArray();
                }
            }
            catch (Exception e)
            {
                LogError($"Failed to convert audio to WAV: {e.Message}");
                return null;
            }
        }
        
        private void WriteWAVHeader(MemoryStream stream, AudioClip clip)
        {
            int sampleCount = clip.samples * clip.channels;
            int byteRate = clip.frequency * clip.channels * 2; // 16-bit
            
            // RIFF chunk
            stream.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"), 0, 4);
            stream.Write(BitConverter.GetBytes(36 + sampleCount * 2), 0, 4);
            stream.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"), 0, 4);
            
            // fmt chunk
            stream.Write(System.Text.Encoding.UTF8.GetBytes("fmt "), 0, 4);
            stream.Write(BitConverter.GetBytes(16), 0, 4); // chunk size
            stream.Write(BitConverter.GetBytes((ushort)1), 0, 2); // audio format (PCM)
            stream.Write(BitConverter.GetBytes((ushort)clip.channels), 0, 2);
            stream.Write(BitConverter.GetBytes(clip.frequency), 0, 4);
            stream.Write(BitConverter.GetBytes(byteRate), 0, 4);
            stream.Write(BitConverter.GetBytes((ushort)(clip.channels * 2)), 0, 2); // block align
            stream.Write(BitConverter.GetBytes((ushort)16), 0, 2); // bits per sample
            
            // data chunk
            stream.Write(System.Text.Encoding.UTF8.GetBytes("data"), 0, 4);
            stream.Write(BitConverter.GetBytes(sampleCount * 2), 0, 4);
        }
        
        private IEnumerator SendAudioForTranscription(byte[] audioData)
        {
            switch (transcriptionService)
            {
                case TranscriptionService.OpenAI:
                    yield return StartCoroutine(SendToOpenAI(audioData));
                    break;
                case TranscriptionService.Local:
                    LogError("Local transcription not implemented yet");
                    OnError?.Invoke("Local transcription not available");
                    break;
            }
        }
        
        private IEnumerator SendToOpenAI(byte[] audioData)
        {
            if (string.IsNullOrEmpty(openAIApiKey) || openAIApiKey == "YOUR_OPENAI_API_KEY")
            {
                LogError("OpenAI API key not configured");
                OnError?.Invoke("OpenAI API key required");
                yield break;
            }
            
            LogDebug("Sending audio to OpenAI Whisper...");
            
            // Create multipart form data
            var formData = new System.Collections.Generic.List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", audioData, "audio.wav", "audio/wav"),
                new MultipartFormDataSection("model", whisperModel)
            };
            
            using (UnityWebRequest request = UnityWebRequest.Post("https://api.openai.com/v1/audio/transcriptions", formData))
            {
                request.SetRequestHeader("Authorization", $"Bearer {openAIApiKey}");
                request.timeout = (int)requestTimeout;
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        // Parse response
                        var response = JsonUtility.FromJson<OpenAITranscriptionResponse>(request.downloadHandler.text);
                        
                        if (!string.IsNullOrEmpty(response.text))
                        {
                            LogDebug($"Transcription: {response.text}");
                            OnTranscriptionComplete?.Invoke(response.text);
                        }
                        else
                        {
                            LogError("Empty transcription response");
                            OnError?.Invoke("Empty transcription received");
                        }
                    }
                    catch (Exception e)
                    {
                        LogError($"Failed to parse transcription response: {e.Message}");
                        OnError?.Invoke("Transcription parsing failed");
                    }
                }
                else
                {
                    LogError($"Transcription request failed: {request.error}");
                    OnError?.Invoke($"Transcription failed: {request.error}");
                }
            }
        }
        
        [Serializable]
        private class OpenAITranscriptionResponse
        {
            public string text;
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
        /// Set voice activation threshold
        /// </summary>
        public void SetVoiceThreshold(float threshold)
        {
            voiceThreshold = Mathf.Clamp01(threshold);
            LogDebug($"Voice threshold set to: {voiceThreshold}");
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[XREALSpeechToText] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[XREALSpeechToText] {message}");
        }
        
        private void OnDestroy()
        {
            if (isListening)
            {
                StopListening();
            }
            
            if (recordedClip != null)
            {
                DestroyImmediate(recordedClip);
            }
        }
        
        #region Editor Utilities
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void OnValidate()
        {
            maxRecordingLength = Mathf.Clamp(maxRecordingLength, 1, 60);
            sampleRate = Mathf.Clamp(sampleRate, 8000, 48000);
            voiceThreshold = Mathf.Clamp01(voiceThreshold);
            requestTimeout = Mathf.Max(1.0f, requestTimeout);
        }
        
        #endregion
    }
}
