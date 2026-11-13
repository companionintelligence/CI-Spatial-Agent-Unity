using System.Collections.Generic;
using NUnit.Framework;
using XRScan.EditorTools;

namespace XRScan.Tests.Config
{
    /// <summary>
    /// Release signing and versionCode rules of the Android builds (<see cref="AndroidReleaseSettings"/>),
    /// with a fake environment: no keystore, alias or password is read from the real one.
    /// </summary>
    public class AndroidReleaseSettingsTests
    {
        private static System.Func<string, string> Env(Dictionary<string, string> values) =>
            name => values.TryGetValue(name, out var value) ? value : null;

        private static Dictionary<string, string> AllSigningVariables() => new Dictionary<string, string>
        {
            { AndroidReleaseSettings.KeystorePathVariable, "release.jks" },
            { AndroidReleaseSettings.KeystorePassVariable, "test-only-store-value" },
            { AndroidReleaseSettings.KeyAliasVariable, "alias" },
            { AndroidReleaseSettings.KeyPassVariable, "test-only-key-value" },
        };

        [TestCase("0.1.0", 1000)]
        [TestCase("1.2.3", 1002003)]
        [TestCase("2.0.0-rc1", 2000000)]
        [TestCase("10.999.999", 10999999)]
        public void VersionCode_ComesFromTheBundleVersion(string bundleVersion, int expected)
        {
            Assert.IsTrue(AndroidReleaseSettings.TryGetVersionCode(bundleVersion, Env(new Dictionary<string, string>()),
                                                                   out var code, out var error), error);
            Assert.AreEqual(expected, code);
        }

        [Test]
        public void VersionCode_VariableWinsOverTheBundleVersion()
        {
            var env = Env(new Dictionary<string, string> { { AndroidReleaseSettings.VersionCodeVariable, " 42 " } });
            Assert.IsTrue(AndroidReleaseSettings.TryGetVersionCode("1.2.3", env, out var code, out _));
            Assert.AreEqual(42, code);
        }

        [TestCase("1.2")]
        [TestCase("1.1000.0")]
        [TestCase("0.0.0")]
        [TestCase("v1.2.3")]
        [TestCase("")]
        public void VersionCode_RejectsBundleVersionsThatDoNotFit(string bundleVersion)
        {
            Assert.IsFalse(AndroidReleaseSettings.TryGetVersionCode(bundleVersion, Env(new Dictionary<string, string>()),
                                                                    out _, out var error));
            StringAssert.Contains(AndroidReleaseSettings.VersionCodeVariable, error);
        }

        [TestCase("0")]
        [TestCase("-5")]
        [TestCase("abc")]
        [TestCase("2100000001")]
        public void VersionCode_RejectsInvalidVariable(string value)
        {
            var env = Env(new Dictionary<string, string> { { AndroidReleaseSettings.VersionCodeVariable, value } });
            Assert.IsFalse(AndroidReleaseSettings.TryGetVersionCode("0.1.0", env, out _, out var error));
            StringAssert.Contains(AndroidReleaseSettings.VersionCodeVariable, error);
        }

        [Test]
        public void Signing_UsesTheReleaseKeystoreOnlyWhenAllFourVariablesAreSet()
        {
            var signing = AndroidReleaseSettings.ReadSigning(Env(AllSigningVariables()), out var missing);
            Assert.IsNotNull(signing);
            CollectionAssert.IsEmpty(missing);
            Assert.AreEqual("release.jks", signing.KeystorePath);
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
        public void ReleaseFlag_IsMatchedExactlyAndCaseInsensitively()
        {
            Assert.IsTrue(AndroidReleaseSettings.HasFlag(new[] { "-batchmode", "-RELEASE" }, AndroidReleaseSettings.ReleaseFlag));
            Assert.IsFalse(AndroidReleaseSettings.HasFlag(new[] { "-releaseNotes", "release" }, AndroidReleaseSettings.ReleaseFlag));
        }
    }
}
