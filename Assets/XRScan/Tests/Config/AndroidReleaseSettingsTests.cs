using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using XRScan.EditorTools;

namespace XRScan.Tests.Config
{
    /// <summary>
    /// Release signing and version rules of the Android builds (<see cref="AndroidReleaseSettings"/>), with a fake
    /// environment: no keystore, alias or password is read from the real one.
    /// </summary>
    public class AndroidReleaseSettingsTests
    {
        private const string StoreValue = "test-only-store-value";
        private const string KeyValue = "test-only-key-value";

        private static System.Func<string, string> Env(Dictionary<string, string> values) =>
            name => values.TryGetValue(name, out var value) ? value : null;

        private static Dictionary<string, string> AllSigningVariables() => new Dictionary<string, string>
        {
            { AndroidReleaseSettings.KeystorePathVariable, "release.p12" },
            { AndroidReleaseSettings.KeystorePassVariable, StoreValue },
            { AndroidReleaseSettings.KeyAliasVariable, "alias" },
            { AndroidReleaseSettings.KeyPassVariable, KeyValue },
        };

        [Test]
        public void SigningVariables_AreTheNamesGameCIPassesToTheBuild()
        {
            // Literal names on purpose: CI's secrets reach the editor under exactly these names (GameCI's
            // unity-builder sets them in its build container), and docs/BUILD.md tells people to set them locally.
            CollectionAssert.AreEqual(
                new[] { "ANDROID_KEYSTORE_NAME", "ANDROID_KEYSTORE_PASS", "ANDROID_KEYALIAS_NAME", "ANDROID_KEYALIAS_PASS" },
                AndroidReleaseSettings.SigningVariables);
        }

        [TestCase("0.1.0", 1000)]
        [TestCase("1.2.3", 1002003)]
        [TestCase("2.0.0-rc1", 2000000)]
        [TestCase("10.999.999", 10999999)]
        public void VersionCode_ComesFromTheBundleVersion(string bundleVersion, int expected)
        {
            Assert.IsTrue(AndroidReleaseSettings.TryVersionCodeFromBundleVersion(bundleVersion, out var code, out var error), error);
            Assert.AreEqual(expected, code);
        }

        [TestCase("1.2")]
        [TestCase("1.1000.0")]
        [TestCase("0.0.0")]
        [TestCase("v1.2.3")]
        [TestCase("")]
        public void VersionCode_RejectsBundleVersionsThatDoNotFit(string bundleVersion)
        {
            Assert.IsFalse(AndroidReleaseSettings.TryVersionCodeFromBundleVersion(bundleVersion, out _, out var error));
            StringAssert.Contains("Player Settings > Version", error);
        }

        [Test]
        public void ProjectVersion_GivesAValidVersionCode()
        {
            // Player Settings > Version is the one place the version is set; a release build fails without this.
            Assert.IsTrue(AndroidReleaseSettings.TryVersionCodeFromBundleVersion(PlayerSettings.bundleVersion, out var code,
                                                                                 out var error), error);
            Assert.Greater(code, 0);
        }

        [Test]
        public void Signing_UsesTheReleaseKeystoreOnlyWhenAllFourVariablesAreSet()
        {
            var signing = AndroidReleaseSettings.ReadSigning(Env(AllSigningVariables()), out var missing);
            Assert.IsNotNull(signing);
            CollectionAssert.IsEmpty(missing);
            Assert.AreEqual("release.p12", signing.KeystorePath);
            Assert.AreEqual("alias", signing.KeyAlias);
            StringAssert.DoesNotContain("test-only", signing.ToString(), "ToString must not reveal the passwords");

            foreach (var name in AndroidReleaseSettings.SigningVariables)
            {
                var partial = AllSigningVariables();
                partial[name] = "";
                Assert.IsNull(AndroidReleaseSettings.ReadSigning(Env(partial), out missing), name + " empty");
                CollectionAssert.AreEqual(new[] { name }, missing);
            }
        }

        [Test]
        public void Signing_ReportsEveryMissingVariable()
        {
            Assert.IsNull(AndroidReleaseSettings.ReadSigning(Env(new Dictionary<string, string>()), out var missing));
            CollectionAssert.AreEqual(AndroidReleaseSettings.SigningVariables, missing);
        }

        [Test]
        public void RetiredVariables_AreNamedWithTheirReplacementButNeverRead()
        {
            var env = Env(new Dictionary<string, string>
            {
                { "KEYSTORE_PATH", "old.jks" }, { "KEY_PASS", StoreValue }, { "VERSION_CODE", "42" },
            });

            var warnings = AndroidReleaseSettings.RetiredVariableWarnings(env);

            CollectionAssert.AreEquivalent(new[]
            {
                "KEYSTORE_PATH is set but no longer read; use ANDROID_KEYSTORE_NAME.",
                "KEY_PASS is set but no longer read; use ANDROID_KEYALIAS_PASS.",
                "VERSION_CODE is set but no longer read; use Player Settings > Version, from which the versionCode is derived.",
            }, warnings);
            Assert.IsNull(AndroidReleaseSettings.ReadSigning(env, out _), "the old names must not sign a build");
            CollectionAssert.IsEmpty(AndroidReleaseSettings.RetiredVariableWarnings(Env(AllSigningVariables())));
        }

        [Test]
        public void ReleaseFlag_IsMatchedExactlyAndCaseInsensitively()
        {
            Assert.IsTrue(AndroidReleaseSettings.HasFlag(new[] { "-batchmode", "-RELEASE" }, AndroidReleaseSettings.ReleaseFlag));
            Assert.IsFalse(AndroidReleaseSettings.HasFlag(new[] { "-releaseNotes", "release" }, AndroidReleaseSettings.ReleaseFlag));
        }

        [TestCase("\n")]
        [TestCase("\r\n")]
        public void GradleScript_ReadsThePasswordsFromTheEnvironment(string newline)
        {
            var script = string.Join(newline,
                "android {",
                "    signingConfigs {",
                "        release {",
                "            storeFile file('D:/keys/release.p12')",
                $"            storePassword '{StoreValue}'",
                "            keyAlias 'alias'",
                $"            keyPassword = '{KeyValue}'",
                "        }",
                "    }",
                "}",
                "");

            var rewritten = AndroidReleaseSettings.ReadPasswordsFromEnvironment(script);

            Assert.IsNotNull(rewritten);
            StringAssert.DoesNotContain("test-only", rewritten);
            StringAssert.Contains("            storePassword System.getenv('ANDROID_KEYSTORE_PASS')" + newline, rewritten);
            StringAssert.Contains("            keyPassword = System.getenv('ANDROID_KEYALIAS_PASS')" + newline, rewritten);
            StringAssert.Contains("storeFile file('D:/keys/release.p12')" + newline + "            storePassword", rewritten);
            StringAssert.Contains("keyAlias 'alias'", rewritten);
        }

        [Test]
        public void GradleScript_IsLeftAloneUnlessItHasOneLineOfEach()
        {
            Assert.IsNull(AndroidReleaseSettings.ReadPasswordsFromEnvironment("android {\n}\n"));
            Assert.IsNull(AndroidReleaseSettings.ReadPasswordsFromEnvironment("storePassword 'a'\nstorePassword 'b'\nkeyPassword 'c'\n"));
            Assert.IsNull(AndroidReleaseSettings.ReadPasswordsFromEnvironment(null));
        }

        [Test]
        public void LibraryCleanup_DeletesOnlyTheFilesThatHoldASecret()
        {
            var root = Path.Combine(Path.GetTempPath(), "xrscan-signing-test-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Android", "launcher"));
                File.WriteAllText(Path.Combine(root, "Android", "launcher", "build.gradle"), $"storePassword '{StoreValue}'\n");
                File.WriteAllText(Path.Combine(root, "Player.dag.json"), $"{{\"keyaliasPass\":\"{KeyValue}\"}}");
                File.WriteAllText(Path.Combine(root, "tundra.log.json"), "nothing secret here");
                // A secret that straddles two reads of the scan (32-byte chunks plus the longest secret's length).
                File.WriteAllBytes(Path.Combine(root, "Player.dag.payloads"),
                                   Enumerable.Repeat((byte)'x', 40).Concat(Encoding.UTF8.GetBytes(StoreValue)).ToArray());

                var deleted = AndroidReleaseSettings.DeleteFilesContaining(root, new[] { StoreValue, KeyValue, null, "short" },
                                                                           out var unreadable, out var undeleted,
                                                                           bufferSize: 32);

                CollectionAssert.AreEquivalent(new[] { "Android/launcher/build.gradle", "Player.dag.json", "Player.dag.payloads" },
                                               deleted);
                CollectionAssert.IsEmpty(unreadable);
                CollectionAssert.IsEmpty(undeleted);
                Assert.IsTrue(File.Exists(Path.Combine(root, "tundra.log.json")));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void LibraryCleanup_IgnoresSecretsTooShortToSearchFor()
        {
            var root = Path.Combine(Path.GetTempPath(), "xrscan-signing-test-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "settings.json"), "\"android\": true");

                var deleted = AndroidReleaseSettings.DeleteFilesContaining(root, new[] { "android" }, out _, out _);

                CollectionAssert.IsEmpty(deleted);
                Assert.IsTrue(File.Exists(Path.Combine(root, "settings.json")));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
