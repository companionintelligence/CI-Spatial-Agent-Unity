using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace CompanionIntelligence.Tests
{
    /// <summary>
    /// Mock implementations for testing XREAL Vision System components
    /// Provides predictable, testable behavior without external dependencies
    /// </summary>
    namespace Mocks
    {
        /// <summary>
        /// Mock implementation of IXREALCameraManager for testing
        /// </summary>
        public class MockCameraManager : MonoBehaviour, CompanionIntelligence.Core.IXREALCameraManager
        {
            public bool IsInitialized { get; private set; } = true;
            public bool HasCapturedImage { get; private set; } = false;
            
            public UnityEvent OnCaptureStarted { get; private set; } = new UnityEvent();
            public UnityEvent<Texture2D> OnImageCaptured { get; private set; } = new UnityEvent<Texture2D>();
            public UnityEvent<string> OnError { get; private set; } = new UnityEvent<string>();
            
            [Header("Mock Settings")]
            [SerializeField] private bool simulateSuccess = true;
            [SerializeField] private float simulatedDelay = 0.1f;
            [SerializeField] private Vector2Int mockImageSize = new Vector2Int(512, 512);
            
            private Texture2D mockImage;
            
            public void Initialize()
            {
                IsInitialized = true;
                CreateMockImage();
            }
            
            public void CaptureImage()
            {
                StartCoroutine(SimulateCaptureCoroutine());
            }
            
            private IEnumerator SimulateCaptureCoroutine()
            {
                OnCaptureStarted?.Invoke();
                
                yield return new WaitForSeconds(simulatedDelay);
                
                if (simulateSuccess)
                {
                    HasCapturedImage = true;
                    OnImageCaptured?.Invoke(mockImage);
                }
                else
                {
                    OnError?.Invoke("Mock capture failed");
                }
            }
            
            public Texture2D GetLastCapturedImage()
            {
                return HasCapturedImage ? mockImage : null;
            }
            
            public Texture2D GetLastCapturedImageCopy()
            {
                if (!HasCapturedImage) return null;
                
                var copy = new Texture2D(mockImage.width, mockImage.height, mockImage.format, false);
                copy.SetPixels(mockImage.GetPixels());
                copy.Apply();
                return copy;
            }
            
            public void ClearCapturedImage()
            {
                HasCapturedImage = false;
            }
            
            public void SetCaptureResolution(int width, int height)
            {
                mockImageSize = new Vector2Int(width, height);
                CreateMockImage();
            }
            
            private void CreateMockImage()
            {
                if (mockImage != null)
                {
                    DestroyImmediate(mockImage);
                }
                
                mockImage = new Texture2D(mockImageSize.x, mockImageSize.y, TextureFormat.RGB24, false);
                
                // Fill with a test pattern
                var pixels = new Color[mockImageSize.x * mockImageSize.y];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = Color.gray;
                }
                mockImage.SetPixels(pixels);
                mockImage.Apply();
            }
            
            private void OnDestroy()
            {
                if (mockImage != null)
                {
                    DestroyImmediate(mockImage);
                }
            }
        }
        
        /// <summary>
        /// Mock implementation of IXREALOllamaConnector for testing
        /// </summary>
        public class MockOllamaConnector : MonoBehaviour, CompanionIntelligence.Core.IXREALOllamaConnector
        {
            public bool IsInitialized { get; private set; } = true;
            public bool IsConnected { get; private set; } = true;
            public bool IsProcessing { get; private set; } = false;
            public string ServerAddress => "mock://localhost:11434";
            
            public UnityEvent OnProcessingStarted { get; private set; } = new UnityEvent();
            public UnityEvent<string> OnResponseReceived { get; private set; } = new UnityEvent<string>();
            public UnityEvent<string> OnError { get; private set; } = new UnityEvent<string>();
            
            [Header("Mock Settings")]
            [SerializeField] private bool simulateSuccess = true;
            [SerializeField] private float simulatedDelay = 1.0f;
            [SerializeField] private string[] mockResponses = {
                "I can see a test image with various objects.",
                "This appears to be a mock response for testing.",
                "The image shows a simulated environment."
            };
            
            private int responseIndex = 0;
            
            public void Initialize()
            {
                IsInitialized = true;
                IsConnected = true;
            }
            
            public void SendVisionRequest(Texture2D image, string prompt)
            {
                StartCoroutine(SimulateVisionRequestCoroutine(prompt));
            }
            
            public void SendTextRequest(string prompt)
            {
                StartCoroutine(SimulateTextRequestCoroutine(prompt));
            }
            
            private IEnumerator SimulateVisionRequestCoroutine(string prompt)
            {
                IsProcessing = true;
                OnProcessingStarted?.Invoke();
                
                yield return new WaitForSeconds(simulatedDelay);
                
                IsProcessing = false;
                
                if (simulateSuccess)
                {
                    string response = mockResponses[responseIndex % mockResponses.Length];
                    responseIndex++;
                    OnResponseReceived?.Invoke($"Vision: {response} (Prompt: {prompt})");
                }
                else
                {
                    OnError?.Invoke("Mock vision request failed");
                }
            }
            
            private IEnumerator SimulateTextRequestCoroutine(string prompt)
            {
                IsProcessing = true;
                OnProcessingStarted?.Invoke();
                
                yield return new WaitForSeconds(simulatedDelay * 0.5f);
                
                IsProcessing = false;
                
                if (simulateSuccess)
                {
                    OnResponseReceived?.Invoke($"Text response to: {prompt}");
                }
                else
                {
                    OnError?.Invoke("Mock text request failed");
                }
            }
        }
        
        /// <summary>
        /// Mock implementation of IXREALSpeechToText for testing
        /// </summary>
        public class MockSpeechToText : MonoBehaviour, CompanionIntelligence.Core.IXREALSpeechToText
        {
            public bool IsInitialized { get; private set; } = true;
            public bool IsListening { get; private set; } = false;
            public bool IsProcessing { get; private set; } = false;
            
            public UnityEvent OnListeningStarted { get; private set; } = new UnityEvent();
            public UnityEvent OnListeningStopped { get; private set; } = new UnityEvent();
            public UnityEvent<string> OnTranscriptionComplete { get; private set; } = new UnityEvent<string>();
            public UnityEvent<string> OnError { get; private set; } = new UnityEvent<string>();
            
            [Header("Mock Settings")]
            [SerializeField] private bool simulateSuccess = true;
            [SerializeField] private float simulatedDelay = 2.0f;
            [SerializeField] private string[] mockTranscriptions = {
                "What am I looking at?",
                "Describe this scene",
                "Tell me about this image"
            };
            
            private int transcriptionIndex = 0;
            
            public void Initialize()
            {
                IsInitialized = true;
            }
            
            public void StartListening()
            {
                if (!IsListening)
                {
                    IsListening = true;
                    OnListeningStarted?.Invoke();
                    StartCoroutine(SimulateListeningCoroutine());
                }
            }
            
            public void StopListening()
            {
                if (IsListening)
                {
                    IsListening = false;
                    OnListeningStopped?.Invoke();
                }
            }
            
            public void ToggleListening()
            {
                if (IsListening)
                    StopListening();
                else
                    StartListening();
            }
            
            private IEnumerator SimulateListeningCoroutine()
            {
                yield return new WaitForSeconds(simulatedDelay);
                
                if (IsListening)
                {
                    IsProcessing = true;
                    yield return new WaitForSeconds(0.5f);
                    
                    IsProcessing = false;
                    IsListening = false;
                    
                    if (simulateSuccess)
                    {
                        string transcription = mockTranscriptions[transcriptionIndex % mockTranscriptions.Length];
                        transcriptionIndex++;
                        OnTranscriptionComplete?.Invoke(transcription);
                    }
                    else
                    {
                        OnError?.Invoke("Mock transcription failed");
                    }
                    
                    OnListeningStopped?.Invoke();
                }
            }
        }
        
        /// <summary>
        /// Mock implementation of IXREALTextToSpeech for testing
        /// </summary>
        public class MockTextToSpeech : MonoBehaviour, CompanionIntelligence.Core.IXREALTextToSpeech
        {
            public bool IsInitialized { get; private set; } = true;
            public bool IsPlaying { get; private set; } = false;
            
            public UnityEvent OnSpeechStarted { get; private set; } = new UnityEvent();
            public UnityEvent OnSpeechCompleted { get; private set; } = new UnityEvent();
            public UnityEvent<string> OnError { get; private set; } = new UnityEvent<string>();
            
            [Header("Mock Settings")]
            [SerializeField] private bool simulateSuccess = true;
            [SerializeField] private float simulatedDelay = 1.5f;
            
            public void Initialize()
            {
                IsInitialized = true;
            }
            
            public void SpeakText(string text)
            {
                if (!IsPlaying)
                {
                    StartCoroutine(SimulateSpeechCoroutine(text));
                }
            }
            
            public void StopSpeaking()
            {
                if (IsPlaying)
                {
                    StopAllCoroutines();
                    IsPlaying = false;
                    OnSpeechCompleted?.Invoke();
                }
            }
            
            public void SetVoiceSettings(string voice, float speed, float pitch)
            {
                // Mock implementation - just log the settings
                Debug.Log($"[MockTTS] Voice settings: {voice}, Speed: {speed}, Pitch: {pitch}");
            }
            
            private IEnumerator SimulateSpeechCoroutine(string text)
            {
                IsPlaying = true;
                OnSpeechStarted?.Invoke();
                
                yield return new WaitForSeconds(simulatedDelay);
                
                IsPlaying = false;
                
                if (simulateSuccess)
                {
                    OnSpeechCompleted?.Invoke();
                }
                else
                {
                    OnError?.Invoke("Mock speech synthesis failed");
                }
            }
        }
    }
}
