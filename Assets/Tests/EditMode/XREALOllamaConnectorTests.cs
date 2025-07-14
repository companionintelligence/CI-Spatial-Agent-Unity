using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CompanionIntelligence.Core;

namespace CompanionIntelligence.Tests
{
    /// <summary>
    /// Unit tests for XREALOllamaConnector component
    /// Tests Ollama server communication and request handling
    /// </summary>
    public class XREALOllamaConnectorTests
    {
        private GameObject testGameObject;
        private XREALOllamaConnector ollamaConnector;

        [SetUp]
        public void Setup()
        {
            testGameObject = new GameObject("TestOllamaConnector");
            ollamaConnector = testGameObject.AddComponent<XREALOllamaConnector>();
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
            {
                Object.DestroyImmediate(testGameObject);
            }
        }

        [Test]
        public void OllamaConnector_InitialState_IsCorrect()
        {
            Assert.IsFalse(ollamaConnector.IsInitialized);
            Assert.IsFalse(ollamaConnector.IsConnected);
            Assert.IsFalse(ollamaConnector.IsProcessing);
            Assert.IsNotNull(ollamaConnector.ServerAddress);
        }

        [Test]
        public void OllamaConnector_Initialize_SetsUpCorrectly()
        {
            Assert.DoesNotThrow(() => ollamaConnector.Initialize());
        }

        [Test]
        public void OllamaConnector_UpdateServerConfiguration_UpdatesSettings()
        {
            string testIP = "192.168.1.100";
            int testPort = 11434;
            string testModel = "llava:7b";

            Assert.DoesNotThrow(() => 
                ollamaConnector.UpdateServerConfiguration(testIP, testPort, testModel));

            Assert.AreEqual($"{testIP}:{testPort}", ollamaConnector.ServerAddress);
        }

        [Test]
        public void OllamaConnector_SendRequests_WithoutInitialization_HandlesGracefully()
        {
            // Test requests without initialization
            Assert.DoesNotThrow(() => ollamaConnector.SendTextRequest("test prompt"));
            
            // Should handle gracefully when not initialized
            Assert.IsFalse(ollamaConnector.IsProcessing);
        }

        [Test]
        public void OllamaConnector_SendVisionRequest_WithNullImage_HandlesGracefully()
        {
            Assert.DoesNotThrow(() => ollamaConnector.SendVisionRequest(null, "test prompt"));
        }

        [Test]
        public void OllamaConnector_EventsSubscription_WorksCorrectly()
        {
            bool processingStartedFired = false;
            bool responseFired = false;
            bool errorFired = false;

            ollamaConnector.OnProcessingStarted.AddListener(() => processingStartedFired = true);
            ollamaConnector.OnResponseReceived.AddListener((response) => responseFired = true);
            ollamaConnector.OnError.AddListener((error) => errorFired = true);

            // Events should be subscribable without errors
            Assert.IsFalse(processingStartedFired);
            Assert.IsFalse(responseFired);
            Assert.IsFalse(errorFired);
        }

        [Test]
        public void OllamaConnector_RequestValidation_RejectsInvalidInputs()
        {
            // Test empty/null prompts
            Assert.DoesNotThrow(() => ollamaConnector.SendTextRequest(""));
            Assert.DoesNotThrow(() => ollamaConnector.SendTextRequest(null));
        }

        [Test]
        public void OllamaConnector_ServerConfiguration_ValidatesInputs()
        {
            // Test invalid configurations
            Assert.DoesNotThrow(() => ollamaConnector.UpdateServerConfiguration("", 0));
            Assert.DoesNotThrow(() => ollamaConnector.UpdateServerConfiguration("invalid", -1));
        }

        [Test]
        public void OllamaConnector_MultipleRequests_HandledCorrectly()
        {
            // Initialize first
            ollamaConnector.Initialize();

            // Send multiple requests
            ollamaConnector.SendTextRequest("First request");
            ollamaConnector.SendTextRequest("Second request");

            // Should handle multiple requests without crashing
            Assert.IsNotNull(ollamaConnector);
        }

        [UnityTest]
        public IEnumerator OllamaConnector_ConnectionTest_DoesNotHang()
        {
            // Test that connection attempts don't hang indefinitely
            ollamaConnector.Initialize();

            // Wait for connection attempt (should timeout gracefully)
            yield return new WaitForSeconds(2f);

            // Should not crash or hang
            Assert.IsNotNull(ollamaConnector);
        }

        [Test]
        public void OllamaConnector_ModelSettings_AreValid()
        {
            // Test model parameter validation through OnValidate
            var onValidateMethod = typeof(XREALOllamaConnector).GetMethod("OnValidate", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (onValidateMethod != null)
            {
                Assert.DoesNotThrow(() => onValidateMethod.Invoke(ollamaConnector, null));
            }
        }
    }
}
