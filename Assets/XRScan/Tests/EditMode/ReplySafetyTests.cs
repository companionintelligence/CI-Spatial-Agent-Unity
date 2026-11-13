using System;
using NUnit.Framework;
using UnityEngine;
using XRScan.Core;
using XRScan.Llm;

namespace XRScan.Tests.EditMode
{
    /// <summary>
    /// The reply safeguard the Google Play and Meta listings require of the shared code: a fixed safety prompt, sent
    /// with every request as the model's system message rather than pasted into the user's text, that no
    /// configuration can remove.
    /// </summary>
    public class ReplySafetyTests
    {
        [Serializable]
        private class Body
        {
            public string model;
            public string system;
            public string prompt;
        }

        private LlmConfig _config;

        [SetUp]
        public void SetUp()
        {
            ConfigOverrides.Store = new MemorySettingsStore();
            ConfigOverrides.EnvironmentReader = _ => null;
            _config = ScriptableObject.CreateInstance<LlmConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_config);
            ConfigOverrides.ResetToDefaults();
        }

        [Test]
        public void EveryRequest_SendsTheSafetyPromptAsTheSystemMessage()
        {
            const string question = "Scanned objects:\n- cup at (0.0, 1.0, 2.0)\n\nUser question: what is on the table?";
            var body = JsonUtility.FromJson<Body>(OllamaBackend.BuildRequestJson(_config, question, null));

            StringAssert.StartsWith(SafetyPrompt.Text, body.system, "the safety rules come first");
            StringAssert.EndsWith(_config.systemPrompt, body.system, "the persona follows them");
            Assert.AreEqual(question, body.prompt, "the user's text goes as the prompt, unchanged");
            StringAssert.DoesNotContain(SafetyPrompt.Text, body.prompt);
            Assert.AreEqual(_config.model, body.model);
        }

        [Test]
        public void TheSafetyPrompt_StaysWhenThePersonaIsEmpty()
        {
            _config.systemPrompt = "   ";
            Assert.AreEqual(SafetyPrompt.Text, _config.BuildSystemMessage());
        }

        [TestCase("human")]
        [TestCase("sexual content")]
        [TestCase("minors")]
        [TestCase("hate")]
        [TestCase("harassment")]
        [TestCase("violence")]
        [TestCase("weapons")]
        [TestCase("self-harm")]
        [TestCase("crime")]
        [TestCase("impersonates")]
        [TestCase("crisis line")]
        public void TheSafetyPrompt_CoversTheRestrictedCategories(string topic)
        {
            StringAssert.Contains(topic, SafetyPrompt.Text);
        }
    }
}
