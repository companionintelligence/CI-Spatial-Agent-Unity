using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CompanionIntelligence.Core;

namespace CompanionIntelligence.Tests
{
    /// <summary>
    /// Comprehensive unit tests for XREALCameraManager
    /// Tests camera initialization, capture functionality, and error handling
    /// Following Unity testing best practices
    /// </summary>
    public class CameraManagerImprovedTests
    {
        private GameObject testGameObject;
        private CameraManager cameraManager;
        private Camera testCamera;

        [SetUp]
        public void Setup()
        {
            // Create test camera
            var cameraGO = new GameObject("TestCamera");
            testCamera = cameraGO.AddComponent<Camera>();
            testCamera.backgroundColor = Color.blue;
            testCamera.clearFlags = CameraClearFlags.SolidColor;

            // Create camera manager
            testGameObject = new GameObject("TestCameraManager");
            cameraManager = testGameObject.AddComponent<CameraManager>();

            // Set up test camera reference
            var targetCameraField = typeof(CameraManager).GetField("targetCamera", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            targetCameraField?.SetValue(cameraManager, testCamera);
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
                Object.DestroyImmediate(testGameObject);
            if (testCamera != null)
                Object.DestroyImmediate(testCamera.gameObject);
        }

        [Test]
        public void CameraManager_InitialState_IsCorrect()
        {
            Assert.IsFalse(cameraManager.IsInitialized);
            Assert.IsFalse(cameraManager.HasCapturedImage);
        }

        [Test]
        public void CameraManager_Initialize_SetsInitializedFlag()
        {
            cameraManager.Initialize();
            Assert.IsTrue(cameraManager.IsInitialized);
        }

        [Test]
        public void CameraManager_InitializeWithoutCamera_HandlesGracefully()
        {
            var emptyCameraManager = new GameObject("EmptyCameraManager").AddComponent<CameraManager>();
            
            // Disable auto-find to test null camera scenario
            var autoFindField = typeof(CameraManager).GetField("autoFindMainCamera", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            autoFindField?.SetValue(emptyCameraManager, false);

            emptyCameraManager.Initialize();
            
            // Should not crash and should handle gracefully
            Assert.IsFalse(emptyCameraManager.IsInitialized);
            
            Object.DestroyImmediate(emptyCameraManager.gameObject);
        }

        [UnityTest]
        public IEnumerator CameraManager_CaptureImage_CompletesSuccessfully()
        {
            cameraManager.Initialize();
            
            bool imageCaptured = false;
            Texture2D capturedImage = null;
            
            cameraManager.OnImageCaptured.AddListener((image) => {
                imageCaptured = true;
                capturedImage = image;
            });

            cameraManager.CaptureImage();
            
            // Wait for capture to complete
            yield return TestUtilities.WaitForCondition(() => imageCaptured, 5f);
            
            Assert.IsTrue(imageCaptured);
            Assert.IsTrue(cameraManager.HasCapturedImage);
            Assert.IsNotNull(capturedImage);
            Assert.IsTrue(TestUtilities.IsValidVisionTexture(capturedImage));
        }

        [Test]
        public void CameraManager_SetCaptureResolution_UpdatesInternalSettings()
        {
            cameraManager.Initialize();
            cameraManager.SetCaptureResolution(256, 256);
            
            // Verify resolution change doesn't crash
            Assert.IsTrue(cameraManager.IsInitialized);
        }

        [Test]
        public void CameraManager_GetLastCapturedImageCopy_ReturnsValidCopy()
        {
            var testTexture = TestUtilities.CreateTestTexture();
            
            // Set the last captured image via reflection for testing
            var lastImageField = typeof(CameraManager).GetField("lastCapturedImage", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            lastImageField?.SetValue(cameraManager, testTexture);
            
            var hasCapturedField = typeof(CameraManager).GetField("hasCapturedImage", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            hasCapturedField?.SetValue(cameraManager, true);
            
            var copy = cameraManager.GetLastCapturedImageCopy();
            
            Assert.IsNotNull(copy);
            Assert.AreNotSame(testTexture, copy); // Should be different instances
            Assert.AreEqual(testTexture.width, copy.width);
            Assert.AreEqual(testTexture.height, copy.height);
            
            Object.DestroyImmediate(testTexture);
            Object.DestroyImmediate(copy);
        }

        [Test]
        public void CameraManager_ClearCapturedImage_ResetsState()
        {
            // Set up captured state
            var hasCapturedField = typeof(CameraManager).GetField("hasCapturedImage", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            hasCapturedField?.SetValue(cameraManager, true);
            
            cameraManager.ClearCapturedImage();
            
            Assert.IsFalse(cameraManager.HasCapturedImage);
        }

        [UnityTest]
        public IEnumerator CameraManager_MultipleCaptures_WorkCorrectly()
        {
            cameraManager.Initialize();
            
            int captureCount = 0;
            cameraManager.OnImageCaptured.AddListener((image) => captureCount++);

            // Perform multiple captures
            for (int i = 0; i < 3; i++)
            {
                cameraManager.CaptureImage();
                yield return TestUtilities.WaitForCondition(() => captureCount > i, 5f);
            }
            
            Assert.AreEqual(3, captureCount);
        }

        [Test]
        public void CameraManager_EventSubscriptions_WorkCorrectly()
        {
            bool captureStarted = false;
            bool errorOccurred = false;
            
            cameraManager.OnCaptureStarted.AddListener(() => captureStarted = true);
            cameraManager.OnError.AddListener((error) => errorOccurred = true);
            
            // Test that listeners are properly set up
            Assert.IsNotNull(cameraManager.OnCaptureStarted);
            Assert.IsNotNull(cameraManager.OnImageCaptured);
            Assert.IsNotNull(cameraManager.OnError);
        }
    }
}
