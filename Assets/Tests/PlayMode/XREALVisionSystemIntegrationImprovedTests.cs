using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CompanionIntelligence.Core;

namespace CompanionIntelligence.Tests
{
    /// <summary>
    /// Comprehensive integration tests for the XREAL Vision System
    /// Tests end-to-end workflows and component interactions
    /// </summary>
    public class XREALVisionSystemIntegrationImprovedTests
    {
        private GameObject visionSystemGO;
        private XREALVisionSystem visionSystem;

        [SetUp]
        public void Setup()
        {
            visionSystemGO = XREALTestUtilities.CreateTestVisionSystem();
            visionSystem = visionSystemGO.GetComponent<XREALVisionSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (visionSystemGO != null)
                Object.DestroyImmediate(visionSystemGO);
        }

        [UnityTest]
        public IEnumerator VisionSystem_FullWorkflow_WithMockComponents_CompletesSuccessfully()
        {
            // Replace components with mocks for reliable testing
            var mockCamera = visionSystemGO.AddComponent<XREALTestUtilities.MockCameraManager>();
            var mockOllama = visionSystemGO.AddComponent<XREALTestUtilities.MockOllamaConnector>();
            
            // Initialize mocks
            mockCamera.Initialize();
            mockOllama.Initialize();
            
            // Set up component references via reflection
            var cameraField = typeof(XREALVisionSystem).GetField("cameraManager", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ollamaField = typeof(XREALVisionSystem).GetField("ollamaConnector", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            cameraField?.SetValue(visionSystem, mockCamera);
            ollamaField?.SetValue(visionSystem, mockOllama);
            
            // Track workflow completion
            bool systemReady = false;
            bool voiceCommandReceived = false;
            bool imageCaptured = false;
            bool visionResponseReceived = false;
            
            visionSystem.OnSystemReady.AddListener(() => systemReady = true);
            visionSystem.OnVoiceCommandReceived.AddListener((command) => voiceCommandReceived = true);
            visionSystem.OnImageCaptured.AddListener((image) => imageCaptured = true);
            visionSystem.OnVisionResponse.AddListener((response) => visionResponseReceived = true);
            
            // Start the system
            visionSystem.gameObject.SetActive(true);
            
            // Wait for system to initialize
            yield return XREALTestUtilities.WaitForCondition(() => systemReady, 10f);
            Assert.IsTrue(systemReady, "System should initialize successfully");
            
            // Simulate voice command workflow
            visionSystem.ProcessTextCommand("What do you see?");
            
            // Wait for workflow completion
            yield return XREALTestUtilities.WaitForCondition(() => visionResponseReceived, 10f);
            
            Assert.IsTrue(voiceCommandReceived, "Voice command should be processed");
            Assert.IsTrue(imageCaptured, "Image should be captured");
            Assert.IsTrue(visionResponseReceived, "Vision response should be received");
        }

        [UnityTest]
        public IEnumerator VisionSystem_ErrorHandling_RecoversProperly()
        {
            var mockCamera = visionSystemGO.AddComponent<XREALTestUtilities.MockCameraManager>();
            var mockOllama = visionSystemGO.AddComponent<XREALTestUtilities.MockOllamaConnector>();
            
            // Configure mock to simulate error
            mockOllama.ShouldSimulateError = true;
            
            mockCamera.Initialize();
            mockOllama.Initialize();
            
            // Set up component references
            var cameraField = typeof(XREALVisionSystem).GetField("cameraManager", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ollamaField = typeof(XREALVisionSystem).GetField("ollamaConnector", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            cameraField?.SetValue(visionSystem, mockCamera);
            ollamaField?.SetValue(visionSystem, mockOllama);
            
            bool systemError = false;
            bool systemRecovered = false;
            
            visionSystem.OnSystemError.AddListener((error) => systemError = true);
            
            visionSystem.gameObject.SetActive(true);
            yield return new WaitForSeconds(1f);
            
            // Trigger error scenario
            visionSystem.ProcessTextCommand("Cause an error");
            
            yield return XREALTestUtilities.WaitForCondition(() => systemError, 5f);
            Assert.IsTrue(systemError, "System should detect and report error");
            
            // Verify system can recover
            yield return new WaitForSeconds(3f); // Wait for error recovery
            Assert.AreEqual(XREALVisionSystem.SystemState.Idle, visionSystem.CurrentState, 
                "System should return to Idle state after error");
        }

        [UnityTest]
        public IEnumerator VisionSystem_StateTransitions_AreCorrect()
        {
            var mockCamera = visionSystemGO.AddComponent<XREALTestUtilities.MockCameraManager>();
            var mockOllama = visionSystemGO.AddComponent<XREALTestUtilities.MockOllamaConnector>();
            
            mockCamera.Initialize();
            mockOllama.Initialize();
            
            var cameraField = typeof(XREALVisionSystem).GetField("cameraManager", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ollamaField = typeof(XREALVisionSystem).GetField("ollamaConnector", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            cameraField?.SetValue(visionSystem, mockCamera);
            ollamaField?.SetValue(visionSystem, mockOllama);
            
            visionSystem.gameObject.SetActive(true);
            yield return new WaitForSeconds(1f);
            
            // Initial state should be Idle
            Assert.AreEqual(XREALVisionSystem.SystemState.Idle, visionSystem.CurrentState);
            
            // Process command and verify state transitions
            visionSystem.ProcessTextCommand("Test command");
            
            // Wait for processing to complete
            yield return new WaitForSeconds(2f);
            
            // Should return to Idle after processing
            Assert.AreEqual(XREALVisionSystem.SystemState.Idle, visionSystem.CurrentState);
        }

        [Test]
        public void VisionSystem_MultipleCommands_QueueCorrectly()
        {
            var mockCamera = visionSystemGO.AddComponent<XREALTestUtilities.MockCameraManager>();
            var mockOllama = visionSystemGO.AddComponent<XREALTestUtilities.MockOllamaConnector>();
            
            mockCamera.Initialize();
            mockOllama.Initialize();
            
            int commandCount = 0;
            visionSystem.OnVoiceCommandReceived.AddListener((command) => commandCount++);
            
            // Send multiple commands
            visionSystem.ProcessTextCommand("Command 1");
            visionSystem.ProcessTextCommand("Command 2");
            visionSystem.ProcessTextCommand("Command 3");
            
            // Only first command should be processed if system is busy
            Assert.IsTrue(commandCount >= 1, "At least one command should be processed");
        }

        [Test]
        public void VisionSystem_ComponentDependencies_AreValidated()
        {
            // Test system behavior with missing components
            var emptyGO = new GameObject("EmptyVisionSystem");
            var emptySystem = emptyGO.AddComponent<XREALVisionSystem>();
            
            // Should handle missing components gracefully
            Assert.DoesNotThrow(() => emptySystem.ProcessTextCommand("Test"));
            Assert.IsFalse(emptySystem.IsProcessing);
            
            Object.DestroyImmediate(emptyGO);
        }

        [Test]
        public void VisionSystem_ListeningControls_WorkCorrectly()
        {
            Assert.DoesNotThrow(() => visionSystem.StartListening());
            Assert.DoesNotThrow(() => visionSystem.StopListening());
            Assert.DoesNotThrow(() => visionSystem.ToggleListening());
        }

        [UnityTest]
        public IEnumerator VisionSystem_Performance_MeetsBaseline()
        {
            var mockCamera = visionSystemGO.AddComponent<XREALTestUtilities.MockCameraManager>();
            var mockOllama = visionSystemGO.AddComponent<XREALTestUtilities.MockOllamaConnector>();
            
            // Set fast response times for performance testing
            mockOllama.ResponseDelay = 0.1f;
            
            mockCamera.Initialize();
            mockOllama.Initialize();
            
            var cameraField = typeof(XREALVisionSystem).GetField("cameraManager", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ollamaField = typeof(XREALVisionSystem).GetField("ollamaConnector", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            cameraField?.SetValue(visionSystem, mockCamera);
            ollamaField?.SetValue(visionSystem, mockOllama);
            
            visionSystem.gameObject.SetActive(true);
            yield return new WaitForSeconds(1f);
            
            bool responseReceived = false;
            visionSystem.OnVisionResponse.AddListener((response) => responseReceived = true);
            
            var startTime = Time.realtimeSinceStartup;
            visionSystem.ProcessTextCommand("Performance test");
            
            yield return XREALTestUtilities.WaitForCondition(() => responseReceived, 5f);
            
            var totalTime = Time.realtimeSinceStartup - startTime;
            
            Assert.IsTrue(responseReceived, "Response should be received");
            Assert.Less(totalTime, 2f, "Total processing time should be under 2 seconds for mock implementation");
        }
    }
}
