using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace XRScan.EditorTools
{
    /// <summary>
    /// For a build that <see cref="XRScanBuilder"/> signs with the release keystore from the environment, makes
    /// the generated launcher <c>build.gradle</c> read the keystore and key passwords from Gradle's environment
    /// (<c>ANDROID_KEYSTORE_PASS</c>, <c>ANDROID_KEYALIAS_PASS</c>, which Gradle inherits from the editor) instead
    /// of the literals Unity writes there. Gradle compiles build scripts into classes it caches under
    /// <c>GRADLE_USER_HOME</c>, outside the project and shared by every build on the machine, so with the literals
    /// each release build left both passwords in that cache. Unity's other copies, in its records under
    /// <c>Library/Bee</c>, are deleted by <see cref="XRScanBuilder"/> when the build ends. Every other build is
    /// left as Unity generates it.
    /// </summary>
    public class XRScanGradleSigning : IPostGenerateGradleAndroidProject
    {
        // After XRScanNativeLibExcluder (2), which also edits the launcher build.gradle.
        public int callbackOrder => 3;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (!XRScanBuilder.SigningFromEnvironment) return;

            // `path` is the unityLibrary module; the app module that signs the APK is its sibling.
            var launcherGradle = Path.GetFullPath(Path.Combine(path, "..", "launcher", "build.gradle"));
            if (!File.Exists(launcherGradle))
            {
                Debug.LogWarning($"[XRScanGradleSigning] {launcherGradle} not found; the signing passwords stay in the Gradle " +
                                 "script as Unity wrote them.");
                return;
            }

            var rewritten = AndroidReleaseSettings.ReadPasswordsFromEnvironment(File.ReadAllText(launcherGradle));
            if (rewritten == null)
            {
                Debug.LogWarning("[XRScanGradleSigning] The launcher build.gradle does not have exactly one storePassword and one " +
                                 "keyPassword line, so the signing passwords stay in it as Unity wrote them, and Gradle's " +
                                 "script cache under GRADLE_USER_HOME will hold them too.");
                return;
            }
            File.WriteAllText(launcherGradle, rewritten);
            Debug.Log($"[XRScanGradleSigning] The launcher build.gradle reads the signing passwords from " +
                      $"{AndroidReleaseSettings.KeystorePassVariable} and {AndroidReleaseSettings.KeyPassVariable}.");
        }
    }
}
