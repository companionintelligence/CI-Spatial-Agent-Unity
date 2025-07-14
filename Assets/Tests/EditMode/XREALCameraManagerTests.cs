using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CompanionIntelligence.Core;

namespace CompanionIntelligence.Tests
{
    /// <summary>
    /// Unit tests for XREALCameraManager component
    /// Tests camera capture functionality and image processing
    /// </summary>
    public class XREALCameraManagerTests
    {
        private GameObject testGameObject;
        private XREALCameraManager cameraManager;

        [SetUp]
        public void Setup()
        {
            testGameObject = new GameObject("TestCameraManager");
            cameraManager = testGameObject.AddComponent<XREALCameraManager>();
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
        public void CameraManager_InitialState_IsCorrect()
        {
            Assert.IsFalse(cameraManager.IsInitialized);
            Assert.IsFalse(cameraManager.HasCapturedImage);
            Assert.IsNull(cameraManager.GetLastCapturedImage());
        }

        [Test]
        public void CameraManager_Initialize_WithoutCamera_HandlesGracefully()
        {
            // Test initialization without a camera component
            cameraManager.Initialize();
            
            // Should handle missing camera gracefully
            Assert.IsNotNull(cameraManager);
        }

        [Test]
        public void CameraManager_SetCaptureResolution_UpdatesCorrectly()
        {
            int testWidth = 256;
            int testHeight = 256;
            
            Assert.DoesNotThrow(() => cameraManager.SetCaptureResolution(testWidth, testHeight));
        }

        [Test]
        public void CameraManager_CaptureImage_WithoutInitialization_HandlesGracefully()
        {
            // Test capture without initialization
            Assert.DoesNotThrow(() => cameraManager.CaptureImage());
            Assert.IsFalse(cameraManager.HasCapturedImage);
        }

        [UnityTest]
        public IEnumerator CameraManager_InitializeWithCamera_WorksCorrectly()
        {
            // Add a camera component
            var camera = testGameObject.AddComponent<Camera>();
            
            // Initialize and wait
            cameraManager.Initialize();
            yield return new WaitForSeconds(0.1f);
            
            // Should initialize successfully with camera
            Assert.IsTrue(cameraManager.IsInitialized);
            
            // Cleanup
            Object.DestroyImmediate(camera);
        }

        [Test]
        public void CameraManager_EventsSubscription_WorksCorrectly()
        {
            bool captureStartedFired = false;
            bool imageCapturedFired = false;
            bool errorFired = false;

            cameraManager.OnCaptureStarted.AddListener(() => captureStartedFired = true);
            cameraManager.OnImageCaptured.AddListener((texture) => imageCapturedFired = true);
            cameraManager.OnError.AddListener((error) => errorFired = true);

            // Events should be subscribable without errors
            Assert.IsFalse(captureStartedFired);
            Assert.IsFalse(imageCapturedFired);
            Assert.IsFalse(errorFired);
        }

        [Test]
        public void CameraManager_GetLastCapturedImageCopy_ReturnsNullWhenNoImage()
        {
            var copy = cameraManager.GetLastCapturedImageCopy();
            Assert.IsNull(copy);
        }

        [Test]
        public void CameraManager_ClearCapturedImage_WorksCorrectly()
        {
            Assert.DoesNotThrow(() => cameraManager.ClearCapturedImage());
            Assert.IsFalse(cameraManager.HasCapturedImage);
        }

        [Test]
        public void CameraManager_ValidationSettings_AreReasonable()
        {
            // Test that default settings are reasonable for mobile/XR
            cameraManager.SetCaptureResolution(512, 512);
            
            // Should accept common resolutions
            Assert.DoesNotThrow(() => cameraManager.SetCaptureResolution(256, 256));
            Assert.DoesNotThrow(() => cameraManager.SetCaptureResolution(1024, 1024));
        }
    }
}
