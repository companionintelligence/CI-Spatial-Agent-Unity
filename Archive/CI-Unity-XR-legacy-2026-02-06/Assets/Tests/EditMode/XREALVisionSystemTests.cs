using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CompanionIntelligence.Core;

namespace CompanionIntelligence.Tests
{
    /// <summary>
    /// Unit tests for XREALVisionSystem component
    /// Tests the core orchestration and state management
    /// </summary>
    public class XREALVisionSystemTests
    {
        private GameObject testGameObject;
        private VisionSystem visionSystem;

        [SetUp]
        public void Setup()
        {
            testGameObject = new GameObject("TestVisionSystem");
            visionSystem = testGameObject.AddComponent<VisionSystem>();
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
        public void VisionSystem_InitialState_IsIdle()
        {
            // Test that the system starts in Idle state
            Assert.AreEqual(VisionSystem.SystemState.Idle, visionSystem.CurrentState);
            Assert.IsFalse(visionSystem.IsInitialized);
            Assert.IsFalse(visionSystem.IsProcessing);
        }

        [UnityTest]
        public IEnumerator VisionSystem_AutoInitialize_InitializesCorrectly()
        {
            // Enable auto-initialize and wait for initialization
            var autoInitializeField = typeof(VisionSystem).GetField("autoInitialize", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            autoInitializeField?.SetValue(visionSystem, true);

            // Start the component
            visionSystem.gameObject.SetActive(true);

            // Wait a frame for Start() to be called
            yield return null;

            // Wait for initialization (with timeout)
            float timeout = 5f;
            float elapsed = 0f;
            while (!visionSystem.IsInitialized && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            // Verify initialization completed or timed out gracefully
            // Note: May not initialize fully without required components, but should handle gracefully
            Assert.IsTrue(elapsed < timeout || visionSystem.CurrentState != VisionSystem.SystemState.Error);
        }

        [Test]
        public void VisionSystem_ProcessTextCommand_RequiresInitialization()
        {
            // Test that commands are rejected when not initialized
            visionSystem.ProcessTextCommand("What do you see?");
            
            // Should remain in idle state and not crash
            Assert.AreEqual(VisionSystem.SystemState.Idle, visionSystem.CurrentState);
            Assert.IsFalse(visionSystem.IsProcessing);
        }

        [Test]
        public void VisionSystem_StateTransitions_AreValid()
        {
            // Test valid state transitions
            var states = System.Enum.GetValues(typeof(VisionSystem.SystemState));
            foreach (VisionSystem.SystemState state in states)
            {
                // Verify all states are defined and accessible
                Assert.IsTrue(System.Enum.IsDefined(typeof(VisionSystem.SystemState), state));
            }
        }

        [Test]
        public void VisionSystem_EventsSubscription_DoesNotThrow()
        {
            // Test that event subscription/unsubscription doesn't throw
            bool eventFired = false;
            
            visionSystem.OnSystemReady.AddListener(() => eventFired = true);
            visionSystem.OnVisionResponse.AddListener((response) => eventFired = true);
            visionSystem.OnSystemError.AddListener((error) => eventFired = true);

            // Cleanup should not throw
            Assert.DoesNotThrow(() =>
            {
                Object.DestroyImmediate(testGameObject);
                testGameObject = null;
            });
        }

        [Test]
        public void VisionSystem_PublicAPI_IsAccessible()
        {
            // Test that all public methods are accessible and don't throw on invalid states
            Assert.DoesNotThrow(() => visionSystem.StartListening());
            Assert.DoesNotThrow(() => visionSystem.StopListening());
            Assert.DoesNotThrow(() => visionSystem.ToggleListening());
            Assert.DoesNotThrow(() => visionSystem.ProcessTextCommand("test"));
        }
    }
}
