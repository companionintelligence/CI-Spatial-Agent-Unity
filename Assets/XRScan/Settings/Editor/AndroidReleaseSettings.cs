using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace XRScan.EditorTools
{
    /// <summary>
    /// Release signing and version of the Android builds made by <c>XRScanBuilder</c>.
    /// <list type="bullet">
    /// <item><b>Signing</b> comes from the environment, so that no keystore, key alias or password is ever stored in
    /// the project. A release keystore is used only when <c>ANDROID_KEYSTORE_NAME</c> (the keystore file),
    /// <c>ANDROID_KEYSTORE_PASS</c>, <c>ANDROID_KEYALIAS_NAME</c> and <c>ANDROID_KEYALIAS_PASS</c> are all set;
    /// otherwise the build is signed with Unity's debug key. With <c>-release</c> on the command line the build
    /// fails instead of falling back to it. These are the variables GameCI's unity-builder sets in its build
    /// container, so local builds and CI read the same names.</item>
    /// <item><b>Version</b> has one source, Player Settings → Version (<c>PlayerSettings.bundleVersion</c>). It is
    /// the versionName, and the versionCode is derived from it: <c>MAJOR * 1000000 + MINOR * 1000 + PATCH</c>.</item>
    /// <item>Unity copies the passwords of a release-signed build into the Gradle project and build records it keeps
    /// in <c>Library</c>. <see cref="ReadPasswordsFromEnvironment"/> and <see cref="DeleteFilesContaining"/> keep
    /// them out of the Gradle caches and remove those copies.</item>
    /// </list>
    /// Plain logic without Unity calls, so EditMode tests cover it.
    /// </summary>
    public static class AndroidReleaseSettings
    {
        /// <summary>
        /// Path of the release keystore file. GameCI calls it a name: in CI it holds the file name the keystore was
        /// decoded to in the project folder, so a relative path is resolved against the project folder.
        /// </summary>
        public const string KeystorePathVariable = "ANDROID_KEYSTORE_NAME";
        public const string KeystorePassVariable = "ANDROID_KEYSTORE_PASS";
        public const string KeyAliasVariable = "ANDROID_KEYALIAS_NAME";
        public const string KeyPassVariable = "ANDROID_KEYALIAS_PASS";

        /// <summary>Command-line flag: fail the build unless it can be signed with the release keystore.</summary>
        public const string ReleaseFlag = "-release";

        /// <summary>The largest versionCode Google Play accepts.</summary>
        public const int MaxVersionCode = 2100000000;

        /// <summary>
        /// Passwords shorter than this are not searched for in <c>Library</c> after a release build: such a short
        /// string could also occur in files that hold no password.
        /// </summary>
        public const int MinSearchedSecretLength = 8;

        /// <summary>The four variables a release-signed build needs.</summary>
        public static readonly string[] SigningVariables =
            { KeystorePathVariable, KeystorePassVariable, KeyAliasVariable, KeyPassVariable };

        /// <summary>Variables earlier builders read, with what replaced each one. The builder ignores them.</summary>
        public static readonly IReadOnlyDictionary<string, string> RetiredVariables = new Dictionary<string, string>
        {
            { "KEYSTORE_PATH", KeystorePathVariable },
            { "KEYSTORE_PASS", KeystorePassVariable },
            { "KEY_ALIAS", KeyAliasVariable },
            { "KEY_PASS", KeyPassVariable },
            { "VERSION_CODE", "Player Settings > Version, from which the versionCode is derived" },
        };

        /// <summary>
        /// One warning for each retired variable that is set (<see cref="RetiredVariables"/>), naming what replaced
        /// it. Never includes the values.
        /// </summary>
        public static string[] RetiredVariableWarnings(Func<string, string> getVariable)
        {
            if (getVariable == null) throw new ArgumentNullException(nameof(getVariable));
            return RetiredVariables
                .Where(retired => !string.IsNullOrEmpty(getVariable(retired.Key)))
                .Select(retired => $"{retired.Key} is set but no longer read; use {retired.Value}.")
                .ToArray();
        }

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
        /// <c>MAJOR * 1000000 + MINOR * 1000 + PATCH</c> for a bundle version <c>MAJOR.MINOR.PATCH</c>, optionally
        /// followed by a pre-release or build suffix (<c>-rc1</c>, <c>+abc</c>); MINOR and PATCH must be below
        /// 1000. <c>0.1.0</c> gives 1000 and <c>1.2.3</c> gives 1002003. Returns false, with the reason in
        /// <paramref name="error"/>, when the bundle version does not give a valid versionCode.
        /// </summary>
        public static bool TryVersionCodeFromBundleVersion(string bundleVersion, out int versionCode, out string error)
        {
            versionCode = 0;
            var match = Regex.Match(bundleVersion ?? string.Empty,
                                    @"^\s*(\d{1,4})\.(\d{1,3})\.(\d{1,3})(?:[-+][0-9A-Za-z.+-]*)?\s*$");
            if (!match.Success)
            {
                error = $"Bundle version '{bundleVersion}' is not MAJOR.MINOR.PATCH with MINOR and PATCH below 1000; " +
                        "fix Player Settings > Version.";
                return false;
            }

            long code = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 1000000L
                      + long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 1000L
                      + long.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            if (code < 1 || code > MaxVersionCode)
            {
                error = $"Bundle version '{bundleVersion}' gives versionCode {code}, outside 1 to {MaxVersionCode}; " +
                        "fix Player Settings > Version.";
                return false;
            }

            versionCode = (int)code;
            error = null;
            return true;
        }

        /// <summary>True when <paramref name="flag"/> is one of <paramref name="args"/> (case-insensitive).</summary>
        public static bool HasFlag(IEnumerable<string> args, string flag) =>
            args != null && args.Any(arg => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));

        private static readonly Regex StorePasswordLine =
            new Regex(@"^([ \t]*storePassword\b)([ \t]*=?[ \t]*)[^\r\n]*", RegexOptions.Multiline);

        private static readonly Regex KeyPasswordLine =
            new Regex(@"^([ \t]*keyPassword\b)([ \t]*=?[ \t]*)[^\r\n]*", RegexOptions.Multiline);

        /// <summary>
        /// Rewrites the signing block Unity generates in the launcher module's <c>build.gradle</c> so that Gradle
        /// reads the passwords from its environment (<see cref="KeystorePassVariable"/>, <see cref="KeyPassVariable"/>)
        /// instead of from the literals Unity writes (<c>storePassword '…'</c>, <c>keyPassword '…'</c>). Gradle
        /// compiles build scripts into classes it caches under <c>GRADLE_USER_HOME</c>, outside the project, so with
        /// the literals every release build left the passwords in that cache. Returns null, for the caller to leave
        /// the file as it is, unless the script has exactly one <c>storePassword</c> and one <c>keyPassword</c> line.
        /// </summary>
        public static string ReadPasswordsFromEnvironment(string launcherGradle)
        {
            if (launcherGradle == null ||
                StorePasswordLine.Matches(launcherGradle).Count != 1 ||
                KeyPasswordLine.Matches(launcherGradle).Count != 1)
                return null;

            var text = StorePasswordLine.Replace(launcherGradle, m => GradleEnvironmentRead(m, KeystorePassVariable));
            return KeyPasswordLine.Replace(text, m => GradleEnvironmentRead(m, KeyPassVariable));
        }

        private static string GradleEnvironmentRead(Match line, string variable)
        {
            var separator = line.Groups[2].Length > 0 ? line.Groups[2].Value : " ";
            return line.Groups[1].Value + separator + "System.getenv('" + variable + "')";
        }

        /// <summary>
        /// Deletes every file under <paramref name="root"/> whose bytes contain one of <paramref name="secrets"/>
        /// (as UTF-8), and returns their paths relative to <paramref name="root"/>. Unity copies the keystore and key
        /// passwords of a release-signed build into its Gradle project and build records under <c>Library/Bee</c>,
        /// and regenerates every one of those files on the next build. Secrets shorter than
        /// <see cref="MinSearchedSecretLength"/> are not searched for. <paramref name="unreadable"/> lists the files
        /// that could not be read (for example because another process holds them open), and
        /// <paramref name="undeleted"/> the files that hold a secret but could not be deleted.
        /// </summary>
        public static List<string> DeleteFilesContaining(string root, IEnumerable<string> secrets,
                                                         out List<string> unreadable, out List<string> undeleted,
                                                         int bufferSize = 1 << 20)
        {
            unreadable = new List<string>();
            undeleted = new List<string>();
            var deleted = new List<string>();
            var needles = (secrets ?? Enumerable.Empty<string>())
                .Where(secret => secret != null && secret.Length >= MinSearchedSecretLength)
                .Distinct(StringComparer.Ordinal)
                .Select(secret => Encoding.UTF8.GetBytes(secret))
                .ToArray();
            if (needles.Length == 0 || string.IsNullOrEmpty(root) || !Directory.Exists(root)) return deleted;

            root = Path.GetFullPath(root);
            var directories = new Stack<string>();
            directories.Push(root);
            while (directories.Count > 0)
            {
                var directory = directories.Pop();
                string[] files, subdirectories;
                try
                {
                    files = Directory.GetFiles(directory);
                    subdirectories = Directory.GetDirectories(directory);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    unreadable.Add(Relative(root, directory));
                    continue;
                }

                foreach (var subdirectory in subdirectories)
                {
                    try
                    {
                        // Never follow a link or junction out of the folder being cleaned.
                        if ((File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) == 0)
                            directories.Push(subdirectory);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                        unreadable.Add(Relative(root, subdirectory));
                    }
                }

                foreach (var file in files)
                {
                    bool holdsSecret;
                    try
                    {
                        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read,
                                                           FileShare.ReadWrite | FileShare.Delete, 4096,
                                                           FileOptions.SequentialScan))
                            holdsSecret = ContainsAny(stream, needles, bufferSize);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                        unreadable.Add(Relative(root, file));
                        continue;
                    }
                    if (!holdsSecret) continue;

                    try
                    {
                        File.Delete(file);
                        deleted.Add(Relative(root, file));
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                        undeleted.Add(Relative(root, file));
                    }
                }
            }
            return deleted;
        }

        /// <summary>
        /// True when the stream's bytes contain one of <paramref name="needles"/>. Reads in chunks of
        /// <paramref name="bufferSize"/> bytes and keeps the tail of each chunk, so a match across two chunks is found.
        /// </summary>
        internal static bool ContainsAny(Stream stream, byte[][] needles, int bufferSize)
        {
            int longest = needles.Max(needle => needle.Length);
            var buffer = new byte[Math.Max(bufferSize, 1) + longest];
            int kept = 0;
            while (true)
            {
                int read = stream.Read(buffer, kept, buffer.Length - kept);
                if (read <= 0) return false;
                int length = kept + read;
                foreach (var needle in needles)
                {
                    if (IndexOf(buffer, length, needle) >= 0) return true;
                }
                kept = Math.Min(longest - 1, length);
                Buffer.BlockCopy(buffer, length - kept, buffer, 0, kept);
            }
        }

        private static int IndexOf(byte[] haystack, int length, byte[] needle)
        {
            int last = length - needle.Length;
            byte first = needle[0];
            for (int i = 0; i <= last; i++)
            {
                if (haystack[i] != first) continue;
                int j = 1;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        private static string Relative(string root, string path) =>
            path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                  .Replace('\\', '/')
                : path;
    }
}
