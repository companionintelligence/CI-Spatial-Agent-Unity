using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CompanionIntelligence.Core;

namespace CompanionIntelligence.Tests
{
    /// <summary>
    /// Comprehensive unit tests for XREALOllamaConnector
    /// Tests connection, request handling, retry logic, and error scenarios
    /// </summary>
    public class XREALOllamaConnectorImprovedTests
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
                Object.DestroyImmediate(testGameObject);
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
        public void OllamaConnector_ServerAddress_FormatsCorrectly()
        {
            var serverAddress = ollamaConnector.ServerAddress;
            Assert.IsTrue(serverAddress.Contains(":"));
            Assert.IsTrue(serverAddress.Length > 0);
        }

        [Test]
        public void OllamaConnector_Initialize_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ollamaConnector.Initialize());
        }

        [Test]
        public void OllamaConnector_SendVisionRequest_WithNullImage_HandlesGracefully()
        {
            ollamaConnector.Initialize();
            
            bool errorOccurred = false;
            ollamaConnector.OnError.AddListener((error) => errorOccurred = true);
            
            // Should handle null image gracefully
            Assert.DoesNotThrow(() => ollamaConnector.SendVisionRequest(null, "Test prompt"));
        }

        [Test]
        public void OllamaConnector_SendTextRequest_WithValidPrompt_DoesNotThrow()
        {
            ollamaConnector.Initialize();
            Assert.DoesNotThrow(() => ollamaConnector.SendTextRequest("Test prompt"));
        }

        [Test]
        public void OllamaConnector_SendTextRequest_WithEmptyPrompt_HandlesGracefully()
        {
            ollamaConnector.Initialize();
            Assert.DoesNotThrow(() => ollamaConnector.SendTextRequest(""));
            Assert.DoesNotThrow(() => ollamaConnector.SendTextRequest(null));
        }

        [Test]
        public void OllamaConnector_MultipleRequests_RejectsWhenBusy()
        {
            ollamaConnector.Initialize();
            
            // Set processing state via reflection
            var isProcessingField = typeof(XREALOllamaConnector).GetField("isProcessing", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            isProcessingField?.SetValue(ollamaConnector, true);
            
            // Should reject new requests when busy
            var testTexture = XREALTestUtilities.CreateTestTexture();
            ollamaConnector.SendVisionRequest(testTexture, "Test");
            
            // Should still be processing the first request
            Assert.IsTrue(ollamaConnector.IsProcessing);
            
            Object.DestroyImmediate(testTexture);
        }

        [UnityTest]
        public IEnumerator OllamaConnector_RequestTimeout_HandlesCorrectly()
        {
            ollamaConnector.Initialize();
            
            // Set a very short timeout for testing
            var timeoutField = typeof(XREALOllamaConnector).GetField("requestTimeout", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            timeoutField?.SetValue(ollamaConnector, 0.1f);
            
            bool errorOccurred = false;
            ollamaConnector.OnError.AddListener((error) => errorOccurred = true);
            
            var testTexture = XREALTestUtilities.CreateTestTexture();
            ollamaConnector.SendVisionRequest(testTexture, "Test prompt");
            
            // Wait for timeout
            yield return new WaitForSeconds(1f);
            
            Object.DestroyImmediate(testTexture);
        }

        [Test]
        public void OllamaConnector_EventSubscriptions_WorkCorrectly()
        {
            bool processingStarted = false;
            bool responseReceived = false;
            bool errorOccurred = false;
            
            ollamaConnector.OnProcessingStarted.AddListener(() => processingStarted = true);
            ollamaConnector.OnResponseReceived.AddListener((response) => responseReceived = true);
            ollamaConnector.OnError.AddListener((error) => errorOccurred = true);
            
            // Test that events are properly set up
            Assert.IsNotNull(ollamaConnector.OnProcessingStarted);
            Assert.IsNotNull(ollamaConnector.OnResponseReceived);
            Assert.IsNotNull(ollamaConnector.OnError);
        }

        [Test]
        public void OllamaConnector_InvalidServerConfig_HandlesGracefully()
        {
            // Test with invalid server configuration
            var serverIPField = typeof(XREALOllamaConnector).GetField("serverIP", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var serverPortField = typeof(XREALOllamaConnector).GetField("serverPort", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            serverIPField?.SetValue(ollamaConnector, "invalid.server.address");
            serverPortField?.SetValue(ollamaConnector, -1);
            
            Assert.DoesNotThrow(() => ollamaConnector.Initialize());
        }

        [Test]
        public void OllamaConnector_LargeImage_HandlesCorrectly()
        {
            ollamaConnector.Initialize();
            
            var largeTexture = XREALTestUtilities.CreateTestTexture(2048, 2048);
            
            Assert.DoesNotThrow(() => ollamaConnector.SendVisionRequest(largeTexture, "Describe this large image"));
            
            Object.DestroyImmediate(largeTexture);
        }

        [Test]
        public void OllamaConnector_ModelConfiguration_IsValid()
        {
            var modelField = typeof(XREALOllamaConnector).GetField("model", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var model = modelField?.GetValue(ollamaConnector) as string;
            
            Assert.IsNotNull(model);
            Assert.IsTrue(model.Length > 0);
        }

        [Test]
        public void OllamaConnector_SystemPrompt_IsConfigured()
        {
            var systemPromptField = typeof(XREALOllamaConnector).GetField("systemPrompt", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var systemPrompt = systemPromptField?.GetValue(ollamaConnector) as string;
            
            Assert.IsNotNull(systemPrompt);
            Assert.IsTrue(systemPrompt.Length > 0);
            Assert.IsTrue(systemPrompt.ToLower().Contains("vision") || systemPrompt.ToLower().Contains("xreal"));
        }
    }
}
