using NUnit.Framework;
using UnityEngine;
using XRScan.Core;

namespace XRScan.Tests.Config
{
    public class BackendConfigTests
    {
        private static readonly string[] NoHosts = new string[0];
        private static readonly string[] LanOllama = { "192.168.1.20" };

        private OverrideSandbox _sandbox;
        private LlmConfig _llm;
        private VoiceConfig _voice;
        private ServerConfig _server;
        private CompanionConfig _companion;

        [SetUp]
        public void SetUp()
        {
            _sandbox = new OverrideSandbox();
            _llm = ScriptableObject.CreateInstance<LlmConfig>();
            _voice = ScriptableObject.CreateInstance<VoiceConfig>();
            _server = ScriptableObject.CreateInstance<ServerConfig>();
            _companion = ScriptableObject.CreateInstance<CompanionConfig>();
            _companion.llm = _llm;
            _companion.voice = _voice;
            _companion.server = _server;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_llm);
            Object.DestroyImmediate(_voice);
            Object.DestroyImmediate(_server);
            Object.DestroyImmediate(_companion);
            _sandbox.Dispose();
        }

        [Test]
        public void Llm_Unconfigured_HasNoEndpointAndNeverFallsBackToAHardcodedHost()
        {
            var check = _llm.ResolveEndpoint(false, NoHosts);
            Assert.AreEqual(EndpointStatus.NotConfigured, check.Status);
            StringAssert.EndsWith(".invalid", _llm.BaseUrl());
            StringAssert.DoesNotContain("127.0.0.1", _llm.BaseUrl());
        }

        [Test]
        public void Llm_LegacyHost_DefaultsToHttpsOnTheConfiguredPort()
        {
            _llm.host = "ollama.example.lan";
            var check = _llm.ResolveEndpoint(false, NoHosts);
            Assert.IsTrue(check.IsUsable);
            Assert.AreEqual("https://ollama.example.lan:11434", check.Url.ToString());
        }

        [Test]
        public void Llm_HttpsEndpoint_WorksInReleaseBuilds()
        {
            _llm.endpoint = "https://llm.example.lan/ollama";
            Assert.AreEqual("https://llm.example.lan/ollama", _llm.ResolveEndpoint(false, NoHosts).Url.ToString());
        }

        [Test]
        public void Llm_LanHttp_OnlyInDevelopmentBuildsForAllowListedHosts()
        {
            _llm.endpoint = "http://192.168.1.20:11434";
            Assert.AreEqual(EndpointStatus.CleartextBlocked, _llm.ResolveEndpoint(false, LanOllama).Status);
            Assert.AreEqual(EndpointStatus.CleartextBlocked, _llm.ResolveEndpoint(true, NoHosts).Status);

            var dev = _llm.ResolveEndpoint(true, LanOllama);
            Assert.IsTrue(dev.IsUsable);
            Assert.AreEqual("http://192.168.1.20:11434", dev.Url.ToString());

            // The URL OllamaBackend sends to, under the active (empty) allowlist: never the blocked http endpoint.
            StringAssert.EndsWith(".invalid", _llm.BaseUrl());
        }

        [Test]
        public void Llm_PlayerPrefsOverride_WinsOverEnvironmentWhichWinsOverTheAsset()
        {
            _llm.endpoint = "https://asset.example.lan";
            _sandbox.Environment[LlmConfig.EnvEndpointKey] = "https://env.example.lan";
            _sandbox.Store.SetString(LlmConfig.PlayerPrefsEndpointKey, "https://prefs.example.lan");

            var check = _llm.ResolveEndpoint(false, NoHosts);
            Assert.AreEqual("https://prefs.example.lan", check.Url.ToString());
            Assert.AreEqual(ConfigOverrides.SourcePlayerPrefs, check.Source);

            _sandbox.Store.DeleteKey(LlmConfig.PlayerPrefsEndpointKey);
            Assert.AreEqual("https://env.example.lan", _llm.ResolveEndpoint(false, NoHosts).Url.ToString());

            _sandbox.Environment.Clear();
            Assert.AreEqual("https://asset.example.lan", _llm.ResolveEndpoint(false, NoHosts).Url.ToString());
        }

        [Test]
        public void Llm_ModelOverride_ReplacesTheAssetDefault()
        {
            Assert.AreEqual("llava:7b", _llm.model);
            _sandbox.Store.SetString(LlmConfig.PlayerPrefsModelKey, "qwen2.5vl:7b");
            Assert.AreEqual("qwen2.5vl:7b", _llm.model);
        }

        [Test]
        public void Voice_DefaultsToSelfHosted_AndNeverToACloudProvider()
        {
            Assert.AreEqual(SpeechProvider.SelfHosted, _voice.provider);
            Assert.IsFalse(_voice.cloudOptIn);
            Assert.AreEqual(EndpointStatus.NotConfigured, _voice.ResolveSttEndpoint(false, NoHosts).Status);
            Assert.AreEqual(EndpointStatus.NotConfigured, _voice.ResolveTtsEndpoint(false, NoHosts).Status);
            StringAssert.DoesNotContain("openai", _voice.sttEndpoint);
            StringAssert.DoesNotContain("openai", _voice.ttsEndpoint);
            Assert.IsFalse(_voice.requiresApiKey);
        }

        [Test]
        public void Voice_SelfHosted_AppendsTheOpenAiCompatiblePaths()
        {
            _voice.selfHostedBaseUrl = "https://hub.example.lan/api";
            Assert.AreEqual("https://hub.example.lan/api/v1/audio/transcriptions",
                _voice.ResolveSttEndpoint(false, NoHosts).Url.ToString());
            Assert.AreEqual("https://hub.example.lan/api/v1/audio/speech",
                _voice.ResolveTtsEndpoint(false, NoHosts).Url.ToString());
        }

        [Test]
        public void Voice_CloudProvider_RequiresTheExplicitOptIn()
        {
            _voice.provider = SpeechProvider.Cloud;
            Assert.AreEqual(EndpointStatus.CloudNotOptedIn, _voice.ResolveSttEndpoint(false, NoHosts).Status);
            Assert.IsFalse(_voice.requiresApiKey);

            _voice.cloudOptIn = true;
            var check = _voice.ResolveSttEndpoint(false, NoHosts);
            Assert.IsTrue(check.IsUsable);
            Assert.AreEqual("api.openai.com", check.Url.Host);
            Assert.IsTrue(_voice.requiresApiKey);
        }

        [Test]
        public void Voice_LanHttp_OnlyInDevelopmentBuildsForAllowListedHosts()
        {
            _voice.selfHostedBaseUrl = "http://192.168.1.20:8000";
            Assert.AreEqual(EndpointStatus.CleartextBlocked, _voice.ResolveSttEndpoint(false, LanOllama).Status);
            Assert.AreEqual(EndpointStatus.CleartextBlocked, _voice.ResolveTtsEndpoint(true, NoHosts).Status);
            Assert.AreEqual("http://192.168.1.20:8000/v1/audio/transcriptions",
                _voice.ResolveSttEndpoint(true, LanOllama).Url.ToString());

            // The URLs CloudWhisperStt / CloudTts send to, under the active (empty) allowlist.
            StringAssert.EndsWith(".invalid", _voice.sttEndpoint);
            StringAssert.EndsWith(".invalid", _voice.ttsEndpoint);
        }

        [Test]
        public void Voice_SelfHostedUrlThatIsACloudProvider_StillNeedsTheOptIn()
        {
            _voice.selfHostedBaseUrl = "https://api.openai.com";
            Assert.AreEqual(EndpointStatus.CloudNotOptedIn, _voice.ResolveSttEndpoint(false, NoHosts).Status);
        }

        [Test]
        public void Voice_ProviderAndOptIn_CanBeOverriddenFromPlayerPrefs()
        {
            _sandbox.Store.SetString(VoiceConfig.PlayerPrefsProviderKey, "cloud");
            _sandbox.Store.SetString(VoiceConfig.PlayerPrefsCloudOptInKey, "true");
            Assert.AreEqual(SpeechProvider.Cloud, _voice.EffectiveProvider);
            Assert.IsTrue(_voice.EffectiveCloudOptIn);
            Assert.IsTrue(_voice.ResolveSttEndpoint(false, NoHosts).IsUsable);
        }

        [Test]
        public void Voice_Credentials_AreOnlySentToTheirOwnProvider()
        {
            _sandbox.Store.SetString(VoiceConfig.PlayerPrefsApiKey, "placeholder-cloud-key");
            _sandbox.Store.SetString(VoiceConfig.PlayerPrefsSelfHostedTokenKey, "placeholder-hub-token");

            Assert.AreEqual("placeholder-hub-token", _voice.ResolveApiKey());
            _voice.provider = SpeechProvider.Cloud;
            Assert.AreEqual("placeholder-cloud-key", _voice.ResolveApiKey());
        }

        [Test]
        public void Server_Unconfigured_IsEmptyInDevelopmentButNeverEmptyInRelease()
        {
            Assert.AreEqual(string.Empty, _server.ResolveBaseUrl(true, NoHosts));
            StringAssert.EndsWith(".invalid", _server.ResolveBaseUrl(false, NoHosts));
        }

        [Test]
        public void Server_BlockedCleartextEndpoint_NeverResolves()
        {
            _server.baseUrl = "http://192.168.1.30:8000";
            StringAssert.EndsWith(".invalid", _server.ResolveBaseUrl(false, new[] { "192.168.1.30" }));
            Assert.AreEqual("http://192.168.1.30:8000", _server.ResolveBaseUrl(true, new[] { "192.168.1.30" }));
        }

        [Test]
        public void Companion_TransportPolicy_UsesTheAllowlistOnlyInDevelopmentBuilds()
        {
            _companion.devCleartextHosts.Add("192.168.1.20");
            _companion.devCleartextHosts.Add("llm.example.org");

            var rejected = _companion.ApplyTransportPolicy(true);
            CollectionAssert.AreEqual(new[] { "192.168.1.20" }, CleartextPolicy.DevAllowlist);
            CollectionAssert.AreEqual(new[] { "llm.example.org" }, rejected);

            _companion.ApplyTransportPolicy(false);
            Assert.AreEqual(0, CleartextPolicy.DevAllowlist.Count);
        }

        [Test]
        public void Companion_NeedsSetup_UntilLlmAndSpeechAreUsable()
        {
            Assert.IsTrue(_companion.NeedsSetup(false, NoHosts));
            _llm.endpoint = "https://llm.example.lan";
            Assert.IsTrue(_companion.NeedsSetup(false, NoHosts));
            _voice.selfHostedBaseUrl = "https://hub.example.lan";
            Assert.IsFalse(_companion.NeedsSetup(false, NoHosts));
        }
    }
}
