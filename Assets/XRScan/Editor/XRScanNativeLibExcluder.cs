using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace XRScan.EditorTools
{
    /// <summary>
    /// Excludes the non-target vendor's native libraries — and, outside Pico builds, the PICO SDK's
    /// Java plugins — from the packaged APK. Unity bundles the <c>.so</c>, <c>.aar</c> and <c>.jar</c>
    /// plugins of every installed XR provider regardless of which loader is active, so a Quest APK
    /// otherwise ships PICO's libraries and Java code (and a Pico APK ships Meta's) plus MockHMD.
    /// <see cref="UnityEditor.PluginImporter"/> flags don't take effect for plugins that live inside
    /// read-only packages, so instead this:
    /// <list type="bullet">
    /// <item>injects <c>packaging { jniLibs { excludes … } }</c> into the launcher module's
    /// <c>build.gradle</c>, which drops the matching <c>.so</c> at packaging time whether they are
    /// standalone or merged from an <c>.aar</c>;</item>
    /// <item>outside Pico builds, deletes the PICO SDK's <c>.aar</c>/<c>.jar</c> plugins from
    /// <c>unityLibrary/libs</c> and their dependency lines from <c>unityLibrary/build.gradle</c>, so
    /// none of PICO's Java code, resources or manifest entries are merged. The PICO SDK licence
    /// forbids reproducing its material without PICO's written permission.</item>
    /// </list>
    /// Keyed off the build define (<see cref="XRScanBuilder.BuildingForPico"/>): only an explicit
    /// Pico build keeps PICO content; every other Android build is treated as Quest. The managed PICO
    /// assemblies are dropped separately by <see cref="XRScanPlayerAssemblyFilter"/>.
    /// </summary>
    public class XRScanNativeLibExcluder : IPostGenerateGradleAndroidProject
    {
        // After XRScanManifestPostProcessor (order 1); they edit different files so order is cosmetic.
        public int callbackOrder => 2;

        private const string PicoPackagePath = "Packages/com.unity.xr.openxr.picoxr/";

        // NB: libopenxr_loader.so is the vendor-neutral Khronos loader that every OpenXR build loads —
        // it is never excluded here.
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
            bool pico = XRScanBuilder.BuildingForPico();
            string vendor = pico ? "Pico" : "Quest";

            if (!pico) RemovePicoJavaPlugins(path);

            // `path` is the unityLibrary module; the app module that packages the APK is its sibling.
            var launcherGradle = Path.GetFullPath(Path.Combine(path, "..", "launcher", "build.gradle"));
            if (!File.Exists(launcherGradle))
            {
                Debug.LogWarning($"[XRScanNativeLibExcluder] launcher build.gradle not found at {launcherGradle} — skipped.");
                return;
            }

            var libs = AlwaysUnusedLibs.Concat(pico ? MetaLibs : PicoLibs).ToArray();

            var text = File.ReadAllText(launcherGradle);
            if (text.Contains(Marker)) return; // idempotent (build.gradle is regenerated each build anyway)

            // Insert `excludes += [...]` inside the existing packaging { jniLibs { } } block.
            const string anchor = "jniLibs {";
            int idx = text.IndexOf(anchor, StringComparison.Ordinal);
            if (idx < 0)
            {
                Debug.LogWarning("[XRScanNativeLibExcluder] no `jniLibs {` block in launcher build.gradle — skipped.");
                return;
            }

            string list = string.Join(", ", libs.Select(l => $"'**/{l}'"));
            string injection = $"\n            {Marker}\n            excludes += [{list}]";
            text = text.Insert(idx + anchor.Length, injection);

            File.WriteAllText(launcherGradle, text);
            Debug.Log($"[XRScanNativeLibExcluder] Excluded {libs.Length} native lib(s) from the {vendor} APK: {string.Join(", ", libs)}");
        }

        /// <summary>File names (with extension) of the PICO SDK's Android Java plugins.</summary>
        internal static List<string> PicoJavaPluginFileNames() =>
            PluginImporter.GetAllImporters()
                .Select(p => p.assetPath)
                .Where(a => a.StartsWith(PicoPackagePath, StringComparison.Ordinal) &&
                            (a.EndsWith(".aar", StringComparison.OrdinalIgnoreCase) ||
                             a.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)))
                .Select(Path.GetFileName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>
        /// Deletes the PICO <c>.aar</c>/<c>.jar</c> plugins Unity copied into <c>unityLibrary/libs</c>
        /// and drops the <c>build.gradle</c> dependency lines that name them.
        /// </summary>
        private static void RemovePicoJavaPlugins(string unityLibraryPath)
        {
            var plugins = PicoJavaPluginFileNames();
            if (plugins.Count == 0) return;

            var removedFiles = new List<string>();
            var libsDir = Path.Combine(unityLibraryPath, "libs");
            foreach (var file in plugins)
            {
                var candidate = Path.Combine(libsDir, file);
                if (!File.Exists(candidate)) continue;
                File.Delete(candidate);
                removedFiles.Add(file);
            }

            int removedLines = 0;
            var gradlePath = Path.Combine(unityLibraryPath, "build.gradle");
            if (File.Exists(gradlePath))
            {
                var stems = plugins.Select(Path.GetFileNameWithoutExtension).ToArray();
                var lines = File.ReadAllLines(gradlePath);
                var kept = new List<string>(lines.Length);
                foreach (var line in lines)
                {
                    bool dependencyOnPico = line.Contains("implementation") &&
                        (stems.Any(s => line.Contains($"'{s}'") || line.Contains($"\"{s}\"")) ||
                         plugins.Any(f => line.Contains("libs/" + f)));
                    if (dependencyOnPico) { removedLines++; continue; }
                    kept.Add(line);
                }
                if (removedLines > 0) File.WriteAllLines(gradlePath, kept);
            }

            Debug.Log($"[XRScanNativeLibExcluder] Removed {removedFiles.Count} PICO Java plugin(s) from the Gradle project " +
                      $"({string.Join(", ", removedFiles)}) and {removedLines} build.gradle dependency line(s).");
        }
    }
}
