using NUnit.Framework;
using XRScan.Core;

namespace XRScan.Tests.Config
{
    public class EndpointPolicyTests
    {
        private static readonly string[] NoHosts = new string[0];

        [Test]
        public void TryParse_BareHostDefaultsToHttps()
        {
            Assert.IsTrue(EndpointUrl.TryParse("Ollama.Example.LAN:11434", out var url, out _));
            Assert.AreEqual("https", url.Scheme);
            Assert.AreEqual("ollama.example.lan", url.Host);
            Assert.AreEqual(11434, url.Port);
            Assert.AreEqual("https://ollama.example.lan:11434", url.ToString());
        }

        [TestCase("ftp://files.example.lan")]
        [TestCase("https://user:pw@llm.example.lan")]
        [TestCase("https://llm.example.lan/?key=1")]
        [TestCase("   ")]
        public void TryParse_RejectsUnsafeOrUnusableInput(string raw)
        {
            Assert.IsFalse(EndpointUrl.TryParse(raw, out _, out var error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void TryParse_KeepsBasePathAndBracketsIpv6()
        {
            Assert.IsTrue(EndpointUrl.TryParse("http://[fd00::5]:8080/api/", out var url, out _));
            Assert.AreEqual("fd00::5", url.Host);
            Assert.AreEqual("/api", url.Path);
            Assert.AreEqual("http://[fd00::5]:8080/api", url.ToString());
            Assert.AreEqual("http://[fd00::5]:8080/api/v1/audio/speech", url.Combine("v1/audio/speech"));
        }

        [Test]
        public void Https_IsAllowedInEveryBuild()
        {
            var check = CleartextPolicy.Evaluate("https://llm.example.org", false, NoHosts, "test");
            Assert.IsTrue(check.IsUsable);
            Assert.AreEqual("https://llm.example.org", check.Url.ToString());
        }

        [Test]
        public void Http_IsBlockedInReleaseBuilds_EvenForAnAllowListedLanHost()
        {
            var check = CleartextPolicy.Evaluate("http://192.168.1.20:11434", false, new[] { "192.168.1.20" }, "test");
            Assert.AreEqual(EndpointStatus.CleartextBlocked, check.Status);
            Assert.IsFalse(check.IsUsable);
            StringAssert.Contains(".invalid", check.UrlOrUnavailable("llm"));
        }

        [Test]
        public void Http_InDevelopmentBuilds_NeedsTheExplicitAllowlist()
        {
            Assert.AreEqual(EndpointStatus.CleartextBlocked,
                CleartextPolicy.Evaluate("http://192.168.1.20:11434", true, NoHosts, "test").Status);

            var allowed = CleartextPolicy.Evaluate("http://192.168.1.20:11434", true, new[] { "192.168.1.20" }, "test");
            Assert.IsTrue(allowed.IsUsable);
            Assert.AreEqual("http://192.168.1.20:11434", allowed.Url.ToString());
        }

        [Test]
        public void Http_ToAPublicHost_IsBlockedEvenWhenListed()
        {
            var check = CleartextPolicy.Evaluate("http://llm.example.org", true, new[] { "llm.example.org" }, "test");
            Assert.AreEqual(EndpointStatus.CleartextBlocked, check.Status);
        }

        [TestCase("localhost", true)]
        [TestCase("127.0.0.1", true)]
        [TestCase("10.1.2.3", true)]
        [TestCase("172.16.0.1", true)]
        [TestCase("172.32.0.1", false)]
        [TestCase("192.168.0.9", true)]
        [TestCase("169.254.1.1", true)]
        [TestCase("100.64.0.1", false)]
        [TestCase("8.8.8.8", false)]
        [TestCase("fd12::1", true)]
        [TestCase("2001:4860::1", false)]
        [TestCase("hub.local", true)]
        [TestCase("nas", true)]
        [TestCase("example.com", false)]
        [TestCase("1.2.3", false)]
        public void IsLanHost(string host, bool expected)
        {
            Assert.AreEqual(expected, CleartextPolicy.IsLanHost(host));
        }

        [Test]
        public void Sanitize_KeepsNormalizedLanHostsAndRejectsPublicOnes()
        {
            var kept = CleartextPolicy.Sanitize(
                new[] { "http://192.168.1.20:11434", "HUB.LOCAL", "example.com", "192.168.1.20", " " }, out var rejected);
            CollectionAssert.AreEqual(new[] { "192.168.1.20", "hub.local" }, kept);
            CollectionAssert.AreEqual(new[] { "example.com" }, rejected);
        }

        [TestCase("api.openai.com", true)]
        [TestCase("speech.googleapis.com", true)]
        [TestCase("openai.com.example.lan", false)]
        [TestCase("hub.local", false)]
        public void CloudProviderHosts_AreRecognized(string host, bool expected)
        {
            Assert.AreEqual(expected, CloudProviderHosts.IsKnownCloudHost(host));
        }
    }
}
