using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace XRScan.EditorTools
{
    /// <summary>
    /// Release signing and version code for the Android builds made by <c>XRScanBuilder</c>, read from the
    /// environment so that no keystore, key alias or password is ever stored in the project.
    /// <list type="bullet">
    /// <item>A release keystore is used only when <c>KEYSTORE_PATH</c>, <c>KEYSTORE_PASS</c>,
    /// <c>KEY_ALIAS</c> and <c>KEY_PASS</c> are all set; otherwise the build is signed with Unity's debug
    /// key. With <c>-release</c> on the command line the build fails instead of falling back to it.</item>
    /// <item>The versionCode is <c>VERSION_CODE</c> when set, otherwise
    /// <c>MAJOR * 1000000 + MINOR * 1000 + PATCH</c> of the bundle version (Player Settings → Version).</item>
    /// </list>
    /// Plain logic without Unity calls, so EditMode tests cover it.
    /// </summary>
    public static class AndroidReleaseSettings
    {
        public const string KeystorePathVariable = "KEYSTORE_PATH";
        public const string KeystorePassVariable = "KEYSTORE_PASS";
        public const string KeyAliasVariable = "KEY_ALIAS";
        public const string KeyPassVariable = "KEY_PASS";
        public const string VersionCodeVariable = "VERSION_CODE";

        /// <summary>Command-line flag: fail the build unless it can be signed with the release keystore.</summary>
        public const string ReleaseFlag = "-release";

        /// <summary>The largest versionCode Google Play accepts.</summary>
        public const int MaxVersionCode = 2100000000;

        /// <summary>The four variables a release-signed build needs.</summary>
        public static readonly string[] SigningVariables =
            { KeystorePathVariable, KeystorePassVariable, KeyAliasVariable, KeyPassVariable };

        /// <summary>Release keystore settings from the environment. Never log the values.</summary>
        public sealed class Signing
        {
            public Signing(string keystorePath, string keystorePass, string keyAlias, string keyPass)
            {
                KeystorePath = keystorePath;
                KeystorePass = keystorePass;
                KeyAlias = keyAlias;
                KeyPass = keyPass;
            }

            public string KeystorePath { get; }
            public string KeystorePass { get; }
            public string KeyAlias { get; }
            public string KeyPass { get; }

            /// <summary>Names the source, never the values.</summary>
            public override string ToString() => "release keystore from " + KeystorePathVariable;
        }

        /// <summary>
        /// The release signing settings when all four variables are set (non-empty); otherwise null, with the
        /// names of the unset ones in <paramref name="missing"/>.
        /// </summary>
        public static Signing ReadSigning(Func<string, string> getVariable, out string[] missing)
        {
            if (getVariable == null) throw new ArgumentNullException(nameof(getVariable));
            missing = SigningVariables.Where(name => string.IsNullOrEmpty(getVariable(name))).ToArray();
            if (missing.Length > 0) return null;
            return new Signing(getVariable(KeystorePathVariable), getVariable(KeystorePassVariable),
                               getVariable(KeyAliasVariable), getVariable(KeyPassVariable));
        }

        /// <summary>
        /// The versionCode for a build: <c>VERSION_CODE</c> when it is set, otherwise derived from
        /// <paramref name="bundleVersion"/> (<see cref="TryVersionCodeFromBundleVersion"/>). Returns false,
        /// with the reason in <paramref name="error"/>, when neither gives a valid code.
        /// </summary>
        public static bool TryGetVersionCode(string bundleVersion, Func<string, string> getVariable,
                                             out int versionCode, out string error)
        {
            var fromEnvironment = getVariable?.Invoke(VersionCodeVariable);
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                if (int.TryParse(fromEnvironment.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out versionCode)
                    && versionCode >= 1 && versionCode <= MaxVersionCode)
                {
                    error = null;
                    return true;
                }
                versionCode = 0;
                error = $"{VersionCodeVariable} must be a whole number from 1 to {MaxVersionCode}.";
                return false;
            }
            return TryVersionCodeFromBundleVersion(bundleVersion, out versionCode, out error);
        }

        /// <summary>
        /// <c>MAJOR * 1000000 + MINOR * 1000 + PATCH</c> for a bundle version <c>MAJOR.MINOR.PATCH</c>, optionally
        /// followed by a pre-release or build suffix (<c>-rc1</c>, <c>+abc</c>); MINOR and PATCH must be below
        /// 1000. <c>0.1.0</c> gives 1000 and <c>1.2.3</c> gives 1002003.
        /// </summary>
        public static bool TryVersionCodeFromBundleVersion(string bundleVersion, out int versionCode, out string error)
        {
            versionCode = 0;
            var match = Regex.Match(bundleVersion ?? string.Empty,
                                    @"^\s*(\d{1,4})\.(\d{1,3})\.(\d{1,3})(?:[-+][0-9A-Za-z.+-]*)?\s*$");
            if (!match.Success)
            {
                error = $"Bundle version '{bundleVersion}' is not MAJOR.MINOR.PATCH with MINOR and PATCH below 1000; " +
                        $"fix Player Settings > Version or set {VersionCodeVariable}.";
                return false;
            }

            long code = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 1000000L
                      + long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 1000L
                      + long.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            if (code < 1 || code > MaxVersionCode)
            {
                error = $"Bundle version '{bundleVersion}' gives versionCode {code}, outside 1 to {MaxVersionCode}; " +
                        $"set {VersionCodeVariable}.";
                return false;
            }

            versionCode = (int)code;
            error = null;
            return true;
        }

        /// <summary>True when <paramref name="flag"/> is one of <paramref name="args"/> (case-insensitive).</summary>
        public static bool HasFlag(IEnumerable<string> args, string flag) =>
            args != null && args.Any(arg => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));
    }
}
