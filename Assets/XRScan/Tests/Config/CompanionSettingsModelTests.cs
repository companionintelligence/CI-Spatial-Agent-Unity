using System.Linq;
using NUnit.Framework;
using UnityEngine;
using XRScan.Core;

namespace XRScan.Tests.Config
{
    public class CompanionSettingsModelTests
    {
        private static readonly string[] NoHosts = new string[0];

        private OverrideSandbox _sandbox;
        private CompanionConfig _companion;

        [SetUp]
        public void SetUp()
        {
            _sandbox = new OverrideSandbox();
            _companion = ScriptableObject.CreateInstance<CompanionConfig>();
            _companion.llm = ScriptableObject.CreateInstance<LlmConfig>();
            _companion.voice = ScriptableObject.CreateInstance<VoiceConfig>();
            _companion.server = ScriptableObject.CreateInstance<ServerConfig>();
            _companion.llmModelChoices.Clear();
            _companion.llmModelChoices.Add("llava:7b");
            _companion.llmModelChoices.Add("qwen2.5vl:7b");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_companion.llm);
            Object.DestroyImmediate(_companion.voice);
            Object.DestroyImmediate(_companion.server);
            Object.DestroyImmediate(_companion);
            _sandbox.Dispose();
        }

        private CompanionSettingsModel Release() => new CompanionSettingsModel(_companion, false, NoHosts);
        private CompanionSettingsModel Development(params string[] hosts) => new CompanionSettingsModel(_companion, true, hosts);

        [Test]
        public void SetEndpoint_StoresTheNormalizedHttpsUrlAsAPlayerPrefsOverride()
        {
            Assert.IsTrue(Release().TrySetEndpoint(SettingId.LlmEndpoint, " llm.example.lan:8443 ", out var message), message);
            Assert.AreEqual("https://llm.example.lan:8443", _sandbox.Store.GetString(LlmConfig.PlayerPrefsEndpointKey));

            var check = _companion.llm.ResolveEndpoint(false, NoHosts);
            Assert.IsTrue(check.IsUsable);
            Assert.AreEqual(ConfigOverrides.SourcePlayerPrefs, check.Source);
        }

        [Test]
        public void SetEndpoint_RejectsCleartextInReleaseBuilds()
        {
            Assert.IsFalse(Release().TrySetEndpoint(SettingId.LlmEndpoint, "http://192.168.1.20:11434", out var message));
            StringAssert.Contains("http://", message);
            Assert.IsNull(_sandbox.Store.GetString(LlmConfig.PlayerPrefsEndpointKey));
        }

        [Test]
        public void SetEndpoint_AcceptsAllowListedLanCleartextInDevelopmentBuilds()
        {
            Assert.IsTrue(Development("192.168.1.20").TrySetEndpoint(SettingId.LlmEndpoint, "http://192.168.1.20:11434", out _));
            Assert.AreEqual("http://192.168.1.20:11434", _sandbox.Store.GetString(LlmConfig.PlayerPrefsEndpointKey));
            Assert.IsFalse(Development().TrySetEndpoint(SettingId.CvServer, "http://192.168.1.30:8000", out _));
        }

        [Test]
        public void SetEndpoint_RejectsInvalidText_AndBlankTextClearsTheOverride()
        {
            var model = Release();
            Assert.IsFalse(model.TrySetEndpoint(SettingId.SpeechServer, "ftp://hub.example.lan", out _));

            Assert.IsTrue(model.TrySetEndpoint(SettingId.SpeechServer, "https://hub.example.lan", out _));
            Assert.AreEqual("https://hub.example.lan", _sandbox.Store.GetString(VoiceConfig.PlayerPrefsBaseUrlKey));
            Assert.IsTrue(model.TrySetEndpoint(SettingId.SpeechServer, "  ", out _));
            Assert.IsNull(_sandbox.Store.GetString(VoiceConfig.PlayerPrefsBaseUrlKey));
        }

        [Test]
        public void CloudOptIn_NeedsASecondConfirmingPress_AndTurnsOffImmediately()
        {
            var model = Release();
            model.Activate(SettingId.CloudOptIn);
            Assert.IsTrue(model.CloudOptInPending);
            Assert.IsFalse(_companion.voice.EffectiveCloudOptIn);

            model.Activate(SettingId.CloudOptIn);
            Assert.IsFalse(model.CloudOptInPending);
            Assert.IsTrue(_companion.voice.EffectiveCloudOptIn);

            model.Activate(SettingId.CloudOptIn);
            Assert.IsFalse(_companion.voice.EffectiveCloudOptIn);
        }

        [Test]
        public void CloudOptIn_PendingConfirmationIsCancelledWhenTheSelectionMoves()
        {
            var model = Release();
            model.Activate(SettingId.CloudOptIn);
            model.CancelPending();
            model.Activate(SettingId.CloudOptIn);
            Assert.IsTrue(model.CloudOptInPending);
            Assert.IsFalse(_companion.voice.EffectiveCloudOptIn);
        }

        [Test]
        public void SpeechProvider_TogglesBetweenSelfHostedAndCloud()
        {
            var model = Release();
            model.Cycle(SettingId.SpeechProvider, 1);
            Assert.AreEqual(SpeechProvider.Cloud, _companion.voice.EffectiveProvider);
            Assert.AreEqual(EndpointStatus.CloudNotOptedIn, _companion.voice.ResolveSttEndpoint(false, NoHosts).Status);

            model.Cycle(SettingId.SpeechProvider, -1);
            Assert.AreEqual(SpeechProvider.SelfHosted, _companion.voice.EffectiveProvider);
        }

        [Test]
        public void LlmModel_CyclesThroughTheChoicesAndWraps()
        {
            var model = Release();
            model.Cycle(SettingId.LlmModel, 1);
            Assert.AreEqual("qwen2.5vl:7b", _companion.llm.model);
            model.Cycle(SettingId.LlmModel, 1);
            Assert.AreEqual("llava:7b", _companion.llm.model);
            model.Cycle(SettingId.LlmModel, -1);
            Assert.AreEqual("qwen2.5vl:7b", _companion.llm.model);
        }

        [Test]
        public void ResetAll_RemovesEveryOverrideIncludingStoredCredentials()
        {
            var model = Release();
            model.TrySetEndpoint(SettingId.LlmEndpoint, "https://llm.example.lan", out _);
            model.TrySetEndpoint(SettingId.SpeechServer, "https://hub.example.lan", out _);
            model.TrySetEndpoint(SettingId.CvServer, "https://cv.example.lan", out _);
            model.Cycle(SettingId.SpeechProvider, 1);
            model.Cycle(SettingId.LlmModel, 1);
            _sandbox.Store.SetString(VoiceConfig.PlayerPrefsApiKey, "placeholder-cloud-key");

            model.Activate(SettingId.ResetAll);
            Assert.AreEqual(0, _sandbox.Store.Count);
        }

        [Test]
        public void Rows_FlagUnconfiguredBackends_AndShowTheReleaseTransportPolicy()
        {
            var rows = Release().BuildRows();
            var llm = rows.Single(r => r.Id == SettingId.LlmEndpoint);
            Assert.AreEqual(CompanionSettingsModel.NotSet, llm.Value);
            Assert.IsTrue(llm.IsWarning);
            Assert.IsTrue(rows.Single(r => r.Id == SettingId.SpeechServer).IsWarning);
            Assert.AreEqual("off", rows.Single(r => r.Id == SettingId.CloudOptIn).Value);
            StringAssert.Contains("https only", rows.Single(r => r.Id == SettingId.DevCleartextHosts).Detail);
        }
    }
}
