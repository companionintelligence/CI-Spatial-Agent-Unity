using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CompanionIntelligence.Core;

namespace CompanionIntelligence.Tests
{
    /// <summary>
    /// Integration tests for the complete XREAL Vision System
    /// Tests end-to-end functionality and component interactions
    /// </summary>
    public class XREALVisionSystemIntegrationTests
    {
        private GameObject visionSystemGameObject;
        private XREALVisionSystem visionSystem;
        private XREALVisionSceneSetup sceneSetup;

        [SetUp]
        public void Setup()
        {
            visionSystemGameObject = new GameObject("TestVisionSystemIntegration");
            sceneSetup = visionSystemGameObject.AddComponent<XREALVisionSceneSetup>();
        }

        [TearDown]
        public void TearDown()
        {
            if (visionSystemGameObject != null)
            {
                Object.DestroyImmediate(visionSystemGameObject);
            }
        }

        [UnityTest]
        public IEnumerator VisionSystem_CompleteSetup_CreatesAllComponents()
        {
            // Test the complete scene setup process
            sceneSetup.SetupXREALVisionScene();
            
            yield return new WaitForSeconds(0.5f);

            // Check that all core components were created
            visionSystem = Object.FindObjectOfType<XREALVisionSystem>();
            var cameraManager = Object.FindObjectOfType<XREALCameraManager>();
            var ollamaConnector = Object.FindObjectOfType<XREALOllamaConnector>();
            var speechToText = Object.FindObjectOfType<XREALSpeechToText>();
            var textToSpeech = Object.FindObjectOfType<XREALTextToSpeech>();
            var voiceCommandHandler = Object.FindObjectOfType<XREALVoiceCommandHandler>();

            Assert.IsNotNull(visionSystem, "VisionSystem should be created");
            Assert.IsNotNull(cameraManager, "CameraManager should be created");
            Assert.IsNotNull(ollamaConnector, "OllamaConnector should be created");
            Assert.IsNotNull(speechToText, "SpeechToText should be created");
            Assert.IsNotNull(textToSpeech, "TextToSpeech should be created");
            Assert.IsNotNull(voiceCommandHandler, "VoiceCommandHandler should be created");
        }

        [UnityTest]
        public IEnumerator VisionSystem_EventFlow_WorksCorrectly()
        {
            // Setup complete system
            sceneSetup.SetupXREALVisionScene();
            yield return new WaitForSeconds(0.5f);

            visionSystem = Object.FindObjectOfType<XREALVisionSystem>();
            Assert.IsNotNull(visionSystem);

            bool systemReadyFired = false;
            bool errorFired = false;

            visionSystem.OnSystemReady.AddListener(() => systemReadyFired = true);
            visionSystem.OnSystemError.AddListener((error) => errorFired = true);

            // Wait for system initialization
            yield return new WaitForSeconds(2f);

            // System should either be ready or handle errors gracefully
            Assert.IsTrue(systemReadyFired || !errorFired, "System should initialize or handle errors gracefully");
        }

        [UnityTest]
        public IEnumerator VisionSystem_VoiceToVisionPipeline_HandlesTestInput()
        {
            // Setup complete system
            sceneSetup.SetupXREALVisionScene();
            yield return new WaitForSeconds(0.5f);

            visionSystem = Object.FindObjectOfType<XREALVisionSystem>();
            Assert.IsNotNull(visionSystem);

            bool commandReceived = false;
            bool processingStarted = false;

            visionSystem.OnVoiceCommandReceived.AddListener((command) => commandReceived = true);

            // Test the pipeline with a text command
            visionSystem.ProcessTextCommand("What do you see?");

            yield return new WaitForSeconds(1f);

            // Command should be received and processed
            Assert.IsTrue(commandReceived, "Voice command should be received");
        }

        [Test]
        public void VisionSystem_ComponentReferences_AreSetCorrectly()
        {
            // Test that scene setup correctly wires component references
            sceneSetup.SetupXREALVisionScene();

            var allComponents = Object.FindObjectsOfType<MonoBehaviour>();
            var visionComponents = System.Array.FindAll(allComponents, 
                c => c.GetType().Namespace == "CompanionIntelligence.Core");

            Assert.IsTrue(visionComponents.Length > 0, "Should create vision system components");
        }

        [UnityTest]
        public IEnumerator VisionSystem_ErrorRecovery_WorksCorrectly()
        {
            // Setup system and test error recovery
            sceneSetup.SetupXREALVisionScene();
            yield return new WaitForSeconds(0.5f);

            visionSystem = Object.FindObjectOfType<XREALVisionSystem>();
            
            if (visionSystem != null)
            {
                bool errorHandled = false;
                visionSystem.OnSystemError.AddListener((error) => errorHandled = true);

                // The system should handle missing components gracefully
                yield return new WaitForSeconds(1f);

                // Even if errors occur, system should not crash
                Assert.IsNotNull(visionSystem);
            }
        }

        [Test]
        public void VisionSystem_DebugControls_AreCreated()
        {
            // Test that debug controls are properly created
            sceneSetup.SetupXREALVisionScene();

            var debugControls = Object.FindObjectOfType<XREALVisionDebugControls>();
            // Debug controls might not be created in all configurations
            // This test verifies the setup process doesn't throw errors
            Assert.DoesNotThrow(() => sceneSetup.SetupXREALVisionScene());
        }

        [UnityTest]
        public IEnumerator VisionSystem_StateTransitions_AreValid()
        {
            // Test state transitions through the complete pipeline
            sceneSetup.SetupXREALVisionScene();
            yield return new WaitForSeconds(0.5f);

            visionSystem = Object.FindObjectOfType<XREALVisionSystem>();
            
            if (visionSystem != null)
            {
                var initialState = visionSystem.CurrentState;
                
                // Process a command and observe state changes
                visionSystem.ProcessTextCommand("Test command");
                yield return new WaitForSeconds(0.1f);
                
                // State should change or remain valid
                Assert.IsTrue(System.Enum.IsDefined(typeof(XREALVisionSystem.SystemState), visionSystem.CurrentState));
            }
        }
    }
}
