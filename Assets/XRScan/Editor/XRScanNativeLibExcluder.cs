using System.IO;
using System.Linq;
using UnityEditor.Android;
using UnityEngine;

namespace XRScan.EditorTools
{
    /// <summary>
    /// Excludes the non-target vendor's native libraries from the packaged APK. Unity bundles the
    /// <c>.so</c> of every installed XR provider regardless of which loader is active, so a Quest APK
    /// otherwise ships PICO's libs (and a Pico APK ships Meta's) plus MockHMD — ~1&#160;MB of inert
    /// native code. <see cref="UnityEditor.PluginImporter"/> flags don't take effect for libraries that
    /// live inside read-only packages, so instead this injects <c>packaging { jniLibs { excludes … } }</c>
    /// into the launcher module's <c>build.gradle</c>, which drops the matching <c>.so</c> at packaging
    /// time whether they are standalone or merged from an <c>.aar</c>. Keyed off the build define
    /// (<see cref="XRScanBuilder.BuildingForQuest"/>) so it is symmetric — whatever the Quest build
    /// leaves out, the Pico build keeps, and vice-versa.
    /// </summary>
    public class XRScanNativeLibExcluder : IPostGenerateGradleAndroidProject
    {
        // After XRScanManifestPostProcessor (order 1); they edit different files so order is cosmetic.
        public int callbackOrder => 2;

        // NB: libopenxr_loader.so is the vendor-neutral Khronos loader (shipped by the picoxr package)
        // that every build depends on — it is never excluded.
        private static readonly string[] PicoLibs =
        {
            "libPICO_TOBAPI.so", "libpvrcapturelib.so", "libpxrplatformloader.so",
            "libCameraRenderingPlugin.so", "libopenxr_pico.so",
        };
        private static readonly string[] MetaLibs = { "libUnityARFoundationMeta.so" };
        private static readonly string[] AlwaysUnusedLibs = { "libUnityMockHMD.so" };

        private const string Marker = "// XRScan native-lib excludes";

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // `path` is the unityLibrary module; the app module that packages the APK is its sibling.
            var launcherGradle = Path.GetFullPath(Path.Combine(path, "..", "launcher", "build.gradle"));
            if (!File.Exists(launcherGradle))
            {
                Debug.LogWarning($"[XRScanNativeLibExcluder] launcher build.gradle not found at {launcherGradle} — skipped.");
                return;
            }

            bool quest = XRScanBuilder.BuildingForQuest();
            var libs = AlwaysUnusedLibs.Concat(quest ? PicoLibs : MetaLibs).ToArray();

            var text = File.ReadAllText(launcherGradle);
            if (text.Contains(Marker)) return; // idempotent (build.gradle is regenerated each build anyway)

            // Insert `excludes += [...]` inside the existing packaging { jniLibs { } } block.
            const string anchor = "jniLibs {";
            int idx = text.IndexOf(anchor, System.StringComparison.Ordinal);
            if (idx < 0)
            {
                Debug.LogWarning("[XRScanNativeLibExcluder] no `jniLibs {` block in launcher build.gradle — skipped.");
                return;
            }

            string list = string.Join(", ", libs.Select(l => $"'**/{l}'"));
            string injection = $"\n            {Marker}\n            excludes += [{list}]";
            text = text.Insert(idx + anchor.Length, injection);

            File.WriteAllText(launcherGradle, text);
            Debug.Log($"[XRScanNativeLibExcluder] Excluded {libs.Length} native lib(s) from the {(quest ? "Quest" : "Pico")} APK: {string.Join(", ", libs)}");
        }
    }
}
