using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using XRScan.Core;
using XRScan.Settings;

namespace XRScan.Tests.Config
{
    /// <summary>
    /// Privacy launch condition S2 (EU AI Act Art. 50(1)): a one-time AI notice at first launch that does not depend
    /// on the server settings, and a permanent line in the settings panel, in the wording of privacy/DECISIONS.md.
    /// </summary>
    public class AiDisclosureTests
    {
        private OverrideSandbox _sandbox;
        private CompanionConfig _companion;
        private GameObject _go;
        private CompanionSettingsPanel _panel;

        [SetUp]
        public void SetUp()
        {
            _sandbox = new OverrideSandbox();
            _companion = ScriptableObject.CreateInstance<CompanionConfig>();
            _companion.llm = ScriptableObject.CreateInstance<LlmConfig>();
            _companion.voice = ScriptableObject.CreateInstance<VoiceConfig>();
            _companion.server = ScriptableObject.CreateInstance<ServerConfig>();
            _go = new GameObject("settings");
            _panel = _go.AddComponent<CompanionSettingsPanel>();
            _panel.Configure(_companion, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_companion.llm);
            Object.DestroyImmediate(_companion.voice);
            Object.DestroyImmediate(_companion.server);
            Object.DestroyImmediate(_companion);
            _sandbox.Dispose();
        }

        private string TextOf(string canvas) =>
            _go.GetComponentsInChildren<Text>(true).Single(t => t.transform.parent.name == canvas && t.name != "Footer").text;

        [Test]
        public void TheLine_IsWordedAsDecisionsS2Says()
        {
            Assert.AreEqual(
                "Answers come from an AI language model and can be wrong. Privacy policy: ci.computer/privacy/spatial-companion",
                AiDisclosure.Line);
        }

        [Test]
        public void TheNotice_IsDueWhateverTheServerSettings_UntilItIsClosed()
        {
            // A reviewer build with every server preset: the settings panel would not open by itself.
            _companion.llm.endpoint = "https://llm.example.lan";
            _companion.voice.selfHostedBaseUrl = "https://speech.example.lan";
            _companion.server.baseUrl = "https://vision.example.lan";
            Assert.IsFalse(_companion.NeedsSetup(false, new string[0]));
            Assert.IsTrue(AiDisclosure.NoticeNeeded);

            AiDisclosure.Acknowledge();
            Assert.IsFalse(AiDisclosure.NoticeNeeded);
            Assert.AreEqual(AiDisclosure.NoticeVersion, _sandbox.Store.GetString(AiDisclosure.AcknowledgedKey));

            new CompanionSettingsModel(_companion, false, new string[0]).ResetAll();
            Assert.IsFalse(AiDisclosure.NoticeNeeded, "resetting the settings does not bring the notice back");
        }

        [Test]
        public void ThePanel_ShowsTheNotice_AndClosingItRecordsIt()
        {
            _panel.ShowAiNotice();
            Assert.IsTrue(_panel.IsAiNoticeOpen);
            StringAssert.Contains(AiDisclosure.Line, TextOf("AiNoticeCanvas"));

            _panel.Open();
            Assert.IsFalse(_panel.IsOpen, "the notice is answered first");

            _panel.AcknowledgeAiNotice();
            Assert.IsFalse(_panel.IsAiNoticeOpen);
            Assert.IsFalse(AiDisclosure.NoticeNeeded);
            Assert.IsFalse(_go.GetComponentsInChildren<Canvas>(true).Single(c => c.name == "AiNoticeCanvas").gameObject.activeSelf);
        }

        [Test]
        public void ThePanel_KeepsTheLineAtTheTop()
        {
            _panel.Open();
            string body = TextOf("SettingsPanelCanvas");
            StringAssert.Contains(AiDisclosure.Line, body);
            Assert.Less(body.IndexOf(AiDisclosure.Line, System.StringComparison.Ordinal), body.IndexOf("LLM server", System.StringComparison.Ordinal));
        }
    }
}
