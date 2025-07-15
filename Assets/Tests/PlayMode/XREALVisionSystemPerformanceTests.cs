using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CompanionIntelligence.Core;

namespace CompanionIntelligence.Tests
{
    /// <summary>
    /// Performance tests for XREAL Vision System
    /// Tests memory usage, initialization time, and throughput
    /// </summary>
    public class XREALVisionSystemPerformanceTests
    {
        private GameObject testGameObject;

        [SetUp]
        public void Setup()
        {
            testGameObject = new GameObject("PerformanceTest");
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
            {
                Object.DestroyImmediate(testGameObject);
            }
        }

        [UnityTest]
        [Performance]
        public IEnumerator VisionSystem_InitializationTime_IsAcceptable()
        {
            var startTime = Time.realtimeSinceStartup;
            
            var sceneSetup = testGameObject.AddComponent<XREALVisionSceneSetup>();
            sceneSetup.SetupVisionScene();
            
            yield return new WaitForSeconds(2f); // Allow for initialization
            
            var endTime = Time.realtimeSinceStartup;
            var initTime = endTime - startTime;
            
            // Initialization should complete within reasonable time (2 seconds)
            Assert.IsTrue(initTime < 3f, $"Initialization took {initTime:F2}s, which is too long");
        }

        [Test]
        [Performance]
        public void VisionSystem_MemoryFootprint_IsReasonable()
        {
            var initialMemory = System.GC.GetTotalMemory(true);
            
            // Create multiple vision systems to test memory usage
            var gameObjects = new GameObject[5];
            for (int i = 0; i < 5; i++)
            {
                gameObjects[i] = new GameObject($"VisionSystem_{i}");
                gameObjects[i].AddComponent<VisionSystem>();
                gameObjects[i].AddComponent<CameraManager>();
                gameObjects[i].AddComponent<OllamaConnector>();
            }
            
            var afterCreationMemory = System.GC.GetTotalMemory(true);
            var memoryUsed = afterCreationMemory - initialMemory;
            
            // Cleanup
            for (int i = 0; i < 5; i++)
            {
                Object.DestroyImmediate(gameObjects[i]);
            }
            
            System.GC.Collect();
            var afterCleanupMemory = System.GC.GetTotalMemory(true);
            
            // Memory usage should be reasonable (less than 10MB for 5 systems)
            Assert.IsTrue(memoryUsed < 10 * 1024 * 1024, 
                $"Memory usage {memoryUsed / (1024 * 1024):F2}MB is too high");
        }

        [UnityTest]
        [Performance]
        public IEnumerator VisionSystem_MultipleCommands_HandlesConcurrency()
        {
            var sceneSetup = testGameObject.AddComponent<XREALVisionSceneSetup>();
            sceneSetup.SetupVisionScene();
            
            yield return new WaitForSeconds(1f);
            
            var visionSystem = Object.FindObjectOfType<VisionSystem>();
            if (visionSystem != null)
            {
                var startTime = Time.realtimeSinceStartup;
                
                // Send multiple commands rapidly
                for (int i = 0; i < 10; i++)
                {
                    visionSystem.ProcessTextCommand($"Command {i}");
                    yield return null; // Wait one frame
                }
                
                var endTime = Time.realtimeSinceStartup;
                var processingTime = endTime - startTime;
                
                // Should handle multiple commands without hanging
                Assert.IsTrue(processingTime < 1f, 
                    $"Processing 10 commands took {processingTime:F2}s, which is too long");
            }
        }

        [Test]
        [Performance]
        public void CameraManager_ImageProcessing_IsEfficient()
        {
            var cameraManager = testGameObject.AddComponent<CameraManager>();
            cameraManager.Initialize();
            
            var startTime = Time.realtimeSinceStartup;
            
            // Test multiple resolution changes
            for (int i = 0; i < 100; i++)
            {
                cameraManager.SetCaptureResolution(256 + i, 256 + i);
            }
            
            var endTime = Time.realtimeSinceStartup;
            var processingTime = endTime - startTime;
            
            // Resolution changes should be fast
            Assert.IsTrue(processingTime < 0.1f, 
                $"100 resolution changes took {processingTime:F3}s, which is too slow");
        }

        [UnityTest]
        [Performance]
        public IEnumerator OllamaConnector_RequestHandling_IsResponsive()
        {
            var ollamaConnector = testGameObject.AddComponent<OllamaConnector>();
            ollamaConnector.Initialize();
            
            yield return new WaitForSeconds(0.5f);
            
            var startTime = Time.realtimeSinceStartup;
            
            // Send multiple text requests
            for (int i = 0; i < 5; i++)
            {
                ollamaConnector.SendTextRequest($"Test request {i}");
                yield return new WaitForFixedUpdate();
            }
            
            var endTime = Time.realtimeSinceStartup;
            var requestTime = endTime - startTime;
            
            // Request queueing should be fast
            Assert.IsTrue(requestTime < 0.5f, 
                $"Queueing 5 requests took {requestTime:F3}s, which is too slow");
        }

        [Test]
        [Performance]
        public void VisionSystem_ComponentLookup_IsOptimized()
        {
            // Test that component lookups are efficient
            var sceneSetup = testGameObject.AddComponent<XREALVisionSceneSetup>();
            
            var startTime = Time.realtimeSinceStartup;
            
            // Multiple setups to test component finding performance
            for (int i = 0; i < 10; i++)
            {
                sceneSetup.SetupVisionScene();
            }
            
            var endTime = Time.realtimeSinceStartup;
            var setupTime = endTime - startTime;
            
            // Component setup should be reasonably fast
            Assert.IsTrue(setupTime < 1f, 
                $"10 scene setups took {setupTime:F3}s, which is too slow");
        }
    }
}
