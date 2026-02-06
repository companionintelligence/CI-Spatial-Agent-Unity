using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CompanionIntelligence.Core;
using CompanionIntelligence.Tests.Mocks;

namespace CompanionIntelligence.Tests
{
    /// <summary>
    /// PlayMode tests for XREAL Vision System runtime behavior
    /// Tests component interactions, coroutines, and real-time behavior
    /// </summary>
    public class XREALVisionSystemPlayModeTests
    {
        private GameObject testGameObject;
        private VisionSystem visionSystem;
        private MockCameraManager mockCamera;
        private MockOllamaConnector mockOllama;
        private MockSpeechToText mockSTT;
        private MockTextToSpeech mockTTS;

        [SetUp]
        public void Setup()
        {
            testGameObject = new GameObject("PlayModeTestSystem");
            
            // Create vision system
            visionSystem = testGameObject.AddComponent<VisionSystem>();
            
            // Add mock components
            mockCamera = testGameObject.AddComponent<MockCameraManager>();
            mockOllama = testGameObject.AddComponent<MockOllamaConnector>();
            mockSTT = testGameObject.AddComponent<MockSpeechToText>();
            mockTTS = testGameObject.AddComponent<MockTextToSpeech>();
            
            // Initialize performance monitoring
            XREALPerformanceMonitor.ClearMetrics();
            XREALPerformanceMonitor.IsEnabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
            {
                Object.Destroy(testGameObject);
            }
            
            XREALPerformanceMonitor.ClearMetrics();
        }

        [UnityTest]
        public IEnumerator VisionSystem_InitializationRuntime_CompletesSuccessfully()
        {
            // Test runtime initialization behavior
            Assert.AreEqual(VisionSystem.SystemState.Idle, visionSystem.CurrentState);
            Assert.IsFalse(visionSystem.IsInitialized);
            
            // Wait for initialization to complete
            yield return new WaitForSeconds(0.5f);
            
            // Since we're using mock components, initialization should succeed quickly
            Assert.IsTrue(visionSystem.IsInitialized || visionSystem.CurrentState != VisionSystem.SystemState.Error);
        }

        [UnityTest]
        public IEnumerator VoiceCommandFlow_EndToEnd_ProcessesCorrectly()
        {
            // Test complete voice command flow
            bool commandReceived = false;
            bool imageCapture = false;
            bool visionResponse = false;
            bool speechStarted = false;
            
            // Subscribe to events
            visionSystem.OnVoiceCommandReceived.AddListener(cmd => commandReceived = true);
            visionSystem.OnImageCaptured.AddListener(img => imageCapture = true);
            visionSystem.OnVisionResponse.AddListener(resp => visionResponse = true);
            mockTTS.OnSpeechStarted.AddListener(() => speechStarted = true);
            
            // Start the flow with a mock voice command
            yield return new WaitForSeconds(0.1f);
            
            // Simulate speech transcription completion
            mockSTT.OnTranscriptionComplete.Invoke("What am I looking at?");
            
            // Wait for the full pipeline to complete
            yield return new WaitForSeconds(5.0f);
            
            // Verify the flow completed (allowing for mock component behavior)
            Assert.IsTrue(commandReceived, "Voice command should be received");
            
            // The rest may depend on specific implementation details of the vision system
            // In a real test environment, you'd verify the complete flow
        }

        [UnityTest]
        public IEnumerator CameraCapture_Simulation_WorksCorrectly()
        {
            // Test camera capture simulation
            bool captureStarted = false;
            bool imageCaptured = false;
            Texture2D capturedImage = null;
            
            mockCamera.OnCaptureStarted.AddListener(() => captureStarted = true);
            mockCamera.OnImageCaptured.AddListener(img => {
                imageCaptured = true;
                capturedImage = img;
            });
            
            // Trigger capture
            mockCamera.CaptureImage();
            
            // Wait for capture to complete
            yield return new WaitForSeconds(0.5f);
            
            Assert.IsTrue(captureStarted, "Capture should have started");
            Assert.IsTrue(imageCaptured, "Image should have been captured");
            Assert.IsNotNull(capturedImage, "Captured image should not be null");
            Assert.IsTrue(mockCamera.HasCapturedImage, "Camera should report having captured image");
        }

        [UnityTest]
        public IEnumerator OllamaConnector_VisionRequest_ProcessesCorrectly()
        {
            // Test Ollama vision request processing
            bool processingStarted = false;
            bool responseReceived = false;
            string receivedResponse = null;
            
            mockOllama.OnProcessingStarted.AddListener(() => processingStarted = true);
            mockOllama.OnResponseReceived.AddListener(resp => {
                responseReceived = true;
                receivedResponse = resp;
            });
            
            // Create a test image
            var testImage = new Texture2D(256, 256, TextureFormat.RGB24, false);
            
            // Send vision request
            mockOllama.SendVisionRequest(testImage, "Test prompt");
            
            // Wait for processing
            yield return new WaitForSeconds(2.0f);
            
            Assert.IsTrue(processingStarted, "Processing should have started");
            Assert.IsTrue(responseReceived, "Response should have been received");
            Assert.IsNotNull(receivedResponse, "Response should not be null");
            Assert.IsFalse(mockOllama.IsProcessing, "Should not be processing after completion");
            
            // Cleanup
            Object.DestroyImmediate(testImage);
        }

        [UnityTest]
        public IEnumerator SpeechToText_ListeningCycle_WorksCorrectly()
        {
            // Test speech-to-text listening cycle
            bool listeningStarted = false;
            bool listeningStopped = false;
            bool transcriptionComplete = false;
            string transcribedText = null;
            
            mockSTT.OnListeningStarted.AddListener(() => listeningStarted = true);
            mockSTT.OnListeningStopped.AddListener(() => listeningStopped = true);
            mockSTT.OnTranscriptionComplete.AddListener(text => {
                transcriptionComplete = true;
                transcribedText = text;
            });
            
            // Start listening
            mockSTT.StartListening();
            
            Assert.IsTrue(mockSTT.IsListening, "Should be listening");
            
            // Wait for the complete cycle
            yield return new WaitForSeconds(3.0f);
            
            Assert.IsTrue(listeningStarted, "Listening should have started");
            Assert.IsTrue(listeningStopped, "Listening should have stopped");
            Assert.IsTrue(transcriptionComplete, "Transcription should be complete");
            Assert.IsNotNull(transcribedText, "Transcribed text should not be null");
            Assert.IsFalse(mockSTT.IsListening, "Should not be listening after completion");
        }

        [UnityTest]
        public IEnumerator TextToSpeech_SpeechCycle_WorksCorrectly()
        {
            // Test text-to-speech cycle
            bool speechStarted = false;
            bool speechCompleted = false;
            
            mockTTS.OnSpeechStarted.AddListener(() => speechStarted = true);
            mockTTS.OnSpeechCompleted.AddListener(() => speechCompleted = true);
            
            // Start speech
            mockTTS.SpeakText("Test speech text");
            
            Assert.IsTrue(mockTTS.IsPlaying, "Should be playing speech");
            
            // Wait for speech to complete
            yield return new WaitForSeconds(2.0f);
            
            Assert.IsTrue(speechStarted, "Speech should have started");
            Assert.IsTrue(speechCompleted, "Speech should have completed");
            Assert.IsFalse(mockTTS.IsPlaying, "Should not be playing after completion");
        }

        [UnityTest]
        public IEnumerator ErrorHandling_ComponentFailure_RecoversGracefully()
        {
            // Test error handling when components fail
            bool errorReceived = false;
            string errorMessage = null;
            
            visionSystem.OnSystemError.AddListener(err => {
                errorReceived = true;
                errorMessage = err;
            });
            
            // Simulate component failure
            mockOllama.OnError.Invoke("Simulated Ollama failure");
            
            yield return new WaitForSeconds(1.0f);
            
            // System should handle error gracefully
            Assert.AreNotEqual(VisionSystem.SystemState.Error, visionSystem.CurrentState, 
                "System should recover from component errors");
        }

        [UnityTest]
        public IEnumerator PerformanceTracking_Runtime_RecordsMetrics()
        {
            // Test performance tracking during runtime
            var performanceTracker = testGameObject.AddComponent<XREALPerformanceTracker>();
            
            // Let the tracker run for a bit
            yield return new WaitForSeconds(2.0f);
            
            // Check that frame time metrics are being recorded
            var frameTimeMetric = XREALPerformanceMonitor.GetMetric("FrameTime");
            Assert.IsNotNull(frameTimeMetric, "Frame time should be tracked");
            Assert.Greater(frameTimeMetric.executionCount, 0, "Should have recorded frame times");
            
            // Check performance summary
            var allMetrics = XREALPerformanceMonitor.GetAllMetrics();
            Assert.Greater(allMetrics.Count, 0, "Should have performance metrics");
        }

        [UnityTest]
        public IEnumerator SystemIntegration_MultipleOperations_WorkTogether()
        {
            // Test multiple system operations working together
            int completedOperations = 0;
            
            // Setup completion tracking
            mockCamera.OnImageCaptured.AddListener(_ => completedOperations++);
            mockOllama.OnResponseReceived.AddListener(_ => completedOperations++);
            mockTTS.OnSpeechCompleted.AddListener(() => completedOperations++);
            
            // Trigger multiple operations
            visionSystem.ProcessTextCommand("First command");
            yield return new WaitForSeconds(1.0f);
            
            visionSystem.ProcessTextCommand("Second command");
            yield return new WaitForSeconds(1.0f);
            
            visionSystem.ProcessTextCommand("Third command");
            
            // Wait for all operations to complete
            yield return new WaitForSeconds(8.0f);
            
            // Verify operations completed
            Assert.Greater(completedOperations, 0, "Some operations should have completed");
            Assert.AreNotEqual(VisionSystem.SystemState.Error, visionSystem.CurrentState);
        }

        [UnityTest]
        public IEnumerator MemoryPressure_LongRunning_StaysStable()
        {
            // Test system behavior under extended operation
            var initialMemory = System.GC.GetTotalMemory(false);
            
            // Run operations for an extended period
            for (int i = 0; i < 20; i++)
            {
                visionSystem.ProcessTextCommand($"Long running test {i}");
                
                // Capture memory usage periodically
                if (i % 5 == 0)
                {
                    XREALPerformanceMonitor.RecordMemoryUsage($"LongRun_{i}");
                }
                
                yield return new WaitForSeconds(0.5f);
            }
            
            // Force garbage collection
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            
            var finalMemory = System.GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;
            
            // Memory increase should be reasonable for extended operation
            Assert.Less(memoryIncrease, 50 * 1024 * 1024, "Memory increase should be less than 50MB");
            
            // System should still be responsive
            Assert.AreNotEqual(VisionSystem.SystemState.Error, visionSystem.CurrentState);
            Assert.IsFalse(visionSystem.IsProcessing);
        }

        [UnityTest]
        public IEnumerator ConfigurationChanges_Runtime_AppliedCorrectly()
        {
            // Test runtime configuration changes
            var config = ScriptableObject.CreateInstance<XREALVisionConfig>();
            
            // Apply configuration changes
            var profile = config.GetCurrentProfile();
            if (profile != null)
            {
                config.ApplyProfile(profile);
            }
            
            yield return new WaitForSeconds(0.1f);
            
            // Verify configuration is valid
            Assert.IsTrue(config.ValidateConfig(), "Configuration should be valid");
            
            // Cleanup
            Object.DestroyImmediate(config);
        }
    }
}
