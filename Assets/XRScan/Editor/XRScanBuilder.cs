using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace XRScan.EditorTools
{
    /// <summary>
    /// One-click Android (Quest 3 / Pico) APK builds. Applies the OpenXR-first player settings,
    /// sets the per-platform scripting define, and builds the app scene to <c>/Builds</c>.
    /// Menu: <b>XRScan &gt; Build</b>. The two headsets share one OpenXR APK; the define only
    /// gates any platform-specific asmdef code.
    /// </summary>
    public static class XRScanBuilder
    {
        private const string Scene = "Assets/XRScan/App/Scenes/XRScan_Main.unity";
        private const string QuestDefine = "XRSCAN_QUEST";
        private const string PicoDefine = "XRSCAN_PICO";

        [MenuItem("XRScan/Build/Quest 3 (Android APK)", false, 0)]
        public static void BuildQuest() => Build(QuestDefine, "CISpatialCompanion-Quest.apk");

        [MenuItem("XRScan/Build/Pico (Android APK)", false, 1)]
        public static void BuildPico() => Build(PicoDefine, "CISpatialCompanion-Pico.apk");

        /// <summary>Diagnostic: log every Android OpenXR feature and whether it is enabled.</summary>
        [MenuItem("XRScan/Build/Dump OpenXR features", false, 21)]
        public static void DumpOpenXRFeatures()
        {
            var settings = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null) { Debug.LogError("[DumpOpenXR] no Android OpenXR settings"); return; }

            var sb = new System.Text.StringBuilder("[DumpOpenXR] Android features:\n");
            foreach (var f in settings.GetFeatures())
            {
                if (f == null) continue;
                sb.Append(f.enabled ? "  [x] " : "  [ ] ").Append(f.GetType().FullName).Append('\n');
            }
            Debug.Log(sb.ToString());
        }

        [MenuItem("XRScan/Build/Configure Android settings (no build)", false, 20)]
        public static void ConfigureMenu()
        {
            ConfigureAndroid();
            Debug.Log("[XRScanBuilder] Android player settings configured (IL2CPP / ARM64 / Vulkan / minSDK32 / Linear / SPI).");
        }

        /// <summary>Applies the Quest/Pico-ready Android player settings. Idempotent.</summary>
        public static void ConfigureAndroid()
        {
            var nbt = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(nbt, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan });
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.stereoRenderingPath = StereoRenderingPath.Instancing;
        }

        private static void Build(string keepDefine, string apkName)
        {
            ConfigureAndroid();
            SetPlatformDefine(keepDefine);
            ConfigureOpenXRFeatures(keepDefine == QuestDefine);
            // The other vendor's native libs (and MockHMD's) are excluded from the APK at packaging
            // time by XRScanNativeLibExcluder, and PICO's stray permission by XRScanManifestPostProcessor
            // — both keyed off the platform define set above. (PluginImporter flags don't work for
            // plugins that live in read-only packages, so packaging-level exclusion is used instead.)

            var dir = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
            Directory.CreateDirectory(dir);

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = Path.Combine(dir, apkName),
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            if (s.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                Debug.Log($"[XRScanBuilder] Built {apkName} — {s.totalSize / (1024 * 1024)} MB at {s.outputPath}");
            else
                Debug.LogError($"[XRScanBuilder] {apkName} build {s.result}: {s.totalErrors} error(s).");
        }

        /// <summary>
        /// Enables the target headset's OpenXR features and disables the other's — Quest and PICO
        /// can't both be active in one build (OpenXR validation allows only one vendor's interaction
        /// profile). Quest build: Meta/Oculus features on, PICO off; Pico build: the reverse.
        /// </summary>
        private static void ConfigureOpenXRFeatures(bool quest)
        {
            var settings = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
            {
                Debug.LogWarning("[XRScanBuilder] No Android OpenXR settings found — is the OpenXR loader enabled for Android?");
                return;
            }

            int changed = 0;
            foreach (var f in settings.GetFeatures())
            {
                if (f == null) continue;
                string n = f.GetType().FullName ?? string.Empty;
                bool isPico = n.Contains("PICO") || n.Contains("Pico") || n.Contains("Poxr");
                // The legacy "Oculus Quest" support feature is deprecated in favour of "Meta Quest".
                bool isDeprecatedOculusQuest = n.Contains("OculusQuest");
                // Meta features + the Oculus/Meta Touch controller interaction profile stay on for Quest.
                bool isMeta = n.Contains("Meta") || n.Contains("OculusTouch");

                bool target;
                if (isPico) target = !quest;
                else if (isDeprecatedOculusQuest) target = false; // always off — use the Meta Quest feature
                else if (isMeta) target = quest;
                else continue; // leave vendor-neutral features (hand tracking, debugger, ...) untouched

                if (f.enabled != target)
                {
                    f.enabled = target;
                    UnityEditor.EditorUtility.SetDirty(f);
                    changed++;
                }

                // Quest passthrough scanning needs CPU camera images from the Meta AR camera
                // feature. Turning this on is also what makes com.unity.xr.meta-openxr add the
                // horizonos.permission.HEADSET_CAMERA permission for us. Reflection keeps this
                // assembly free of a hard meta-openxr dependency.
                if (target && n.EndsWith("ARCameraFeature", System.StringComparison.Ordinal))
                {
                    var prop = f.GetType().GetProperty("cameraImageSupportEnabled");
                    if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool)
                        && !(bool)prop.GetValue(f))
                    {
                        prop.SetValue(f, true);
                        UnityEditor.EditorUtility.SetDirty(f);
                        changed++;
                    }
                }
            }

            UnityEditor.EditorUtility.SetDirty(settings);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"[XRScanBuilder] OpenXR features configured for {(quest ? "Quest" : "Pico")} ({changed} toggled).");
        }

        /// <summary>Keeps exactly one of the platform defines, removing the other.</summary>
        private static void SetPlatformDefine(string keep)
        {
            var nbt = NamedBuildTarget.Android;
            var defines = PlayerSettings.GetScriptingDefineSymbols(nbt)
                .Split(';')
                .Where(d => !string.IsNullOrWhiteSpace(d) && d != QuestDefine && d != PicoDefine)
                .ToList();
            defines.Add(keep);
            PlayerSettings.SetScriptingDefineSymbols(nbt, string.Join(";", defines));
        }

        /// <summary>
        /// True when the current Android scripting defines target a Quest build (set by
        /// <see cref="SetPlatformDefine"/> at the start of a build). The gradle/manifest
        /// post-processors read this to strip the non-target vendor's native libs and permissions.
        /// </summary>
        internal static bool BuildingForQuest() =>
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)
                .Split(';').Any(d => d == QuestDefine);
    }
}
