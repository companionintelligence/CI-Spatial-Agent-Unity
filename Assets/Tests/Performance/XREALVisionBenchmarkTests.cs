using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CompanionIntelligence.Core;

namespace CompanionIntelligence.Tests
{
    /// <summary>
    /// Performance benchmark tests for XREAL Vision System
    /// Provides detailed performance analysis and regression testing
    /// </summary>
    [Category("Performance")]
    public class XREALVisionBenchmarkTests
    {
        private const float FRAME_TIME_THRESHOLD_MS = 16.67f; // 60 FPS target
        private const long MEMORY_THRESHOLD_BYTES = 100 * 1024 * 1024; // 100MB threshold
        private const int STRESS_TEST_ITERATIONS = 100;
        
        private GameObject benchmarkSystem;
        private XREALPerformanceTracker performanceTracker;
        private Stopwatch stopwatch;

        [SetUp]
        public void Setup()
        {
            benchmarkSystem = new GameObject("BenchmarkSystem");
            performanceTracker = benchmarkSystem.AddComponent<XREALPerformanceTracker>();
            stopwatch = new Stopwatch();
            
            XREALPerformanceMonitor.ClearMetrics();
            XREALPerformanceMonitor.IsEnabled = true;
            
            // Force garbage collection before benchmarks
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
        }

        [TearDown]
        public void TearDown()
        {
            if (benchmarkSystem != null)
            {
                Object.Destroy(benchmarkSystem);
            }
            
            stopwatch?.Stop();
            XREALPerformanceMonitor.ClearMetrics();
        }

        [Test]
        public void ImageProcessing_Benchmark_MeetsPerformanceRequirements()
        {
            // Benchmark image processing operations
            var testImage = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            var results = new List<float>();

            for (int i = 0; i < 10; i++)
            {
                stopwatch.Restart();
                
                // Simulate image processing operations
                XREALPerformanceMonitor.StartTimer("ImageProcessing");
                
                // Convert texture to bytes (common operation)
                var imageBytes = testImage.EncodeToJPG(75);
                
                // Simulate processing delay
                System.Threading.Thread.Sleep(1);
                
                XREALPerformanceMonitor.StopTimer("ImageProcessing");
                stopwatch.Stop();
                
                results.Add((float)stopwatch.Elapsed.TotalMilliseconds);
            }

            // Analyze results
            float averageTime = CalculateAverage(results);
            float maxTime = CalculateMax(results);
            
            Assert.Less(averageTime, 50f, $"Average image processing time should be under 50ms, was {averageTime:F2}ms");
            Assert.Less(maxTime, 100f, $"Max image processing time should be under 100ms, was {maxTime:F2}ms");
            
            // Cleanup
            Object.DestroyImmediate(testImage);
        }

        [Test]
        public void TextureCreation_Benchmark_EfficientMemoryUsage()
        {
            // Benchmark texture creation and memory usage
            var initialMemory = System.GC.GetTotalMemory(false);
            var textures = new List<Texture2D>();
            
            stopwatch.Restart();
            
            // Create multiple textures
            for (int i = 0; i < 10; i++)
            {
                var texture = new Texture2D(512, 512, TextureFormat.RGB24, false);
                textures.Add(texture);
            }
            
            stopwatch.Stop();
            var creationTime = stopwatch.Elapsed.TotalMilliseconds;
            var memoryAfterCreation = System.GC.GetTotalMemory(false);
            
            // Cleanup textures
            foreach (var texture in textures)
            {
                Object.DestroyImmediate(texture);
            }
            
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            
            var finalMemory = System.GC.GetTotalMemory(false);
            var memoryUsed = memoryAfterCreation - initialMemory;
            var memoryLeaked = finalMemory - initialMemory;
            
            Assert.Less(creationTime, 100, $"Texture creation should be under 100ms, was {creationTime:F2}ms");
            Assert.Less(memoryLeaked, 10 * 1024 * 1024, $"Memory leak should be under 10MB, was {memoryLeaked / (1024*1024):F2}MB");
        }

        [UnityTest]
        public IEnumerator StressTest_ConcurrentOperations_MaintainsPerformance()
        {
            // Stress test with concurrent operations
            var operationCount = 0;
            var errors = new List<string>();
            var startTime = Time.realtimeSinceStartup;
            
            // Start multiple concurrent operations
            for (int i = 0; i < 20; i++)
            {
                StartOperation(i, () => operationCount++, errors.Add);
            }
            
            // Monitor for 10 seconds
            while (Time.realtimeSinceStartup - startTime < 10f)
            {
                XREALPerformanceMonitor.RecordFrameTime();
                
                // Check frame time performance
                if (Time.deltaTime > FRAME_TIME_THRESHOLD_MS / 1000f)
                {
                    errors.Add($"Frame time exceeded threshold: {Time.deltaTime * 1000f:F2}ms");
                }
                
                yield return null;
            }
            
            // Analyze results
            var frameTimeMetric = XREALPerformanceMonitor.GetMetric("FrameTime");
            Assert.IsNotNull(frameTimeMetric, "Frame time should be tracked");
            Assert.Greater(operationCount, 0, "Some operations should have completed");
            Assert.Less(errors.Count, 5, $"Should have fewer than 5 performance issues, had {errors.Count}");
            
            if (errors.Count > 0)
            {
                UnityEngine.Debug.LogWarning($"Performance issues detected:\n{string.Join("\n", errors)}");
            }
        }

        [Test]
        public void MemoryAllocation_Benchmark_EfficientGarbageCollection()
        {
            // Benchmark memory allocation patterns
            var initialMemory = System.GC.GetTotalMemory(false);
            var allocations = new List<byte[]>();
            
            stopwatch.Restart();
            
            // Simulate typical allocation patterns
            for (int i = 0; i < 1000; i++)
            {
                // Allocate various sizes (simulating image data, text, etc.)
                allocations.Add(new byte[1024]); // 1KB
                if (i % 10 == 0) allocations.Add(new byte[64 * 1024]); // 64KB
                if (i % 100 == 0) allocations.Add(new byte[512 * 1024]); // 512KB
            }
            
            stopwatch.Stop();
            var allocationTime = stopwatch.Elapsed.TotalMilliseconds;
            var memoryAfterAllocation = System.GC.GetTotalMemory(false);
            
            // Clear allocations
            allocations.Clear();
            allocations = null;
            
            // Force garbage collection
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            
            var finalMemory = System.GC.GetTotalMemory(false);
            var memoryRecovered = memoryAfterAllocation - finalMemory;
            var memoryLeaked = finalMemory - initialMemory;
            
            Assert.Less(allocationTime, 1000, $"Memory allocation should be under 1000ms, was {allocationTime:F2}ms");
            Assert.Greater(memoryRecovered, 0, "Some memory should be recovered after GC");
            Assert.Less(memoryLeaked, 5 * 1024 * 1024, $"Memory leak should be under 5MB, was {memoryLeaked / (1024*1024):F2}MB");
        }

        [Test]
        public void ConfigurationAccess_Benchmark_FastLookup()
        {
            // Benchmark configuration access performance
            var config = ScriptableObject.CreateInstance<XREALVisionConfig>();
            var results = new List<float>();
            
            for (int i = 0; i < 1000; i++)
            {
                stopwatch.Restart();
                
                // Access configuration properties
                var profile = config.GetCurrentProfile();
                var isValid = config.ValidateConfig();
                
                stopwatch.Stop();
                results.Add((float)stopwatch.Elapsed.TotalMicroseconds);
            }
            
            float averageTime = CalculateAverage(results);
            float maxTime = CalculateMax(results);
            
            Assert.Less(averageTime, 100, $"Average config access should be under 100μs, was {averageTime:F2}μs");
            Assert.Less(maxTime, 1000, $"Max config access should be under 1ms, was {maxTime:F2}μs");
            
            Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator SystemStartup_Benchmark_FastInitialization()
        {
            // Benchmark system startup time
            var visionSystemObject = new GameObject("VisionSystemBenchmark");
            
            stopwatch.Restart();
            
            var visionSystem = visionSystemObject.AddComponent<VisionSystem>();
            
            // Wait for initialization
            while (!visionSystem.IsInitialized && stopwatch.ElapsedMilliseconds < 5000)
            {
                yield return null;
            }
            
            stopwatch.Stop();
            var initializationTime = stopwatch.Elapsed.TotalMilliseconds;
            
            Assert.IsTrue(visionSystem.IsInitialized, "System should initialize successfully");
            Assert.Less(initializationTime, 3000, $"Initialization should be under 3 seconds, was {initializationTime:F2}ms");
            
            Object.Destroy(visionSystemObject);
        }

        [Test]
        public void PerformanceMonitor_Overhead_Minimal()
        {
            // Benchmark the performance monitor itself
            var iterations = 10000;
            
            // Test without monitoring
            stopwatch.Restart();
            for (int i = 0; i < iterations; i++)
            {
                // Simulate work
                var dummy = Mathf.Sin(i * 0.01f);
            }
            stopwatch.Stop();
            var timeWithoutMonitoring = stopwatch.Elapsed.TotalMicroseconds;
            
            // Test with monitoring
            stopwatch.Restart();
            for (int i = 0; i < iterations; i++)
            {
                XREALPerformanceMonitor.StartTimer("TestOperation");
                var dummy = Mathf.Sin(i * 0.01f);
                XREALPerformanceMonitor.StopTimer("TestOperation");
            }
            stopwatch.Stop();
            var timeWithMonitoring = stopwatch.Elapsed.TotalMicroseconds;
            
            var overhead = timeWithMonitoring - timeWithoutMonitoring;
            var overheadPercentage = (overhead / timeWithoutMonitoring) * 100;
            
            Assert.Less(overheadPercentage, 20, $"Performance monitoring overhead should be under 20%, was {overheadPercentage:F2}%");
        }

        [Test]
        public void DataSerialization_Benchmark_EfficientProcessing()
        {
            // Benchmark JSON serialization/deserialization
            var testData = new Dictionary<string, object>
            {
                ["prompt"] = "Test vision prompt",
                ["temperature"] = 0.7f,
                ["max_tokens"] = 150,
                ["model"] = "llava:7b"
            };
            
            var results = new List<float>();
            
            for (int i = 0; i < 100; i++)
            {
                stopwatch.Restart();
                
                var json = JsonUtility.ToJson(testData);
                var deserialized = JsonUtility.FromJson<Dictionary<string, object>>(json);
                
                stopwatch.Stop();
                results.Add((float)stopwatch.Elapsed.TotalMicroseconds);
            }
            
            float averageTime = CalculateAverage(results);
            Assert.Less(averageTime, 1000, $"Average serialization time should be under 1ms, was {averageTime:F2}μs");
        }

        // Helper Methods
        private void StartOperation(int id, System.Action onComplete, System.Action<string> onError)
        {
            try
            {
                // Simulate async operation
                var operation = new GameObject($"Operation_{id}");
                var coroutineRunner = operation.AddComponent<CoroutineRunner>();
                coroutineRunner.StartCoroutine(SimulateAsyncWork(onComplete, onError, operation));
            }
            catch (System.Exception ex)
            {
                onError?.Invoke($"Operation {id} failed: {ex.Message}");
            }
        }

        private IEnumerator SimulateAsyncWork(System.Action onComplete, System.Action<string> onError, GameObject cleanup)
        {
            try
            {
                // Simulate work with random delay
                var workTime = Random.Range(0.1f, 2.0f);
                yield return new WaitForSeconds(workTime);
                
                onComplete?.Invoke();
            }
            catch (System.Exception ex)
            {
                onError?.Invoke($"Async work failed: {ex.Message}");
            }
            finally
            {
                if (cleanup != null)
                {
                    Object.Destroy(cleanup);
                }
            }
        }

        private float CalculateAverage(List<float> values)
        {
            if (values.Count == 0) return 0;
            
            float sum = 0;
            foreach (var value in values)
            {
                sum += value;
            }
            return sum / values.Count;
        }

        private float CalculateMax(List<float> values)
        {
            if (values.Count == 0) return 0;
            
            float max = values[0];
            foreach (var value in values)
            {
                if (value > max) max = value;
            }
            return max;
        }

        // Helper class for running coroutines in tests
        private class CoroutineRunner : MonoBehaviour { }
    }
}
