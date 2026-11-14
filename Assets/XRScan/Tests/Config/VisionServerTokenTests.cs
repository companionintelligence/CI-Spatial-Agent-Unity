using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using XRScan.Core;

namespace XRScan.Tests.Config
{
    /// <summary>
    /// The vision server answers every route only with its bearer token (cv-server/app/auth.py). The token comes
    /// from the runtime configuration (the settings panel, or an environment variable on desktop and CI), never
    /// from a committed asset, and every request to that server carries it.
    /// </summary>
    public class VisionServerTokenTests
    {
        private OverrideSandbox _sandbox;
        private ServerConfig _server;

        [SetUp]
        public void SetUp()
        {
            _sandbox = new OverrideSandbox();
            _server = ScriptableObject.CreateInstance<ServerConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_server);
            _sandbox.Dispose();
        }

        [Test]
        public void Token_ComesFromThePanelOverride_ThenTheEnvironment()
        {
            Assert.AreEqual(string.Empty, _server.ResolveApiToken());
            Assert.IsFalse(_server.HasApiToken);

            _sandbox.Environment[ServerConfig.EnvApiTokenKey] = "from-environment";
            Assert.AreEqual("from-environment", _server.ResolveApiToken());

            _sandbox.Store.SetString(ServerConfig.PlayerPrefsApiTokenKey, "from-panel");
            Assert.AreEqual("from-panel", _server.ResolveApiToken());
        }

        [Test]
        public void Requests_CarryTheTokenAsABearerHeader_OnlyWhenOneIsSet()
        {
            using (var request = new UnityWebRequest("https://vision.example.lan/v1/detect", "POST"))
            {
                _server.ApplyAuthorization(request);
                Assert.IsTrue(string.IsNullOrEmpty(request.GetRequestHeader("Authorization")));
            }

            _sandbox.Store.SetString(ServerConfig.PlayerPrefsApiTokenKey, "abc123");
            using (var request = new UnityWebRequest("https://vision.example.lan/v1/detect", "POST"))
            {
                _server.ApplyAuthorization(request);
                Assert.AreEqual("Bearer abc123", request.GetRequestHeader("Authorization"));
            }
        }

        [Test]
        public void TheConfigAsset_HasNoPlaceForAToken()
        {
            var serialized = typeof(ServerConfig)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.IsPublic ? !f.IsNotSerialized : f.GetCustomAttribute<SerializeField>() != null)
                .Select(f => f.Name.ToLowerInvariant());
            CollectionAssert.IsEmpty(serialized.Where(n => n.Contains("token") || n.Contains("key") || n.Contains("secret")));

            string asset = File.ReadAllText("Assets/XRScan/Config/ServerConfig.asset").ToLowerInvariant();
            StringAssert.DoesNotContain("token", asset);
        }

        [TestCase("Vision/CVServerClient.cs", "_config?.ApplyAuthorization(request);")]
        [TestCase("Export/GltfSyncClient.cs", "_config.ApplyAuthorization(req);")]
        public void EveryClientOfTheVisionServer_SendsTheToken(string file, string call)
        {
            StringAssert.Contains(call, File.ReadAllText(Path.Combine(Application.dataPath, "XRScan", file)));
        }

        [Test]
        public void ARefusedRequest_PointsTheUserAtTheToken()
        {
            StringAssert.Contains("token in Settings", ServerConfig.DescribeFailure(401, "Unauthorized"));
            StringAssert.Contains("Could not reach", ServerConfig.DescribeFailure(0, "Cannot resolve destination host"));
        }
    }
}
