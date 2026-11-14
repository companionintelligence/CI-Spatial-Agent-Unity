using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XRScan.Core;
using XRScan.EditorTools;

namespace XRScan.Tests.Config
{
    /// <summary>
    /// The checks that decide whether the app may send plain http://. On Android, UnityWebRequest runs on Unity's
    /// native libcurl, which ignores the Android network security config. So the app's traffic is gated only by
    /// Unity's "Allow downloads over HTTP" (<see cref="UnityHttpBuildGuard"/>) and by <see cref="CleartextPolicy"/>,
    /// which every call site must resolve its URL through. The generated config XML is tested separately: it
    /// governs Java-level networking only.
    /// </summary>
    public class TransportGateTests
    {
        /// <summary>
        /// Every UnityWebRequest network call site in the runtime code (relative to Assets/XRScan), with the
        /// CleartextPolicy-gated source of its URL. A new call site fails
        /// <see cref="EveryNetworkCallSite_TakesItsUrlFromACleartextPolicyResolver"/> until its URL comes from
        /// CleartextPolicy (LlmConfig.BaseUrl, ServerConfig.ResolveBaseUrl, VoiceConfig.sttEndpoint / ttsEndpoint,
        /// or CleartextPolicy.Evaluate) and it is listed here.
        /// </summary>
        private static readonly Dictionary<string, string> GatedCallSites = new Dictionary<string, string>
        {
            { "Llm/OllamaBackend.cs", "_baseUrl = config.BaseUrl();" },
            { "Vision/CVServerClient.cs", "_config.ResolveBaseUrl()" },
            { "Export/GltfSyncClient.cs", "_config.ResolveBaseUrl()" },
            { "Voice/CloudTts.cs", "new UnityWebRequest(config.ttsEndpoint" },
            { "Voice/CloudWhisperStt.cs", "UnityWebRequest.Post(config.sttEndpoint" },
            // ReplyReport.CheckedUrl is set only from CleartextPolicy.Evaluate (ReplyReporting.Build).
            { "Settings/CompanionSettingsPanel.cs", "new UnityWebRequest(report.CheckedUrl, \"POST\")" },
        };

        private static readonly Regex RequestFactory = new Regex(
            @"(?:new\s+UnityWebRequest|UnityWebRequest(?:Texture|Multimedia|AssetBundle)?\s*\.\s*" +
            @"(?:Get|Post|Put|Delete|Head|PostWwwForm|GetTexture|GetAudioClip|GetAssetBundle))\s*\(\s*(?<arg>[^,)]*)");

        private static readonly Regex OtherHttpStack = new Regex(
            @"new\s+(?:System\.Net\.Http\.)?(?:HttpClient|HttpClientHandler|WebClient|TcpClient|ClientWebSocket)\s*\(|" +
            @"\b(?:Http)?WebRequest\s*\.\s*Create(?:Http)?\s*\(");

        private static string RuntimeRoot => Path.Combine(Application.dataPath, "XRScan");

        [Test]
        public void EveryNetworkCallSite_TakesItsUrlFromACleartextPolicyResolver()
        {
            var callSites = new SortedSet<string>();
            var otherStacks = new List<string>();

            foreach (var file in Directory.GetFiles(RuntimeRoot, "*.cs", SearchOption.AllDirectories))
            {
                string relative = file.Substring(RuntimeRoot.Length + 1).Replace('\\', '/');
                var segments = relative.Split('/');
                if (segments.Contains("Editor") || segments.Contains("Tests")) continue; // not in the player

                foreach (var raw in File.ReadAllLines(file))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("//") || line.StartsWith("/*") || line.StartsWith("*")) continue;

                    foreach (Match match in RequestFactory.Matches(line))
                    {
                        // Loading a local file (e.g. CloudTts playing back its cached reply) is not network traffic.
                        if (!match.Groups["arg"].Value.StartsWith("\"file://")) callSites.Add(relative);
                    }
                    if (OtherHttpStack.IsMatch(line)) otherStacks.Add(relative + ": " + line);
                }
            }

            CollectionAssert.IsEmpty(otherStacks,
                "Runtime code must send HTTP through UnityWebRequest with a URL resolved by CleartextPolicy; " +
                "other HTTP stacks are not covered by it.");
            CollectionAssert.AreEquivalent(GatedCallSites.Keys, callSites,
                "The UnityWebRequest call sites changed. Resolve each new call site's URL through CleartextPolicy " +
                "(LlmConfig.BaseUrl, ServerConfig.ResolveBaseUrl, VoiceConfig.sttEndpoint / ttsEndpoint or " +
                "CleartextPolicy.Evaluate) and list it in TransportGateTests.GatedCallSites.");

            foreach (var site in GatedCallSites)
            {
                StringAssert.Contains(site.Value, File.ReadAllText(Path.Combine(RuntimeRoot, site.Key)),
                    site.Key + " no longer takes its URL from its CleartextPolicy-gated resolver.");
            }
        }

        [TestCase(InsecureHttpOption.AlwaysAllowed, false, 0, UnityHttpBuildGuard.Verdict.Fail)]
        [TestCase(InsecureHttpOption.DevelopmentOnly, false, 0, UnityHttpBuildGuard.Verdict.Ok)]
        [TestCase(InsecureHttpOption.NotAllowed, false, 0, UnityHttpBuildGuard.Verdict.Ok)]
        [TestCase(InsecureHttpOption.DevelopmentOnly, true, 1, UnityHttpBuildGuard.Verdict.Ok)]
        [TestCase(InsecureHttpOption.DevelopmentOnly, true, 0, UnityHttpBuildGuard.Verdict.Ok)]
        [TestCase(InsecureHttpOption.NotAllowed, true, 0, UnityHttpBuildGuard.Verdict.Ok)]
        [TestCase(InsecureHttpOption.NotAllowed, true, 1, UnityHttpBuildGuard.Verdict.Warn)]
        [TestCase(InsecureHttpOption.AlwaysAllowed, true, 1, UnityHttpBuildGuard.Verdict.Warn)]
        public void UnityHttpBuildGuard_FailsReleaseBuildsThatLetUnitySendHttp(
            InsecureHttpOption option, bool developmentBuild, int devHosts, UnityHttpBuildGuard.Verdict expected)
        {
            Assert.AreEqual(expected, UnityHttpBuildGuard.Evaluate(option, developmentBuild, devHosts, out string message));
            StringAssert.Contains(UnityHttpBuildGuard.Label(option), message);
        }

        [Test]
        public void ProjectPlayerSettings_KeepUnityRefusingHttpInReleaseBuilds()
        {
            var verdict = UnityHttpBuildGuard.Evaluate(PlayerSettings.insecureHttpOption, false, 0, out string message);
            Assert.AreNotEqual(UnityHttpBuildGuard.Verdict.Fail, verdict, message);
        }

        /// <summary>
        /// The generated XML only. Android applies it to Java-level networking (NetworkSecurityPolicy), not to
        /// UnityWebRequest; the tests above cover the gates that do apply to the app's traffic.
        /// </summary>
        [Test]
        public void NetworkSecurityConfigXml_ListsCleartextHostsOnlyInDevelopmentBuilds()
        {
            string release = NetworkSecurityConfigGenerator.BuildXml(new[] { "192.168.1.20" }, false);
            StringAssert.Contains("cleartextTrafficPermitted=\"false\"", release);
            StringAssert.DoesNotContain("cleartextTrafficPermitted=\"true\"", release);
            StringAssert.DoesNotContain("192.168.1.20", release);
            StringAssert.DoesNotContain("src=\"user\"", release);

            string development = NetworkSecurityConfigGenerator.BuildXml(new[] { "192.168.1.20" }, true);
            StringAssert.Contains("<domain includeSubdomains=\"false\">192.168.1.20</domain>", development);
            StringAssert.Contains("cleartextTrafficPermitted=\"true\"", development);
            StringAssert.Contains("<base-config cleartextTrafficPermitted=\"false\">", development);

            string developmentWithoutHosts = NetworkSecurityConfigGenerator.BuildXml(new string[0], true);
            StringAssert.DoesNotContain("cleartextTrafficPermitted=\"true\"", developmentWithoutHosts);
        }
    }
}
