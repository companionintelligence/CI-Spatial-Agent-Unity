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
    /// PICO off). A Pico build flips the define and vendor features and leaves them modified until the
    /// next Quest build — don't commit that state. Builds also leave editor side effects in tracked
    /// files (docs/BUILD.md lists them), so check <c>git status</c> before committing.</para>
    /// <para>In batch mode (<c>-executeMethod</c>, without <c>-quit</c>) the editor exits when the build
    /// ends: 0 = built, 1 = failed, threw, or wrote no fresh APK.</para>
    /// <para>Signing and versionCode come from the environment (<see cref="AndroidReleaseSettings"/>): the
    /// release keystore only when <c>KEYSTORE_PATH</c>, <c>KEYSTORE_PASS</c>, <c>KEY_ALIAS</c> and
    /// <c>KEY_PASS</c> are set, else Unity's debug key; <c>-release</c> makes a build without them fail.
    /// <c>-aab</c> builds an Android App Bundle instead of an APK.</para>
    /// </summary>
    public static class XRScanBuilder
    {
        private const string Scene = "Assets/XRScan/App/Scenes/XRScan_Main.unity";
        private const string QuestDefine = "XRSCAN_QUEST";
        private const string PicoDefine = "XRSCAN_PICO";
        private const string QuestOutput = "CISpatialCompanion-Quest";
        private const string PicoOutput = "CISpatialCompanion-Pico";

        /// <summary>Command-line flag: build an Android App Bundle (<c>.aab</c>) instead of an APK.</summary>
        internal const string AppBundleFlag = "-aab";

        /// <summary>Android target SDK of every APK: the Meta Horizon Store requires exactly API 34.</summary>
        internal const AndroidSdkVersions TargetSdk = AndroidSdkVersions.AndroidApiLevel34;

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
                // Fail on missing release settings before XRScanBuildPackages changes the project.
                if (!CheckReleaseSettings(out _, out _))
                {
                    if (Application.isBatchMode)
                        EditorApplication.Exit(1);
                    return;
                }
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
            Debug.Log("[XRScanBuilder] Android player settings configured (IL2CPP / ARM64 / Vulkan / minSDK32 / targetSDK34 / Linear / SPI / GameActivity).");
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
            // The Meta Horizon Store requires targetSdkVersion 34 exactly for apps created since March
            // 2026. "Automatic" would target the highest SDK platform installed (36 with this editor), so
            // pin it. The Pico APK uses the same value.
            PlayerSettings.Android.targetSdkVersion = TargetSdk;
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
            if (!CheckReleaseSettings(out var versionCode, out var signing))
                return false;
            bool appBundle = AndroidReleaseSettings.HasFlag(Environment.GetCommandLineArgs(), AppBundleFlag);
            var apkName = (pico ? PicoOutput : QuestOutput) + (appBundle ? ".aab" : ".apk");
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

            var restoreReleaseSettings = ApplyReleaseSettings(versionCode, signing);
            // EditorUserBuildSettings.buildAppBundle picks the Gradle bundle task; it lives in Library,
            // but is put back too so the next menu build makes an APK again.
            bool previousAppBundle = EditorUserBuildSettings.buildAppBundle;
            EditorUserBuildSettings.buildAppBundle = appBundle;
            try
            {
                return BuildPlayer(apkName);
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = previousAppBundle;
                restoreReleaseSettings();
            }
        }

        /// <summary>
        /// Builds the app scene to <paramref name="apkName"/> (an <c>.apk</c>, or an <c>.aab</c> when
        /// <see cref="EditorUserBuildSettings.buildAppBundle"/> is on) in <see cref="OutputDirectory"/>.
        /// </summary>
        private static bool BuildPlayer(string apkName)
        {
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

            Debug.LogError($"[XRScanBuilder] {apkName} build {s.result}{(apkWritten ? "" : ", nothing written to " + apkName)}, {s.totalErrors} error(s){errorList}");
            return false;
        }

        /// <summary>
        /// Resolves the build's versionCode and release signing from the environment and logs why a build
        /// cannot go ahead: an unusable version, <c>-release</c> without all four signing variables, or a
        /// <c>KEYSTORE_PATH</c> that is not a file. Never logs the values themselves.
        /// </summary>
        private static bool CheckReleaseSettings(out int versionCode, out AndroidReleaseSettings.Signing signing)
        {
            signing = null;
            if (!AndroidReleaseSettings.TryGetVersionCode(PlayerSettings.bundleVersion, Environment.GetEnvironmentVariable,
                                                          out versionCode, out var versionError))
            {
                Debug.LogError("[XRScanBuilder] " + versionError);
                return false;
            }

            bool release = AndroidReleaseSettings.HasFlag(Environment.GetCommandLineArgs(), AndroidReleaseSettings.ReleaseFlag);
            signing = AndroidReleaseSettings.ReadSigning(Environment.GetEnvironmentVariable, out var missing);
            if (signing == null)
            {
                var names = string.Join(", ", missing);
                if (release)
                {
                    Debug.LogError($"[XRScanBuilder] {AndroidReleaseSettings.ReleaseFlag} needs the release keystore, but {names} " +
                                   $"{(missing.Length == 1 ? "is" : "are")} not set. Set {string.Join(", ", AndroidReleaseSettings.SigningVariables)} " +
                                   "in the environment (see docs/BUILD.md).");
                    return false;
                }
                if (missing.Length < AndroidReleaseSettings.SigningVariables.Length)
                    Debug.LogWarning($"[XRScanBuilder] {names} not set, so the release keystore is not used; signing with Unity's debug key.");
                return true;
            }

            if (!File.Exists(signing.KeystorePath))
            {
                Debug.LogError($"[XRScanBuilder] {AndroidReleaseSettings.KeystorePathVariable} does not point to a file.");
                signing = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Applies the versionCode and signing for this build and returns the action that puts the previous
        /// Player Settings back, so neither the keystore path and alias nor the build's versionCode end up in
        /// ProjectSettings.asset. The passwords only ever live in memory. A debug-signed build leaves the
        /// keystore settings alone.
        /// </summary>
        private static Action ApplyReleaseSettings(int versionCode, AndroidReleaseSettings.Signing signing)
        {
            var previousVersionCode = PlayerSettings.Android.bundleVersionCode;
            var previousUseCustomKeystore = PlayerSettings.Android.useCustomKeystore;
            Action restoreKeystore = () => { };

            PlayerSettings.Android.bundleVersionCode = versionCode;
            PlayerSettings.Android.useCustomKeystore = signing != null;
            if (signing != null)
            {
                restoreKeystore = UseKeystore(signing);
                Debug.Log($"[XRScanBuilder] Signing with the {signing}; versionCode {versionCode}.");
            }
            else
            {
                Debug.Log($"[XRScanBuilder] No release keystore in the environment: signing with Unity's debug key; versionCode {versionCode}.");
            }

            return () =>
            {
                restoreKeystore();
                PlayerSettings.Android.useCustomKeystore = previousUseCustomKeystore;
                PlayerSettings.Android.bundleVersionCode = previousVersionCode;
                AssetDatabase.SaveAssets();
            };
        }

        /// <summary>
        /// Points Player Settings at the release keystore and returns the action that puts the previous
        /// keystore settings back. The keystore and alias names are restored through the serialized
        /// PlayerSettings object, because the keystoreName setter stores an empty name as "{inproject}: ",
        /// which would leave ProjectSettings.asset modified.
        /// </summary>
        private static Action UseKeystore(AndroidReleaseSettings.Signing signing)
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")
                .FirstOrDefault(o => o is PlayerSettings);
            var serialized = settings != null ? new SerializedObject(settings) : null;
            var keystoreName = serialized?.FindProperty("AndroidKeystoreName")?.stringValue;
            var keyaliasName = serialized?.FindProperty("AndroidKeyaliasName")?.stringValue;
            var keystorePass = PlayerSettings.Android.keystorePass;
            var keyaliasPass = PlayerSettings.Android.keyaliasPass;

            PlayerSettings.Android.keystoreName = Path.GetFullPath(signing.KeystorePath);
            PlayerSettings.Android.keystorePass = signing.KeystorePass;
            PlayerSettings.Android.keyaliasName = signing.KeyAlias;
            PlayerSettings.Android.keyaliasPass = signing.KeyPass;

            return () =>
            {
                PlayerSettings.Android.keystorePass = keystorePass;
                PlayerSettings.Android.keyaliasPass = keyaliasPass;
                if (serialized != null && keystoreName != null && keyaliasName != null)
                {
                    serialized.Update();
                    serialized.FindProperty("AndroidKeystoreName").stringValue = keystoreName;
                    serialized.FindProperty("AndroidKeyaliasName").stringValue = keyaliasName;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    PlayerSettings.Android.keystoreName = keystoreName ?? string.Empty;
                    PlayerSettings.Android.keyaliasName = keyaliasName ?? string.Empty;
                }
            };
        }

        /// <summary>
        /// Where APKs and app bundles are written: <c>-xrscanBuildDir &lt;dir&gt;</c> on the command line, else the
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
        /// profile). Quest build: Meta/Oculus features on (except <see cref="UnusedMetaFeatures"/> and
        /// Social Eye Gaze), PICO off; Pico build: the reverse. Saves the settings asset only when a
        /// toggle changed.
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
                // Meta OpenXR 2.6 added "Meta Quest: Social Eye Gaze", which adds the
                // com.oculus.permission.EYE_TRACKING permission and the oculus.software.eye_tracking
                // feature. The app does not use eye tracking. The pinned Meta OpenXR 2.5.1 has no such
                // feature; this keeps a package upgrade from turning it on with the other Meta features.
                bool isSocialEyeGaze = n.EndsWith("MetaSocialEyeGazeFeature", System.StringComparison.Ordinal);
                // Meta features + the Oculus/Meta Touch controller interaction profile stay on for Quest.
                bool isMeta = n.Contains("Meta") || n.Contains("OculusTouch");

                bool target;
                if (isPico) target = !quest;
                else if (isDeprecatedOculusQuest) target = false; // always off — use the Meta Quest feature
                else if (isSocialEyeGaze) target = false; // always off — opt in deliberately if eye tracking is needed
                else if (UnusedMetaFeatures.Contains(n)) target = false; // see UnusedMetaFeatures
                else if (isMeta) target = quest;
                else continue; // leave vendor-neutral features (hand tracking, debugger, ...) untouched

                if (f.enabled != target)
                {
                    f.enabled = target;
                    UnityEditor.EditorUtility.SetDirty(f);
                    changed++;
                }

                // The Meta AR camera feature, on for Quest builds only:
                //  - cameraImageSupportEnabled: passthrough scanning needs CPU camera images. Turning
                //    this on is also what makes com.unity.xr.meta-openxr add the
                //    horizonos.permission.HEADSET_CAMERA permission for us.
                //  - passthroughPreSplashScreen: the companion is a passthrough MR app, so the headset
                //    shows passthrough instead of its opaque loading screen before Unity's splash
                //    (meta-openxr adds the com.oculus.ossplash.background = passthrough-contextual
                //    meta-data to the manifest).
                // Reflection keeps this assembly free of a hard meta-openxr dependency.
                if (target && n.EndsWith("ARCameraFeature", System.StringComparison.Ordinal))
                {
                    changed += EnableBoolProperty(f, "cameraImageSupportEnabled");
                    changed += EnableBoolProperty(f, "passthroughPreSplashScreen");
                }
            }

            if (changed > 0)
            {
                UnityEditor.EditorUtility.SetDirty(settings);
                UnityEditor.AssetDatabase.SaveAssets();
            }
            Debug.Log($"[XRScanBuilder] OpenXR features configured for {(quest ? "Quest" : "Pico")} ({changed} toggled).");
        }

        /// <summary>Sets a bool property of an OpenXR feature to true; returns 1 when that changed it.</summary>
        private static int EnableBoolProperty(UnityEngine.XR.OpenXR.Features.OpenXRFeature feature, string property)
        {
            var prop = feature.GetType().GetProperty(property);
            if (prop == null || !prop.CanWrite || prop.PropertyType != typeof(bool))
            {
                Debug.LogWarning($"[XRScanBuilder] {feature.GetType().Name} has no writable bool '{property}'; it was not enabled.");
                return 0;
            }
            if ((bool)prop.GetValue(feature)) return 0;
            prop.SetValue(feature, true);
            UnityEditor.EditorUtility.SetDirty(feature);
            return 1;
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
