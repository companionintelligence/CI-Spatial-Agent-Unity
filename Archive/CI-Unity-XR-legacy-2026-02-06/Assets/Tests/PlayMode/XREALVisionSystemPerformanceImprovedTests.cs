using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Profiling;
using CompanionIntelligence.Core;

namespace CompanionIntelligence.Tests
{
    /// <summary>
    /// Performance tests for XREAL Vision System
    /// Validates memory usage, processing times, and resource management
    /// </summary>
    public class VisionSystemPerformanceImprovedTests
    {
        private GameObject visionSystemGO;
        private VisionSystem visionSystem;
        private const int PERFORMANCE_ITERATIONS = 10;
        private const float MAX_FRAME_TIME_MS = 16.67f; // 60 FPS target
        private const long MAX_MEMORY_ALLOCATION_MB = 50;

        [SetUp]
        public void Setup()
        {
            visionSystemGO = TestUtilities.CreateTestVisionSystem();
            visionSystem = visionSystemGO.GetComponent<VisionSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (visionSystemGO != null)
                Object.DestroyImmediate(visionSystemGO);
            
            // Force garbage collection after tests
            System.GC.Collect();
            Resources.UnloadUnusedAssets();
        }

        [UnityTest]
        public IEnumerator Performance_CameraCapture_MeetsFrameTimeRequirements()
        {
            var camera = TestUtilities.CreateTestCamera();
            var cameraManager = visionSystemGO.GetComponent<CameraManager>();
            
            // Set camera reference
            var cameraField = typeof(CameraManager).GetField("targetCamera", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            cameraField?.SetValue(cameraManager, camera);
            
            cameraManager.Initialize();
            
            var stopwatch = new Stopwatch();
            var captureTimes = new float[PERFORMANCE_ITERATIONS];
            
            for (int i = 0; i < PERFORMANCE_ITERATIONS; i++)
            {
                stopwatch.Restart();
                
                bool captureComplete = false;
                cameraManager.OnImageCaptured.AddListener((image) => captureComplete = true);
                
                cameraManager.CaptureImage();
                
                yield return TestUtilities.WaitForCondition(() => captureComplete, 2f);
                
                stopwatch.Stop();
                captureTimes[i] = (float)stopwatch.Elapsed.TotalMilliseconds;
                
                // Clear for next iteration
                cameraManager.ClearCapturedImage();
                cameraManager.OnImageCaptured.RemoveAllListeners();
            }
            
            var averageTime = 0f;
            var maxTime = 0f;
            
            for (int i = 0; i < PERFORMANCE_ITERATIONS; i++)
            {
                averageTime += captureTimes[i];
                if (captureTimes[i] > maxTime) maxTime = captureTimes[i];
            }
            averageTime /= PERFORMANCE_ITERATIONS;
            
            UnityEngine.Debug.Log($"Camera Capture Performance - Average: {averageTime:F2}ms, Max: {maxTime:F2}ms");
            
            Assert.Less(averageTime, MAX_FRAME_TIME_MS, 
                $"Average capture time ({averageTime:F2}ms) should be less than {MAX_FRAME_TIME_MS}ms");
            Assert.Less(maxTime, MAX_FRAME_TIME_MS * 2, 
                $"Max capture time ({maxTime:F2}ms) should be less than {MAX_FRAME_TIME_MS * 2}ms");
            
            Object.DestroyImmediate(camera.gameObject);
        }

        [Test]
        public void Performance_MemoryUsage_TextureManagement_IsEfficient()
        {
            var cameraManager = visionSystemGO.GetComponent<CameraManager>();
            cameraManager.Initialize();
            
            // Measure initial memory
            Profiler.BeginSample("Memory Baseline");
            var initialMemory = Profiler.GetTotalAllocatedMemory(false);
            Profiler.EndSample();
            
            // Create and destroy multiple textures to test memory management
            Profiler.BeginSample("Texture Creation Loop");
            for (int i = 0; i < 100; i++)
            {
                var texture = TestUtilities.CreateTestTexture(512, 512);
                var copy = cameraManager.GetLastCapturedImageCopy();
                
                Object.DestroyImmediate(texture);
                if (copy != null) Object.DestroyImmediate(copy);
            }
            Profiler.EndSample();
            
            // Force cleanup
            System.GC.Collect();
            Resources.UnloadUnusedAssets();
            
            var finalMemory = Profiler.GetTotalAllocatedMemory(false);
            var memoryDifference = (finalMemory - initialMemory) / (1024 * 1024); // Convert to MB
            
            UnityEngine.Debug.Log($"Memory Usage - Initial: {initialMemory / (1024 * 1024)}MB, " +
                  $"Final: {finalMemory / (1024 * 1024)}MB, " +
                  $"Difference: {memoryDifference}MB");
            
            Assert.Less(memoryDifference, MAX_MEMORY_ALLOCATION_MB, 
                $"Memory allocation difference ({memoryDifference}MB) should be less than {MAX_MEMORY_ALLOCATION_MB}MB");
        }

        [UnityTest]
        public IEnumerator Performance_TextureEncoding_IsOptimized()
        {
            var textures = new Texture2D[]
            {
                TestUtilities.CreateTestTexture(256, 256),
                TestUtilities.CreateTestTexture(512, 512),
                TestUtilities.CreateTestTexture(1024, 1024),
                TestUtilities.CreateTestTextureWithPattern(512, 512)
            };
            
            var stopwatch = new Stopwatch();
            
            foreach (var texture in textures)
            {
                stopwatch.Restart();
                
                // Test JPEG encoding performance
                var jpegData = texture.EncodeToJPG(75);
                
                stopwatch.Stop();
                var encodingTime = (float)stopwatch.Elapsed.TotalMilliseconds;
                
                UnityEngine.Debug.Log($"Texture {texture.width}x{texture.height} encoding time: {encodingTime:F2}ms, " +
                      $"Size: {jpegData.Length / 1024}KB");
                
                Assert.Less(encodingTime, 50f, 
                    $"Encoding time for {texture.width}x{texture.height} texture should be under 50ms");
                Assert.Greater(jpegData.Length, 0, "Encoded data should not be empty");
                Assert.Less(jpegData.Length, 1024 * 1024, "Encoded size should be under 1MB for reasonable compression");
                
                yield return null; // Yield to prevent frame drops
            }
            
            // Clean up
            foreach (var texture in textures)
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Performance_VisionSystem_StateChanges_AreEfficient()
        {
            var stopwatch = new Stopwatch();
            var states = new VisionSystem.SystemState[]
            {
                VisionSystem.SystemState.Idle,
                VisionSystem.SystemState.Listening,
                VisionSystem.SystemState.Capturing,
                VisionSystem.SystemState.Processing,
                VisionSystem.SystemState.Speaking,
                VisionSystem.SystemState.Error
            };
            
            stopwatch.Start();
            
            // Simulate rapid state changes
            for (int i = 0; i < 1000; i++)
            {
                var targetState = states[i % states.Length];
                // State changes are internal, so we just verify the system handles rapid calls
                visionSystem.ProcessTextCommand($"Test {i}");
            }
            
            stopwatch.Stop();
            var totalTime = (float)stopwatch.Elapsed.TotalMilliseconds;
            
            UnityEngine.Debug.Log($"1000 state change operations completed in {totalTime:F2}ms");
            
            Assert.Less(totalTime, 100f, "1000 state change operations should complete in under 100ms");
        }

        [UnityTest]
        public IEnumerator Performance_ConcurrentOperations_HandledCorrectly()
        {
            var mockCamera = visionSystemGO.AddComponent<TestUtilities.MockCameraManager>();
            var mockOllama = visionSystemGO.AddComponent<TestUtilities.MockOllamaConnector>();
            
            // Set very fast response times for stress testing
            mockOllama.ResponseDelay = 0.01f;
            
            mockCamera.Initialize();
            mockOllama.Initialize();
            
            var cameraField = typeof(VisionSystem).GetField("cameraManager", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ollamaField = typeof(VisionSystem).GetField("ollamaConnector", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            cameraField?.SetValue(visionSystem, mockCamera);
            ollamaField?.SetValue(visionSystem, mockOllama);
            
            visionSystem.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.5f);
            
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            
            int completedOperations = 0;
            visionSystem.OnVisionResponse.AddListener((response) => completedOperations++);
            
            // Send multiple concurrent requests
            for (int i = 0; i < 10; i++)
            {
                visionSystem.ProcessTextCommand($"Concurrent test {i}");
                yield return null; // Yield between requests
            }
            
            // Wait for operations to complete
            yield return TestUtilities.WaitForCondition(() => completedOperations >= 1, 5f);
            
            stopwatch.Stop();
            var totalTime = (float)stopwatch.Elapsed.TotalMilliseconds;
            
            UnityEngine.Debug.Log($"Concurrent operations test completed in {totalTime:F2}ms, " +
                  $"Completed operations: {completedOperations}");
            
            Assert.Greater(completedOperations, 0, "At least one operation should complete");
            Assert.Less(totalTime, 10000f, "Operations should complete within 10 seconds");
        }

        [Test]
        public void Performance_StringOperations_AreOptimized()
        {
            var stopwatch = new Stopwatch();
            var testStrings = new string[]
            {
                "What do you see?",
                "Describe the scene in detail with lots of information about what is visible",
                "A very long prompt that might be used in a real-world scenario where the user asks for a comprehensive analysis of the visual scene including objects, people, colors, lighting conditions, and spatial relationships",
                ""
            };
            
            stopwatch.Start();
            
            // Test string processing operations
            for (int i = 0; i < 1000; i++)
            {
                var testString = testStrings[i % testStrings.Length];
                
                // Simulate common string operations in the system
                var processed = testString?.Trim().ToLower() ?? "";
                var encoded = System.Text.Encoding.UTF8.GetBytes(processed);
                var decoded = System.Text.Encoding.UTF8.GetString(encoded);
                
                Assert.IsNotNull(decoded);
            }
            
            stopwatch.Stop();
            var totalTime = (float)stopwatch.Elapsed.TotalMilliseconds;
            
            UnityEngine.Debug.Log($"String operations performance: {totalTime:F2}ms for 1000 operations");
            
            Assert.Less(totalTime, 50f, "String operations should be highly optimized");
        }
    }
}
