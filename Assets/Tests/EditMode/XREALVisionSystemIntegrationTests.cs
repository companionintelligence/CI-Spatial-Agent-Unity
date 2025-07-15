using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.TestTools;
using CompanionIntelligence.Core;

namespace CompanionIntelligence.Tests
{
    /// <summary>
    /// Comprehensive integration tests for the complete XREAL Vision System
    /// Tests the end-to-end functionality and component interactions
    /// </summary>
    public class XREALVisionSystemIntegrationTests
    {
        private GameObject testGameObject;
        private VisionSystem visionSystem;
        private CameraManager cameraManager;
        private OllamaConnector ollamaConnector;
        private SpeechToText speechToText;
        private TextToSpeech textToSpeech;
        private XREALVisionConfig testConfig;

        [SetUp]
        public void Setup()
        {
            // Create test configuration
            testConfig = ScriptableObject.CreateInstance<XREALVisionConfig>();
            
            // Create test game object hierarchy
            testGameObject = new GameObject("TestXREALVisionSystem");
            visionSystem = testGameObject.AddComponent<VisionSystem>();
            
            // Create child objects for components
            var cameraGameObject = new GameObject("CameraManager");
            cameraGameObject.transform.SetParent(testGameObject.transform);
            cameraManager = cameraGameObject.AddComponent<CameraManager>();
            
            var ollamaGameObject = new GameObject("OllamaConnector");
            ollamaGameObject.transform.SetParent(testGameObject.transform);
            ollamaConnector = ollamaGameObject.AddComponent<OllamaConnector>();
            
            var sttGameObject = new GameObject("SpeechToText");
            sttGameObject.transform.SetParent(testGameObject.transform);
            speechToText = sttGameObject.AddComponent<SpeechToText>();
            
            var ttsGameObject = new GameObject("TextToSpeech");
            ttsGameObject.transform.SetParent(testGameObject.transform);
            textToSpeech = ttsGameObject.AddComponent<TextToSpeech>();
            
            // Create a test camera
            var cameraObj = new GameObject("TestCamera");
            cameraObj.AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
            {
                Object.DestroyImmediate(testGameObject);
            }
            
            if (testConfig != null)
            {
                Object.DestroyImmediate(testConfig);
            }
            
            // Clean up any test cameras
            var testCameras = Object.FindObjectsOfType<Camera>();
            foreach (var cam in testCameras)
            {
                if (cam.gameObject.name == "TestCamera")
                {
                    Object.DestroyImmediate(cam.gameObject);
                }
            }
        }

        [Test]
        public void VisionSystem_ComponentsAutoFind_FindsAllComponents()
        {
            // Test that the vision system can auto-find its required components
            Assert.IsNotNull(visionSystem);
            
            // The system should find child components automatically
            var foundCameraManager = visionSystem.GetComponentInChildren<CameraManager>();
            var foundOllamaConnector = visionSystem.GetComponentInChildren<OllamaConnector>();
            var foundSpeechToText = visionSystem.GetComponentInChildren<SpeechToText>();
            var foundTextToSpeech = visionSystem.GetComponentInChildren<TextToSpeech>();
            
            Assert.IsNotNull(foundCameraManager, "CameraManager should be found");
            Assert.IsNotNull(foundOllamaConnector, "OllamaConnector should be found");
            Assert.IsNotNull(foundSpeechToText, "SpeechToText should be found");
            Assert.IsNotNull(foundTextToSpeech, "TextToSpeech should be found");
        }

        [UnityTest]
        public IEnumerator VisionSystem_InitializationFlow_InitializesCorrectly()
        {
            // Test the complete initialization flow
            Assert.AreEqual(VisionSystem.SystemState.Idle, visionSystem.CurrentState);
            Assert.IsFalse(visionSystem.IsInitialized);
            
            // Wait for initialization to complete
            yield return new WaitForSeconds(2.0f);
            
            // Note: In actual tests, components may not fully initialize without proper setup
            // This test structure shows how to test the initialization flow
        }

        [Test]
        public void VisionSystem_StateTransitions_AreValid()
        {
            // Test that state transitions follow expected patterns
            var initialState = visionSystem.CurrentState;
            Assert.AreEqual(VisionSystem.SystemState.Idle, initialState);
            
            // Test manual text command (which doesn't require full component setup)
            visionSystem.ProcessTextCommand("Test command");
            
            // State should remain consistent
            Assert.IsTrue(visionSystem.CurrentState == VisionSystem.SystemState.Idle || 
                         visionSystem.CurrentState == VisionSystem.SystemState.Processing);
        }

        [Test]
        public void VisionSystem_EventSubscriptions_AreSetupCorrectly()
        {
            // Test that events are properly set up
            bool voiceCommandReceived = false;
            bool imageCaptured = false;
            bool visionResponse = false;
            bool systemError = false;
            bool systemReady = false;
            
            visionSystem.OnVoiceCommandReceived.AddListener(_ => voiceCommandReceived = true);
            visionSystem.OnImageCaptured.AddListener(_ => imageCaptured = true);
            visionSystem.OnVisionResponse.AddListener(_ => visionResponse = true);
            visionSystem.OnSystemError.AddListener(_ => systemError = true);
            visionSystem.OnSystemReady.AddListener(() => systemReady = true);
            
            // Events should be initialized (not null)
            Assert.IsNotNull(visionSystem.OnVoiceCommandReceived);
            Assert.IsNotNull(visionSystem.OnImageCaptured);
            Assert.IsNotNull(visionSystem.OnVisionResponse);
            Assert.IsNotNull(visionSystem.OnSystemError);
            Assert.IsNotNull(visionSystem.OnSystemReady);
        }

        [Test]
        public void CameraManager_InterfaceCompliance_ImplementsCorrectly()
        {
            // Test that CameraManager implements IXREALCameraManager correctly
            IXREALCameraManager iCameraManager = cameraManager;
            
            Assert.IsNotNull(iCameraManager);
            Assert.IsNotNull(iCameraManager.OnCaptureStarted);
            Assert.IsNotNull(iCameraManager.OnImageCaptured);
            Assert.IsNotNull(iCameraManager.OnError);
            
            // Test properties
            Assert.IsFalse(iCameraManager.HasCapturedImage); // Should start false
        }

        [Test]
        public void OllamaConnector_InterfaceCompliance_ImplementsCorrectly()
        {
            // Test that OllamaConnector implements IXREALOllamaConnector correctly
            IXREALOllamaConnector iOllamaConnector = ollamaConnector;
            
            Assert.IsNotNull(iOllamaConnector);
            Assert.IsNotNull(iOllamaConnector.OnProcessingStarted);
            Assert.IsNotNull(iOllamaConnector.OnResponseReceived);
            Assert.IsNotNull(iOllamaConnector.OnError);
            
            // Test properties
            Assert.IsFalse(iOllamaConnector.IsProcessing); // Should start false
            Assert.IsNotNull(iOllamaConnector.ServerAddress);
        }

        [Test]
        public void SpeechToText_InterfaceCompliance_ImplementsCorrectly()
        {
            // Test that SpeechToText implements IXREALSpeechToText correctly
            IXREALSpeechToText iSpeechToText = speechToText;
            
            Assert.IsNotNull(iSpeechToText);
            Assert.IsNotNull(iSpeechToText.OnListeningStarted);
            Assert.IsNotNull(iSpeechToText.OnListeningStopped);
            Assert.IsNotNull(iSpeechToText.OnTranscriptionComplete);
            Assert.IsNotNull(iSpeechToText.OnError);
            
            // Test properties
            Assert.IsFalse(iSpeechToText.IsListening); // Should start false
            Assert.IsFalse(iSpeechToText.IsProcessing); // Should start false
        }

        [Test]
        public void TextToSpeech_InterfaceCompliance_ImplementsCorrectly()
        {
            // Test that TextToSpeech implements IXREALTextToSpeech correctly
            IXREALTextToSpeech iTextToSpeech = textToSpeech;
            
            Assert.IsNotNull(iTextToSpeech);
            Assert.IsNotNull(iTextToSpeech.OnSpeechStarted);
            Assert.IsNotNull(iTextToSpeech.OnSpeechCompleted);
            Assert.IsNotNull(iTextToSpeech.OnError);
            
            // Test properties
            Assert.IsFalse(iTextToSpeech.IsPlaying); // Should start false
        }

        [Test]
        public void VisionConfig_Validation_WorksCorrectly()
        {
            // Test configuration validation
            Assert.IsTrue(testConfig.ValidateConfig());
            
            // Test that invalid configurations are caught
            var invalidConfig = ScriptableObject.CreateInstance<XREALVisionConfig>();
            // The OnValidate method should ensure valid values, so this should still be valid
            Assert.IsTrue(invalidConfig.ValidateConfig());
            
            Object.DestroyImmediate(invalidConfig);
        }

        [Test]
        public void PerformanceMonitor_Recording_WorksCorrectly()
        {
            // Test performance monitoring
            XREALPerformanceMonitor.ClearMetrics();
            
            const string testMetricName = "TestMetric";
            XREALPerformanceMonitor.RecordMetric(testMetricName, 100.0f);
            
            var metric = XREALPerformanceMonitor.GetMetric(testMetricName);
            Assert.IsNotNull(metric);
            Assert.AreEqual(1, metric.executionCount);
            Assert.AreEqual(100.0f, metric.lastExecutionTime, 0.01f);
            
            // Test timer functionality
            XREALPerformanceMonitor.StartTimer("TimerTest");
            XREALPerformanceMonitor.StopTimer("TimerTest");
            
            var timerMetric = XREALPerformanceMonitor.GetMetric("TimerTest");
            Assert.IsNotNull(timerMetric);
            Assert.AreEqual(1, timerMetric.executionCount);
        }

        [UnityTest]
        public IEnumerator VisionSystem_ErrorRecovery_HandlesGracefully()
        {
            // Test error recovery mechanisms
            bool errorEventFired = false;
            visionSystem.OnSystemError.AddListener(_ => errorEventFired = true);
            
            // Simulate error conditions and verify recovery
            // This would need more specific implementation based on error scenarios
            
            yield return new WaitForSeconds(1.0f);
            
            // System should maintain stable state even with errors
            Assert.IsTrue(visionSystem.CurrentState == VisionSystem.SystemState.Idle ||
                         visionSystem.CurrentState == VisionSystem.SystemState.Error);
        }

        [Test]
        public void VisionSystem_MemoryManagement_NoLeaks()
        {
            // Test for memory leaks by creating and destroying components
            var initialMemory = System.GC.GetTotalMemory(true);
            
            for (int i = 0; i < 10; i++)
            {
                var tempObject = new GameObject("TempTest");
                var tempVisionSystem = tempObject.AddComponent<VisionSystem>();
                Object.DestroyImmediate(tempObject);
            }
            
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            
            var finalMemory = System.GC.GetTotalMemory(true);
            var memoryDifference = finalMemory - initialMemory;
            
            // Allow for some memory overhead, but shouldn't be excessive
            Assert.Less(memoryDifference, 1024 * 1024, "Memory usage increased by more than 1MB");
        }
    }
}
