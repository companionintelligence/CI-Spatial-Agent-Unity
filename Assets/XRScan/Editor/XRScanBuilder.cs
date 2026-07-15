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
    }
}
