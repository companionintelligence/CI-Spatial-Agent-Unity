using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using CompanionIntelligence.Core;
using UnityEngine.Events;

namespace CompanionIntelligence.Tests
{
    /// <summary>
    /// Utility class for testing XREAL Vision System components
    /// Provides common test helpers and mock implementations
    /// Following Unity testing best practices
    /// </summary>
    public static class TestUtilities
    {
        /// <summary>
        /// Creates a test texture for vision processing tests
        /// </summary>
        public static Texture2D CreateTestTexture(int width = 512, int height = 512, Color? fillColor = null)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var color = fillColor ?? Color.gray;
            
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Creates a test texture with a pattern for visual verification
        /// </summary>
        public static Texture2D CreateTestTextureWithPattern(int width = 512, int height = 512)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var pixels = new Color[width * height];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Create a checkerboard pattern
                    int index = y * width + x;
                    bool isWhite = ((x / 32) + (y / 32)) % 2 == 0;
                    pixels[index] = isWhite ? Color.white : Color.black;
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Creates a test audio clip for speech testing
        /// </summary>
        public static AudioClip CreateTestAudioClip(float length = 1f, int frequency = 44100)
        {
            var sampleCount = Mathf.RoundToInt(length * frequency);
            var audioClip = AudioClip.Create("TestClip", sampleCount, 1, frequency, false);
            
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = Mathf.Sin(2f * Mathf.PI * 440f * i / frequency) * 0.1f; // 440Hz tone
            }
            
            audioClip.SetData(samples, 0);
            return audioClip;
        }

        /// <summary>
        /// Creates a silent audio clip for testing
        /// </summary>
        public static AudioClip CreateSilentAudioClip(float length = 1f, int frequency = 44100)
        {
            var sampleCount = Mathf.RoundToInt(length * frequency);
            var audioClip = AudioClip.Create("SilentTestClip", sampleCount, 1, frequency, false);
            
            var samples = new float[sampleCount]; // All zeros = silence
            audioClip.SetData(samples, 0);
            return audioClip;
        }

        /// <summary>
        /// Creates a test GameObject with XREAL Vision System components
        /// </summary>
        public static GameObject CreateTestVisionSystem()
        {
            var go = new GameObject("TestVisionSystem");
            go.AddComponent<VisionSystem>();
            go.AddComponent<CameraManager>();
            go.AddComponent<OllamaConnector>();
            go.AddComponent<SpeechToText>();
            go.AddComponent<TextToSpeech>();
            go.AddComponent<VoiceCommandHandler>();
            return go;
        }

        /// <summary>
        /// Creates a minimal test camera setup
        /// </summary>
        public static Camera CreateTestCamera()
        {
            var cameraGO = new GameObject("TestCamera");
            var camera = cameraGO.AddComponent<Camera>();
            camera.backgroundColor = Color.blue;
            camera.clearFlags = CameraClearFlags.SolidColor;
            return camera;
        }

        /// <summary>
        /// Simulates waiting for async operations in tests
        /// </summary>
        public static IEnumerator WaitForCondition(System.Func<bool> condition, float timeout = 5f)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
        }

        /// <summary>
        /// Validates that a texture is valid for vision processing
        /// </summary>
        public static bool IsValidVisionTexture(Texture2D texture)
        {
            if (texture == null) return false;
            if (texture.width <= 0 || texture.height <= 0) return false;
            if (texture.width > 4096 || texture.height > 4096) return false; // Reasonable size limit
            return true;
        }

        /// <summary>
        /// Validates audio clip for speech processing
        /// </summary>
        public static bool IsValidAudioClip(AudioClip clip)
        {
            if (clip == null) return false;
            if (clip.length <= 0) return false;
            if (clip.samples <= 0) return false;
            if (clip.channels <= 0) return false;
            return true;
        }

        /// <summary>
        /// Mock implementation of XREALCameraManager for testing
        /// </summary>
        public class MockCameraManager : MonoBehaviour
        {
            public bool IsInitialized { get; private set; } = false;
            public bool HasCapturedImage { get; private set; } = false;
            
            public UnityEvent OnCaptureStarted { get; private set; } = new UnityEvent();
            public UnityEvent<Texture2D> OnImageCaptured { get; private set; } = new UnityEvent<Texture2D>();
            public UnityEvent<string> OnError { get; private set; } = new UnityEvent<string>();
            
            private Texture2D lastCapturedImage;
            
            public void Initialize()
            {
                IsInitialized = true;
            }
            
            public void CaptureImage()
            {
                OnCaptureStarted?.Invoke();
                lastCapturedImage = CreateTestTexture();
                HasCapturedImage = true;
                OnImageCaptured?.Invoke(lastCapturedImage);
            }
            
            public Texture2D GetLastCapturedImage() => lastCapturedImage;
            
            public Texture2D GetLastCapturedImageCopy()
            {
                if (lastCapturedImage == null) return null;
                var copy = new Texture2D(lastCapturedImage.width, lastCapturedImage.height, lastCapturedImage.format, false);
                copy.SetPixels(lastCapturedImage.GetPixels());
                copy.Apply();
                return copy;
            }
            
            public void ClearCapturedImage()
            {
                HasCapturedImage = false;
                if (lastCapturedImage != null)
                {
                    DestroyImmediate(lastCapturedImage);
                    lastCapturedImage = null;
                }
            }
            
            public void SetCaptureResolution(int width, int height)
            {
                // Mock implementation
            }
        }

        /// <summary>
        /// Mock implementation of XREALOllamaConnector for testing
        /// </summary>
        public class MockOllamaConnector : MonoBehaviour
        {
            public bool IsInitialized { get; private set; } = false;
            public bool IsConnected { get; private set; } = false;
            public bool IsProcessing { get; private set; } = false;
            public string ServerAddress => "mock://localhost:11434";
            
            public UnityEvent OnProcessingStarted { get; private set; } = new UnityEvent();
            public UnityEvent<string> OnResponseReceived { get; private set; } = new UnityEvent<string>();
            public UnityEvent<string> OnError { get; private set; } = new UnityEvent<string>();
            
            public string MockResponse = "I can see a test image with geometric patterns.";
            public bool ShouldSimulateError = false;
            public float ResponseDelay = 0.5f;
            
            public void Initialize()
            {
                IsInitialized = true;
                IsConnected = true;
            }
            
            public void SendVisionRequest(Texture2D image, string prompt)
            {
                StartCoroutine(SimulateVisionRequest(image, prompt));
            }
            
            public void SendTextRequest(string prompt)
            {
                StartCoroutine(SimulateTextRequest(prompt));
            }
            
            private IEnumerator SimulateVisionRequest(Texture2D image, string prompt)
            {
                IsProcessing = true;
                OnProcessingStarted?.Invoke();
                
                yield return new WaitForSeconds(ResponseDelay);
                
                IsProcessing = false;
                
                if (ShouldSimulateError)
                {
                    OnError?.Invoke("Mock error occurred");
                }
                else
                {
                    OnResponseReceived?.Invoke($"{MockResponse} Prompt: {prompt}");
                }
            }
            
            private IEnumerator SimulateTextRequest(string prompt)
            {
                IsProcessing = true;
                OnProcessingStarted?.Invoke();
                
                yield return new WaitForSeconds(ResponseDelay);
                
                IsProcessing = false;
                
                if (ShouldSimulateError)
                {
                    OnError?.Invoke("Mock error occurred");
                }
                else
                {
                    OnResponseReceived?.Invoke($"Text response to: {prompt}");
                }
            }
        }
    }
        /// Sets up a complete vision system for testing
        /// </summary>
        public static GameObject CreateTestVisionSystem(string name = "TestVisionSystem")
        {
            var gameObject = new GameObject(name);
            
            // Add all core components
            gameObject.AddComponent<VisionSystem>();
            gameObject.AddComponent<CameraManager>();
            gameObject.AddComponent<OllamaConnector>();
            gameObject.AddComponent<SpeechToText>();
            gameObject.AddComponent<TextToSpeech>();
            gameObject.AddComponent<VoiceCommandHandler>();
            
            return gameObject;
        }

        /// <summary>
        /// Waits for a system to initialize with timeout
        /// </summary>
        public static IEnumerator WaitForInitialization(System.Func<bool> isInitialized, float timeout = 5f)
        {
            float elapsed = 0f;
            while (!isInitialized() && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
        }

        /// <summary>
        /// Mock implementation of Ollama response for testing
        /// </summary>
        public static string GetMockOllamaResponse(string prompt)
        {
            if (prompt.ToLower().Contains("see"))
                return "I can see a test image with basic geometric shapes.";
            else if (prompt.ToLower().Contains("color"))
                return "The image contains primarily gray and white colors.";
            else
                return "This is a mock response for testing purposes.";
        }

        /// <summary>
        /// Validates that all required vision system components are present
        /// </summary>
        public static bool ValidateVisionSystemSetup(GameObject gameObject)
        {
            return gameObject.GetComponent<VisionSystem>() != null &&
                   gameObject.GetComponent<CameraManager>() != null &&
                   gameObject.GetComponent<OllamaConnector>() != null &&
                   gameObject.GetComponent<SpeechToText>() != null &&
                   gameObject.GetComponent<TextToSpeech>() != null &&
                   gameObject.GetComponent<VoiceCommandHandler>() != null;
        }

        /// <summary>
        /// Creates a test camera for camera manager testing
        /// </summary>
        public static Camera CreateTestCamera(GameObject parent)
        {
            var cameraObj = new GameObject("TestCamera");
            cameraObj.transform.SetParent(parent.transform);
            
            var camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.gray;
            
            return camera;
        }

        /// <summary>
        /// Simulates user input for testing
        /// </summary>
        public static void SimulateKeyPress(KeyCode key)
        {
            // Note: This is a mock - actual input simulation would require additional setup
            Debug.Log($"Simulating key press: {key}");
        }

        /// <summary>
        /// Creates test speech recognition result
        /// </summary>
        public static string CreateTestTranscription(string command)
        {
            var testCommands = new[]
            {
                "What do you see?",
                "Describe the scene",
                "What objects can you identify?",
                "What colors are visible?",
                "Tell me about the environment"
            };

            return testCommands[Mathf.Abs(command.GetHashCode()) % testCommands.Length];
        }

        /// <summary>
        /// Validates component event subscriptions
        /// </summary>
        public static bool ValidateEventSubscriptions(MonoBehaviour component)
        {
            // Check if component has UnityEvents that can be subscribed to
            var fields = component.GetType().GetFields();
            foreach (var field in fields)
            {
                if (field.FieldType.IsSubclassOf(typeof(UnityEngine.Events.UnityEventBase)))
                {
                    var eventField = field.GetValue(component) as UnityEngine.Events.UnityEventBase;
                    if (eventField == null) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Performance measurement helper
        /// </summary>
        public static float MeasureExecutionTime(System.Action action)
        {
            var startTime = Time.realtimeSinceStartup;
            action?.Invoke();
            return Time.realtimeSinceStartup - startTime;
        }

        /// <summary>
        /// Memory measurement helper
        /// </summary>
        public static long MeasureMemoryUsage(System.Action action)
        {
            System.GC.Collect();
            var initialMemory = System.GC.GetTotalMemory(false);
            action?.Invoke();
            var finalMemory = System.GC.GetTotalMemory(false);
            return finalMemory - initialMemory;
        }
    }
}
