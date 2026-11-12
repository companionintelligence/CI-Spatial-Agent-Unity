using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace XRScan.EditorTools
{
    /// <summary>
    /// One-click Android (Quest 3 / Pico) APK builds. Applies the OpenXR-first player settings,
    /// sets the per-platform scripting define, and builds the app scene to <c>/Builds</c>.
    /// Menu: <b>XRScan &gt; Build</b>. The two headsets share one codebase; what differs per APK is the
    /// define, the enabled OpenXR feature group and the vendor packages present while the APK is built
    /// (<see cref="XRScanBuildPackages"/>: the PICO SDK only for Pico, MCP for Unity never).
    /// <para>The committed settings are the Quest state (define <c>XRSCAN_QUEST</c>, Meta features on,
    /// PICO off), so a Quest build changes no tracked file. A Pico build flips the define and vendor
    /// features and leaves them modified until the next Quest build — don't commit that state.</para>
    /// <para>In batch mode (<c>-executeMethod</c>, without <c>-quit</c>) the editor exits when the build
    /// ends: 0 = built, 1 = failed, threw, or wrote no fresh APK.</para>
    /// </summary>
    public static class XRScanBuilder
    {
        private const string Scene = "Assets/XRScan/App/Scenes/XRScan_Main.unity";
        private const string QuestDefine = "XRSCAN_QUEST";
        private const string PicoDefine = "XRSCAN_PICO";
        private const string QuestApk = "CISpatialCompanion-Quest.apk";
        private const string PicoApk = "CISpatialCompanion-Pico.apk";

        /// <summary>
        /// Meta (com.unity.xr.meta-openxr) features the app does not use; every build keeps them off.
        /// No script, scene or prefab uses an ARAnchorManager / ARAnchor, ARBoundingBoxManager or
        /// AROcclusionManager, or calls the colocation, boundary-visibility or display-refresh-rate APIs.
        /// Enabled, they only request extra OpenXR extensions and (Anchors, Boundary Visibility,
        /// Colocation Discovery) add the IMPORT_EXPORT_IOT_MAP_DATA, BOUNDARY_VISIBILITY and
        /// USE_COLOCATION_DISCOVERY_API permissions. To use one, remove it here and enable it in
        /// Assets/XR/Settings/OpenXRPackageSettings.asset.
        /// </summary>
        private static readonly HashSet<string> UnusedMetaFeatures = new HashSet<string>
        {
            "UnityEngine.XR.OpenXR.Features.Meta.ARAnchorFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.ARBoundingBoxFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.AROcclusionFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.BoundaryVisibilityFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.ColocationDiscoveryFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.DisplayUtilitiesFeature",
        };

        [MenuItem("XRScan/Build/Quest 3 (Android APK)", false, 0)]
        public static void BuildQuest() => Build(pico: false);

        [MenuItem("XRScan/Build/Pico (Android APK)", false, 1)]
        public static void BuildPico() => Build(pico: true);

        private static void Build(bool pico)
        {
            try
            {
                XRScanBuildPackages.Build(pico);
            }
            catch (Exception e)
            {
                // XRScanBuildPackages reports its own failures and, in batch mode, exits with the build
                // result. This catches an exception thrown before it gets that far (e.g. an unreadable
                // manifest), which would otherwise leave a headless editor without a failing exit code.
                Debug.LogException(e);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
            }
        }

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
            Debug.Log("[XRScanBuilder] Android player settings configured (IL2CPP / ARM64 / Vulkan / minSDK32 / Linear / SPI / GameActivity).");
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
            // OpenXR's Meta Quest validation requires GameActivity on Unity 6+. Nothing here depends on
            // the legacy activity: no custom manifest or Activity code, and the bundled .aar/.java
            // plugins (PICO, ARCore permissions, Oculus) only use UnityPlayer.currentActivity or
            // android.app.Activity, which GameActivity also provides.
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
        }

        /// <summary>
        /// Configures and builds one APK in the current project state. Called by
        /// <see cref="XRScanBuildPackages"/> once the packages the APK must not contain are out of the
        /// project; returns whether the build succeeded.
        /// </summary>
        internal static bool BuildNow(bool pico)
        {
            var apkName = pico ? PicoApk : QuestApk;
            ConfigureAndroid();
            SetPlatformDefine(pico ? PicoDefine : QuestDefine);
            ConfigureOpenXRFeatures(!pico);
            // The build callbacks below are keyed off the platform define set above. They are a second
            // line of defence for content that XRScanBuildPackages already took out of the project:
            //  - XRScanVendorPackageGuard fails a non-Pico Android build if the PICO SDK (or a file it
            //    generated in Assets/Resources) is still in the project,
            //  - XRScanPlayerAssemblyFilter drops managed assemblies that must not ship,
            //  - XRScanNativeLibExcluder drops the other vendor's native libs (and, outside Pico builds,
            //    PICO's Java plugins) from the Gradle project,
            //  - XRScanManifestPostProcessor strips PICO's stray permission from Quest builds.

            var dir = OutputDirectory();
            Directory.CreateDirectory(dir);
            var apkPath = Path.Combine(dir, apkName);

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            var startedUtc = DateTime.UtcNow;
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            // First line of each error only: the full text is already in the editor log, and a Gradle
            // failure's text includes the whole process environment.
            var errors = report.steps
                .SelectMany(step => step.messages)
                .Where(m => m.type == LogType.Error || m.type == LogType.Exception || m.type == LogType.Assert)
                .Select(m => (m.content ?? string.Empty).Trim().Split('\n')[0].Trim())
                .Where(line => line.Length > 0)
                .Take(20)
                .ToList();
            var errorList = errors.Count > 0 ? ":\n  " + string.Join("\n  ", errors) : ".";

            // A stale APK from an earlier build must not pass for this one.
            var apk = new FileInfo(apkPath);
            bool apkWritten = apk.Exists && apk.Length > 0 && apk.LastWriteTimeUtc >= startedUtc.AddSeconds(-5);

            if (s.result == BuildResult.Succeeded && apkWritten)
            {
                Debug.Log($"[XRScanBuilder] Built {apkName} — {apk.Length / (1024.0 * 1024.0):F1} MB at {apk.FullName}");
                if (s.totalErrors > 0)
                    Debug.LogWarning($"[XRScanBuilder] Build succeeded but logged {s.totalErrors} error(s){errorList}");
                return true;
            }

            Debug.LogError($"[XRScanBuilder] {apkName} build {s.result}{(apkWritten ? "" : ", no APK written")}, {s.totalErrors} error(s){errorList}");
            return false;
        }

        /// <summary>
        /// Where APKs are written: <c>-xrscanBuildDir &lt;dir&gt;</c> on the command line, else the
        /// <c>XRSCAN_BUILD_DIR</c> environment variable, else <c>&lt;project&gt;/Builds</c>. Lets CI and
        /// scripted builds keep their output outside the working tree.
        /// </summary>
        internal static string OutputDirectory()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-xrscanBuildDir", System.StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(args[i + 1]);
            }
            var env = System.Environment.GetEnvironmentVariable("XRSCAN_BUILD_DIR");
            if (!string.IsNullOrWhiteSpace(env)) return Path.GetFullPath(env);
            return Path.Combine(Directory.GetCurrentDirectory(), "Builds");
        }

        /// <summary>
        /// Enables the target headset's OpenXR features and disables the other's — Quest and PICO
        /// can't both be active in one build (OpenXR validation allows only one vendor's interaction
        /// profile). Quest build: Meta/Oculus features on (except <see cref="UnusedMetaFeatures"/>),
        /// PICO off; Pico build: the reverse. Saves the settings asset only when a toggle changed.
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
                else if (UnusedMetaFeatures.Contains(n)) target = false; // see UnusedMetaFeatures
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

            if (changed > 0)
            {
                UnityEditor.EditorUtility.SetDirty(settings);
                UnityEditor.AssetDatabase.SaveAssets();
            }
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
        internal static bool BuildingForQuest() => HasAndroidDefine(QuestDefine);

        /// <summary>
        /// True only for an explicit Pico build (<c>XRSCAN_PICO</c>). PICO SDK content — managed
        /// assemblies, native libs, Java plugins, permissions — ships in Pico builds only; any other
        /// Android build (Quest, or one made without either define) leaves it out.
        /// </summary>
        internal static bool BuildingForPico() => HasAndroidDefine(PicoDefine);

        private static bool HasAndroidDefine(string define) =>
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)
                .Split(';').Any(d => d == define);
    }
}
