using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CompanionIntelligence.Core;
using CompanionIntelligence.Tests.Mocks;

namespace CompanionIntelligence.Tests
{
    /// <summary>
    /// Performance tests for XREAL Vision System
    /// Ensures the system meets performance requirements and handles stress conditions
    /// </summary>
    public class XREALPerformanceTests
    {
        private GameObject testGameObject;
        private VisionSystem visionSystem;
        private XREALPerformanceTracker performanceTracker;

        [SetUp]
        public void Setup()
        {
            testGameObject = new GameObject("PerformanceTestSystem");
            visionSystem = testGameObject.AddComponent<VisionSystem>();
            performanceTracker = testGameObject.AddComponent<XREALPerformanceTracker>();
            
            // Setup mock components for testing
            var mockCameraManager = testGameObject.AddComponent<MockCameraManager>();
            var mockOllamaConnector = testGameObject.AddComponent<MockOllamaConnector>();
            var mockSpeechToText = testGameObject.AddComponent<MockSpeechToText>();
            var mockTextToSpeech = testGameObject.AddComponent<MockTextToSpeech>();
            
            // Clear performance metrics
            XREALPerformanceMonitor.ClearMetrics();
            XREALPerformanceMonitor.IsEnabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
            {
                Object.DestroyImmediate(testGameObject);
            }
            
            XREALPerformanceMonitor.ClearMetrics();
        }

        [Test]
        public void PerformanceMonitor_BasicOperations_PerformWithinLimits()
        {
            // Test basic performance monitoring functionality
            const string metricName = "TestOperation";
            const int iterations = 1000;
            
            XREALPerformanceMonitor.StartTimer(metricName);
            
            // Simulate some work
            for (int i = 0; i < iterations; i++)
            {
                var temp = Mathf.Sin(i * 0.1f);
            }
            
            XREALPerformanceMonitor.StopTimer(metricName);
            
            var metric = XREALPerformanceMonitor.GetMetric(metricName);
            Assert.IsNotNull(metric);
            Assert.AreEqual(1, metric.executionCount);
            Assert.Greater(metric.lastExecutionTime, 0);
            Assert.Less(metric.lastExecutionTime, 100); // Should complete within 100ms
        }

        [Test]
        public void MemoryUsage_ComponentCreation_StaysWithinLimits()
        {
            // Test memory usage during component creation and destruction
            var initialMemory = System.GC.GetTotalMemory(false);
            
            // Create multiple vision system instances
            var gameObjects = new GameObject[10];
            for (int i = 0; i < gameObjects.Length; i++)
            {
                gameObjects[i] = new GameObject($"TestVisionSystem_{i}");
                gameObjects[i].AddComponent<VisionSystem>();
                gameObjects[i].AddComponent<MockCameraManager>();
                gameObjects[i].AddComponent<MockOllamaConnector>();
            }
            
            var peakMemory = System.GC.GetTotalMemory(false);
            
            // Clean up
            for (int i = 0; i < gameObjects.Length; i++)
            {
                Object.DestroyImmediate(gameObjects[i]);
            }
            
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            
            var finalMemory = System.GC.GetTotalMemory(false);
            
            var memoryIncrease = peakMemory - initialMemory;
            var memoryRetained = finalMemory - initialMemory;
            
            // Memory increase should be reasonable (less than 10MB for 10 instances)
            Assert.Less(memoryIncrease, 10 * 1024 * 1024, "Memory increase exceeded 10MB");
            
            // Most memory should be released (allow for some GC overhead)
            Assert.Less(memoryRetained, memoryIncrease * 0.5f, "Too much memory retained after cleanup");
        }

        [UnityTest]
        public IEnumerator VisionSystem_ConcurrentRequests_HandlesGracefully()
        {
            // Test system behavior under concurrent request load
            const int concurrentRequests = 5;
            bool[] requestsCompleted = new bool[concurrentRequests];
            
            XREALPerformanceMonitor.StartTimer("ConcurrentRequests");
            
            // Send multiple text commands simultaneously
            for (int i = 0; i < concurrentRequests; i++)
            {
                int requestIndex = i; // Capture for closure
                visionSystem.OnVisionResponse.AddListener(response => {
                    requestsCompleted[requestIndex] = true;
                });
                
                visionSystem.ProcessTextCommand($"Test command {i}");
            }
            
            // Wait for all requests to complete or timeout
            float timeout = 10.0f;
            float elapsed = 0;
            
            while (elapsed < timeout)
            {
                bool allCompleted = true;
                for (int i = 0; i < concurrentRequests; i++)
                {
                    if (!requestsCompleted[i])
                    {
                        allCompleted = false;
                        break;
                    }
                }
                
                if (allCompleted) break;
                
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            
            XREALPerformanceMonitor.StopTimer("ConcurrentRequests");
            
            // Verify system handled requests within reasonable time
            var metric = XREALPerformanceMonitor.GetMetric("ConcurrentRequests");
            Assert.IsNotNull(metric);
            Assert.Less(metric.lastExecutionTime, timeout * 1000, "Concurrent requests took too long");
            
            // Verify system state remains stable
            Assert.AreNotEqual(VisionSystem.SystemState.Error, visionSystem.CurrentState);
        }

        [Test]
        public void TextureProcessing_LargeImages_PerformsWithinLimits()
        {
            // Test performance with large texture processing
            var mockCameraManager = testGameObject.GetComponent<MockCameraManager>();
            
            XREALPerformanceMonitor.StartTimer("LargeTextureProcessing");
            
            // Test with progressively larger textures
            int[] textureSizes = { 256, 512, 1024, 2048 };
            
            foreach (int size in textureSizes)
            {
                mockCameraManager.SetCaptureResolution(size, size);
                mockCameraManager.CaptureImage();
                
                var capturedImage = mockCameraManager.GetLastCapturedImage();
                Assert.IsNotNull(capturedImage);
                Assert.AreEqual(size, capturedImage.width);
                Assert.AreEqual(size, capturedImage.height);
                
                // Test creating a copy (memory intensive operation)
                var imageCopy = mockCameraManager.GetLastCapturedImageCopy();
                Assert.IsNotNull(imageCopy);
                
                // Clean up
                Object.DestroyImmediate(imageCopy);
                mockCameraManager.ClearCapturedImage();
            }
            
            XREALPerformanceMonitor.StopTimer("LargeTextureProcessing");
            
            var metric = XREALPerformanceMonitor.GetMetric("LargeTextureProcessing");
            Assert.IsNotNull(metric);
            Assert.Less(metric.lastExecutionTime, 5000, "Large texture processing took too long (>5s)");
        }

        [UnityTest]
        public IEnumerator SystemStress_RapidStateChanges_MaintainsStability()
        {
            // Test system stability under rapid state changes
            const int stateChangeCount = 50;
            const float stateChangeInterval = 0.1f;
            
            XREALPerformanceMonitor.StartTimer("RapidStateChanges");
            
            for (int i = 0; i < stateChangeCount; i++)
            {
                // Alternate between different operations
                if (i % 3 == 0)
                {
                    visionSystem.StartListening();
                }
                else if (i % 3 == 1)
                {
                    visionSystem.StopListening();
                }
                else
                {
                    visionSystem.ProcessTextCommand($"Rapid test {i}");
                }
                
                yield return new WaitForSeconds(stateChangeInterval);
            }
            
            XREALPerformanceMonitor.StopTimer("RapidStateChanges");
            
            // Verify system is still responsive and in a valid state
            Assert.AreNotEqual(VisionSystem.SystemState.Error, visionSystem.CurrentState);
            Assert.IsFalse(visionSystem.IsProcessing); // Should not be stuck processing
            
            var metric = XREALPerformanceMonitor.GetMetric("RapidStateChanges");
            Assert.IsNotNull(metric);
            
            // Should complete all state changes within reasonable time
            Assert.Less(metric.lastExecutionTime, (stateChangeCount * stateChangeInterval + 5) * 1000);
        }

        [Test]
        public void GarbageCollection_MinimalAllocation_DuringOperation()
        {
            // Test that the system doesn't create excessive garbage during operation
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            
            var initialMemory = System.GC.GetTotalMemory(false);
            
            // Perform typical operations that shouldn't allocate much
            for (int i = 0; i < 100; i++)
            {
                XREALPerformanceMonitor.RecordMetric("TestMetric", i);
                XREALPerformanceMonitor.StartTimer("GCTest");
                XREALPerformanceMonitor.StopTimer("GCTest");
            }
            
            var finalMemory = System.GC.GetTotalMemory(false);
            var allocatedMemory = finalMemory - initialMemory;
            
            // Should allocate less than 1MB for these operations
            Assert.Less(allocatedMemory, 1024 * 1024, "Excessive memory allocation detected");
        }

        [Test]
        public void PerformanceMetrics_Accuracy_MeasuresCorrectly()
        {
            // Test that performance metrics are accurate
            const string metricName = "AccuracyTest";
            const float expectedDelay = 0.1f; // 100ms
            
            XREALPerformanceMonitor.StartTimer(metricName);
            
            // Use a more precise timing method for verification
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            System.Threading.Thread.Sleep((int)(expectedDelay * 1000));
            stopwatch.Stop();
            
            XREALPerformanceMonitor.StopTimer(metricName);
            
            var metric = XREALPerformanceMonitor.GetMetric(metricName);
            var actualTime = stopwatch.ElapsedMilliseconds;
            
            Assert.IsNotNull(metric);
            
            // Allow for some measurement tolerance (±20ms)
            Assert.AreEqual(actualTime, metric.lastExecutionTime, 20.0f, 
                "Performance measurement accuracy is outside acceptable range");
        }

        [Test]
        public void ConfigValidation_Performance_ValidatesQuickly()
        {
            // Test that configuration validation is fast
            var config = ScriptableObject.CreateInstance<XREALVisionConfig>();
            
            XREALPerformanceMonitor.StartTimer("ConfigValidation");
            
            // Validate multiple times
            for (int i = 0; i < 1000; i++)
            {
                config.ValidateConfig();
            }
            
            XREALPerformanceMonitor.StopTimer("ConfigValidation");
            
            var metric = XREALPerformanceMonitor.GetMetric("ConfigValidation");
            Assert.IsNotNull(metric);
            Assert.Less(metric.lastExecutionTime, 100, "Config validation took too long");
            
            Object.DestroyImmediate(config);
        }
    }
}
