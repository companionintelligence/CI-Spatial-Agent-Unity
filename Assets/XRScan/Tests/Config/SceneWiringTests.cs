using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRScan.Core;
using XRScan.EditorTools;
using XRScan.Settings;

namespace XRScan.Tests.Config
{
    /// <summary>Guards the shipping wiring: XRScan_Main runs real backends from the config assets; mocks only in XRScan_Dev.</summary>
    public class SceneWiringTests
    {
        private const string MainScene = "Assets/XRScan/App/Scenes/XRScan_Main.unity";
        private const string DevScene = "Assets/XRScan/App/Scenes/XRScan_Dev.unity";
        private const string ConfigFolder = "Assets/XRScan/Config/";

        private Scene _scene;

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded) EditorSceneManager.CloseScene(_scene, true);
        }

        private static MonoBehaviour FindBehaviour(Scene scene, string typeName) =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .FirstOrDefault(b => b != null && b.GetType().Name == typeName);

        private static Object Field(Object target, string field)
        {
            using (var serialized = new SerializedObject(target))
            {
                var property = serialized.FindProperty(field);
                Assert.IsNotNull(property, target.GetType().Name + "." + field + " not found");
                return property.objectReferenceValue;
            }
        }

        [Test]
        public void MainScene_RunsRealBackendsFromTheConfigAssets()
        {
            _scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Additive);
            CollectionAssert.IsEmpty(ReleaseMockGuard.FindMockWiring(_scene));

            var bootstrap = FindBehaviour(_scene, "AppBootstrap");
            Assert.IsNotNull(bootstrap);
            Assert.IsInstanceOf<ServerConfig>(Field(bootstrap, "serverConfig"));
            Assert.AreEqual("PermissionGatedSpeechToText", Field(bootstrap, "sttBehaviour").GetType().Name);
            Assert.AreEqual("OllamaBackend", Field(bootstrap, "llmBehaviour").GetType().Name);
            Assert.IsInstanceOf<IReplyChecker>(Field(bootstrap, "llmBehaviour"),
                "the LLM backend brings the reply check (Granite Guardian on the same server); without one every reply is withheld");
            Assert.AreEqual("CloudTts", Field(bootstrap, "ttsBehaviour").GetType().Name);

            var gate = (PermissionGatedSpeechToText)Field(bootstrap, "sttBehaviour");
            Assert.AreEqual("CloudWhisperStt", Field(gate, "inner").GetType().Name);
            Assert.IsNotNull(Field(gate, "permissions"));
            Assert.IsInstanceOf<VoiceConfig>(Field(gate, "voiceConfig"));
            Assert.IsInstanceOf<LlmConfig>(Field(Field(bootstrap, "llmBehaviour"), "config"));
            Assert.IsInstanceOf<VoiceConfig>(Field(Field(bootstrap, "ttsBehaviour"), "config"));

            var configBootstrap = (CompanionConfigBootstrap)FindBehaviour(_scene, nameof(CompanionConfigBootstrap));
            Assert.IsNotNull(configBootstrap);
            var companion = configBootstrap.Config;
            Assert.IsNotNull(companion);
            Assert.IsNotNull(companion.llm);
            Assert.IsNotNull(companion.voice);
            Assert.IsNotNull(companion.server);

            var panel = FindBehaviour(_scene, nameof(CompanionSettingsPanel));
            Assert.IsNotNull(panel);
            Assert.AreSame(companion, Field(panel, "config"));
            Assert.IsNotNull(FindBehaviour(_scene, nameof(RuntimePermissions)));

            var scan = FindBehaviour(_scene, "ScanController");
            Assert.IsNotNull(scan);
            using (var serialized = new SerializedObject(scan))
            {
                string prompt = serialized.FindProperty("detectionPrompt").stringValue.Trim();
                Assert.AreNotEqual("object", prompt, "a one-word prompt labels every box 'object'");
                Assert.GreaterOrEqual(prompt.Split('.').Length, 10, "the vision server is asked for named objects");
            }
        }

        [Test]
        public void DevScene_KeepsTheMockBackendsForDevelopment()
        {
            _scene = EditorSceneManager.OpenScene(DevScene, OpenSceneMode.Additive);
            var problems = ReleaseMockGuard.FindMockWiring(_scene);
            Assert.IsTrue(problems.Any(p => p.Contains("useMocks is on")));
            Assert.IsTrue(problems.Any(p => p.Contains("MockStt")));
        }

        [Test]
        public void DevScene_IsNotPartOfTheBuild()
        {
            Assert.IsFalse(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == DevScene));
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == MainScene));
        }

        [Test]
        public void ShippedConfigAssets_ArePrivateByDefault()
        {
            var voice = AssetDatabase.LoadAssetAtPath<VoiceConfig>(ConfigFolder + "VoiceConfig.asset");
            var llm = AssetDatabase.LoadAssetAtPath<LlmConfig>(ConfigFolder + "LlmConfig.asset");
            var companion = AssetDatabase.LoadAssetAtPath<CompanionConfig>(ConfigFolder + "CompanionConfig.asset");
            Assert.IsNotNull(voice);
            Assert.IsNotNull(llm);
            Assert.IsNotNull(companion);

            Assert.AreEqual(SpeechProvider.SelfHosted, voice.provider);
            Assert.IsFalse(voice.cloudOptIn);
            Assert.IsFalse(CloudProviderHosts.IsKnownCloudHost(EndpointUrl.TryParse(voice.selfHostedBaseUrl, out var url, out _) ? url.Host : string.Empty));
            Assert.IsTrue(string.IsNullOrEmpty(llm.endpoint) || llm.endpoint.StartsWith("https://"));
            Assert.IsTrue(llm.useHttps);
            CollectionAssert.IsEmpty(companion.devCleartextHosts, "No developer hosts may be committed to the shipping config.");
        }
    }
}
