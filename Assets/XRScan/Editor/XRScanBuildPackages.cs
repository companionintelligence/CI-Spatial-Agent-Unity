using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace XRScan.EditorTools
{
    /// <summary>
    /// Per-vendor package selection for player builds. Unity packs every installed package's
    /// <c>Resources</c> folders, runtime assemblies, plugins and the settings objects they own into every
    /// player, whatever loader or feature is active, and nothing inside a read-only package can be
    /// excluded piecemeal. So <see cref="XRScanBuilder"/> builds each APK from a project that contains only
    /// what that APK may ship:
    /// <list type="bullet">
    /// <item><b>Every build:</b> MCP for Unity (<c>com.coplaydev.unity-mcp</c>) is removed. It is
    /// edit-time tooling.</item>
    /// <item><b>Quest (any build other than Pico):</b> the project as it is. The PICO OpenXR SDK
    /// (<c>com.unity.xr.openxr.picoxr</c>) is not in <c>Packages/manifest.json</c>; if it was added there
    /// anyway, it is removed for the build too, and the settings files PICO's editor code generates under
    /// <c>Assets/Resources</c> are moved aside. The PICO SDK licence forbids reproducing its material
    /// (code, controller and hand models, textures, shaders) without PICO's written permission.</item>
    /// <item><b>Pico:</b> the PICO SDK is added from <see cref="PicoPackageSource"/> for the build, and the
    /// settings files its editor code generates during the build are deleted afterwards.</item>
    /// </list>
    /// Adding or removing a package needs a script reload, so such a build runs in two steps: back up
    /// <c>Packages/manifest.json</c>, <c>Packages/packages-lock.json</c> and, when the PICO SDK is added or
    /// removed, the OpenXR settings to <c>Library/XRScanBuild</c>; change the packages; and after the domain
    /// reload build and put every file back. In batch mode the editor exits by itself when the build ends
    /// (0 = built), so <c>-quit</c> must not be passed. An editor that dies mid-build restores the files the
    /// next time it opens the project.
    /// </summary>
    [InitializeOnLoad]
    internal static class XRScanBuildPackages
    {
        internal const string PicoPackage = "com.unity.xr.openxr.picoxr";
        internal const string McpPackage = "com.coplaydev.unity-mcp";

        /// <summary>
        /// Where a Pico build installs the PICO OpenXR SDK from: PICO's repository at the commit of its tag
        /// <c>release_1.4.0</c> (package version 1.4.1). The SDK is not in <c>Packages/manifest.json</c>, so
        /// the project, the tests and every Quest build run without it.
        /// </summary>
        internal const string PicoPackageSource =
            "https://github.com/Pico-Developer/PICO-Unity-OpenXR-SDK.git#3aa3e62bff41df618529eeb60ff02c29a515dafe";

        /// <summary>Packages that never ship in a player (edit-time tooling).</summary>
        internal static readonly string[] EditorOnlyPackages = { McpPackage };

        /// <summary>Packages that ship only in a Pico build.</summary>
        internal static readonly string[] PicoOnlyPackages = { PicoPackage };

        /// <summary>
        /// Files PICO's editor code creates in the project (<c>PXR_PlatformSetting</c> from its platform
        /// build preprocessor, <c>PICOProjectSetting</c> from its OpenXR feature, the PC config). They sit
        /// in a <c>Resources</c> folder, so any build would ship them.
        /// </summary>
        internal static readonly string[] PicoGeneratedAssets =
        {
            "Assets/Resources/PXR_PlatformSetting.asset",
            "Assets/Resources/PICOProjectSetting.asset",
            "Assets/Resources/PicoSdkPCConfig.json",
        };

        private const string ResourcesFolder = "Assets/Resources";
        private const string ManifestPath = "Packages/manifest.json";
        private const string LockPath = "Packages/packages-lock.json";
        private const string DefaultOpenXRSettingsPath = "Assets/XR/Settings/OpenXRPackageSettings.asset";
        private const string WorkDir = "Library/XRScanBuild";
        private const string StatePath = WorkDir + "/pending-build.json";
        private const string SessionKey = "XRScan.BuildPackages.Session";
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

        [Serializable]
        private class PendingBuild
        {
            public bool pico;
            public string session;
            public long startedUtcTicks;
            public string[] addedPackages = Array.Empty<string>();
            public string[] removedPackages = Array.Empty<string>();
            public string[] backedUpFiles = Array.Empty<string>();
            public string[] stashedFiles = Array.Empty<string>();
            /// <summary>PICO-generated files that did not exist when the Pico build started.</summary>
            public string[] deleteIfCreated = Array.Empty<string>();
            /// <summary>True when <see cref="ResourcesFolder"/> did not exist when the build started.</summary>
            public bool resourcesFolderWasMissing;
        }

        private static AddAndRemoveRequest s_PackageRequest;
        private static double s_NextPoll;

        static XRScanBuildPackages()
        {
            var state = LoadState();
            if (state == null) return;

            if (state.session != CurrentSession())
            {
                // Left behind by an editor that quit or crashed mid-build: undo the package change once
                // the asset database is usable (not from inside the domain load).
                EditorApplication.delayCall += () =>
                {
                    var stale = LoadState();
                    if (stale == null || stale.session == CurrentSession()) return;
                    Debug.LogWarning("[XRScanBuildPackages] Found an unfinished build from an earlier editor session; restoring " +
                                     "Packages/manifest.json, the lock file, the OpenXR settings and stashed assets.");
                    Restore(stale);
                    AssetDatabase.Refresh();
                    if (ChangesPackages(stale)) Client.Resolve();
                };
                return;
            }

            // Same session: the packages were changed and scripts reloaded. Build once Unity is idle.
            EditorApplication.update += PollAndContinue;
        }

        /// <summary>The packages a build for the given headset leaves out of the project.</summary>
        internal static string[] PackagesToRemoveFor(bool pico) =>
            pico ? EditorOnlyPackages.ToArray() : EditorOnlyPackages.Concat(PicoOnlyPackages).ToArray();

        /// <summary>The packages a build for the given headset needs in the project.</summary>
        internal static string[] PackagesToAddFor(bool pico) =>
            pico ? PicoOnlyPackages.ToArray() : Array.Empty<string>();

        /// <summary>What <c>Client.AddAndRemove</c> installs a package from.</summary>
        internal static string SourceOf(string packageName) =>
            packageName == PicoPackage ? PicoPackageSource : packageName;

        /// <summary>True while an <see cref="XRScanBuilder"/> build that changed the package set is in progress.</summary>
        internal static bool BuildInProgress => File.Exists(StatePath);

        internal static bool IsRegistered(string packageName) =>
            PackageInfo.GetAllRegisteredPackages().Any(p => p.name == packageName);

        /// <summary>Entry point for <see cref="XRScanBuilder.BuildQuest"/> / <see cref="XRScanBuilder.BuildPico"/>.</summary>
        internal static void Build(bool pico)
        {
            if (BuildInProgress)
            {
                Fail(null, $"Another XRScan build is still in progress ({StatePath} exists). " +
                           "Wait for it to finish, or delete that file after restoring Packages/manifest.json.");
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Fail(null, "Exit Play mode before building.");
                return;
            }

            var manifest = File.ReadAllText(ManifestPath);
            var remove = PackagesToRemoveFor(pico).Where(p => InManifest(manifest, p)).ToArray();
            var indirect = PackagesToRemoveFor(pico).Where(p => !InManifest(manifest, p) && IsRegistered(p)).ToArray();
            if (indirect.Length > 0)
            {
                Fail(null, $"{string.Join(", ", indirect)} {(indirect.Length == 1 ? "is" : "are")} installed as a dependency " +
                           "of another package and cannot be removed for the build.");
                return;
            }
            var add = PackagesToAddFor(pico).Where(p => !InManifest(manifest, p)).ToArray();
            var stash = pico ? Array.Empty<string>() : PicoGeneratedAssets.Where(File.Exists).ToArray();

            if (remove.Length == 0 && add.Length == 0 && stash.Length == 0)
            {
                Finish(null, TryBuild(pico));
                return;
            }

            bool changesPackages = remove.Length > 0 || add.Length > 0;
            if (changesPackages && Application.isBatchMode && HasCommandLineFlag("-quit"))
            {
                Fail(null, $"This build {Describe(add, remove)} first, which needs a script reload that -quit would cut short. " +
                           "Run without -quit: the builder exits by itself (0 = built).");
                return;
            }

            var state = new PendingBuild
            {
                pico = pico,
                session = CurrentSession(),
                startedUtcTicks = DateTime.UtcNow.Ticks,
                addedPackages = add,
                removedPackages = remove,
                // The PICO SDK writes its settings files into Assets/Resources while it is installed. Delete
                // the ones this build creates, since the SDK is gone again afterwards.
                deleteIfCreated = add.Contains(PicoPackage) ? PicoGeneratedAssets.Where(f => !File.Exists(f)).ToArray() : Array.Empty<string>(),
                resourcesFolderWasMissing = !Directory.Exists(ResourcesFolder),
            };

            Directory.CreateDirectory(WorkDir);
            var backups = new List<string> { ManifestPath };
            if (File.Exists(LockPath)) backups.Add(LockPath);
            if (remove.Contains(PicoPackage) || add.Contains(PicoPackage))
            {
                // PICO's OpenXR feature objects live in this asset. A build that removes the SDK leaves them as
                // missing-script objects, one that adds it creates them and the Pico build switches them on, and
                // the build re-saves the asset either way. Put the exact bytes back after.
                var openxr = OpenXRSettingsPath();
                if (File.Exists(openxr)) backups.Add(openxr);
            }
            foreach (var file in backups) CopyFile(file, BackupPath(file));
            state.backedUpFiles = backups.ToArray();

            var stashed = new List<string>();
            foreach (var file in stash)
            {
                MoveFile(file, StashPath(file));
                stashed.Add(file);
                if (File.Exists(file + ".meta")) { MoveFile(file + ".meta", StashPath(file + ".meta")); stashed.Add(file + ".meta"); }
            }
            state.stashedFiles = stashed.ToArray();
            SaveState(state);

            if (stash.Length > 0)
                Debug.Log($"[XRScanBuildPackages] Moved {string.Join(", ", stash)} out of the project for this build.");

            if (!changesPackages)
            {
                AssetDatabase.Refresh();
                Finish(state, TryBuild(pico));
                return;
            }

            Debug.Log($"[XRScanBuildPackages] For the {(pico ? "Pico" : "Quest")} build: {Describe(add, remove)}; " +
                      "the build starts after the script reload and the package set is restored afterwards.");
            s_PackageRequest = Client.AddAndRemove(add.Length > 0 ? add.Select(SourceOf).ToArray() : null,
                                                   remove.Length > 0 ? remove : null);
            s_NextPoll = 0;
            EditorApplication.update += WatchPackageRequest;
        }

        /// <summary>Runs in the domain that asked UPM to change the packages, until the script reload replaces it.</summary>
        private static void WatchPackageRequest()
        {
            if (EditorApplication.timeSinceStartup < s_NextPoll) return;
            s_NextPoll = EditorApplication.timeSinceStartup + 1.0;

            var state = LoadState();
            if (state == null) { EditorApplication.update -= WatchPackageRequest; return; }

            if (s_PackageRequest != null && s_PackageRequest.IsCompleted && s_PackageRequest.Status == StatusCode.Failure)
            {
                EditorApplication.update -= WatchPackageRequest;
                Fail(state, $"Changing the packages ({Describe(state.addedPackages, state.removedPackages)}) failed: {s_PackageRequest.Error?.message}");
                return;
            }

            bool settled = s_PackageRequest != null && s_PackageRequest.IsCompleted &&
                           !EditorApplication.isCompiling && !EditorApplication.isUpdating;
            if (settled && EditorUtility.scriptCompilationFailed)
            {
                EditorApplication.update -= WatchPackageRequest;
                Fail(state, "Scripts no longer compile once the packages are changed (" +
                            Describe(state.addedPackages, state.removedPackages) +
                            "), so the build cannot start. See the compiler errors above.");
                return;
            }

            if (TimedOut(state))
            {
                EditorApplication.update -= WatchPackageRequest;
                Fail(state, "Timed out waiting for the package change and script reload.");
            }
        }

        /// <summary>Runs after the script reload: builds once the package set is in place and Unity is idle.</summary>
        private static void PollAndContinue()
        {
            if (EditorApplication.timeSinceStartup < s_NextPoll) return;
            s_NextPoll = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            var state = LoadState();
            if (state == null) { EditorApplication.update -= PollAndContinue; return; }

            var stillInstalled = state.removedPackages.Where(IsRegistered).ToArray();
            var notInstalled = state.addedPackages.Where(p => !IsRegistered(p)).ToArray();
            if (stillInstalled.Length > 0 || notInstalled.Length > 0)
            {
                // A script reload for another reason can come before the one for the package change.
                if (!TimedOut(state)) return;
                EditorApplication.update -= PollAndContinue;
                Fail(state, stillInstalled.Length > 0
                    ? $"{string.Join(", ", stillInstalled)} still installed after the package change."
                    : $"{string.Join(", ", notInstalled)} not installed after the package change.");
                return;
            }

            EditorApplication.update -= PollAndContinue;
            Finish(state, TryBuild(state.pico));
        }

        private static bool TryBuild(bool pico)
        {
            try { return XRScanBuilder.BuildNow(pico); }
            catch (Exception e) { Debug.LogException(e); return false; }
        }

        private static void Fail(PendingBuild state, string message)
        {
            Debug.LogError("[XRScanBuildPackages] " + message);
            Finish(state, false);
        }

        /// <summary>Puts the project back as it was and, in batch mode, exits with the build result.</summary>
        private static void Finish(PendingBuild state, bool built)
        {
            if (state != null)
            {
                var deleted = Restore(state);
                Debug.Log("[XRScanBuildPackages] Restored " + string.Join(", ", state.backedUpFiles.Concat(
                              state.stashedFiles.Where(f => !f.EndsWith(".meta", StringComparison.Ordinal)))) +
                          (deleted.Count > 0 ? $"; deleted {string.Join(", ", deleted)}, which the build created" : "") +
                          (ChangesPackages(state) ? " (the installed packages follow the restored manifest at the next package resolve)." : "."));
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(built ? 0 : 1);
                return;
            }

            if (state != null)
            {
                AssetDatabase.Refresh();
                if (ChangesPackages(state)) Client.Resolve();
            }
        }

        /// <summary>
        /// Copies the backups back, returns stashed assets, deletes the PICO-generated files a Pico build
        /// created and forgets the pending build. Returns the deleted paths.
        /// </summary>
        private static List<string> Restore(PendingBuild state)
        {
            foreach (var file in state.stashedFiles)
            {
                var stashed = StashPath(file);
                if (File.Exists(stashed)) MoveFile(stashed, file);
            }

            foreach (var file in state.backedUpFiles)
            {
                var backup = BackupPath(file);
                if (!File.Exists(backup)) continue;
                CopyFile(backup, file);
                if (file.StartsWith("Assets/", StringComparison.Ordinal))
                    AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceUpdate);
            }

            var deleted = new List<string>();
            foreach (var file in state.deleteIfCreated)
            {
                if (!File.Exists(file)) continue;
                File.Delete(file);
                if (File.Exists(file + ".meta")) File.Delete(file + ".meta");
                deleted.Add(file);
            }
            if (state.resourcesFolderWasMissing && deleted.Count > 0 && Directory.Exists(ResourcesFolder) &&
                !Directory.EnumerateFileSystemEntries(ResourcesFolder).Any())
            {
                Directory.Delete(ResourcesFolder);
                if (File.Exists(ResourcesFolder + ".meta")) File.Delete(ResourcesFolder + ".meta");
                deleted.Add(ResourcesFolder);
            }

            try { Directory.Delete(WorkDir, true); }
            catch (IOException e) { Debug.LogWarning($"[XRScanBuildPackages] Could not delete {WorkDir}: {e.Message}"); }
            return deleted;
        }

        private static bool ChangesPackages(PendingBuild state) =>
            state.addedPackages.Length > 0 || state.removedPackages.Length > 0;

        private static string Describe(string[] add, string[] remove)
        {
            var parts = new List<string>();
            if (add.Length > 0) parts.Add("adds " + string.Join(", ", add));
            if (remove.Length > 0) parts.Add("removes " + string.Join(", ", remove));
            return string.Join(" and ", parts);
        }

        private static bool TimedOut(PendingBuild state) =>
            DateTime.UtcNow - new DateTime(state.startedUtcTicks, DateTimeKind.Utc) > Timeout;

        internal static bool InManifest(string manifestJson, string packageName) =>
            Regex.IsMatch(manifestJson, "\"" + Regex.Escape(packageName) + "\"\\s*:");

        private static string OpenXRSettingsPath()
        {
            var settings = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            var path = settings != null ? AssetDatabase.GetAssetPath(settings) : null;
            return string.IsNullOrEmpty(path) ? DefaultOpenXRSettingsPath : path;
        }

        private static string CurrentSession()
        {
            var id = SessionState.GetString(SessionKey, string.Empty);
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                SessionState.SetString(SessionKey, id);
            }
            return id;
        }

        private static bool HasCommandLineFlag(string flag) =>
            Environment.GetCommandLineArgs().Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

        private static string BackupPath(string projectRelative) => Path.Combine(WorkDir, "backup", projectRelative);
        private static string StashPath(string projectRelative) => Path.Combine(WorkDir, "stash", projectRelative);

        private static void CopyFile(string from, string to)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to) ?? ".");
            File.Copy(from, to, true);
        }

        private static void MoveFile(string from, string to)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to) ?? ".");
            if (File.Exists(to)) File.Delete(to);
            File.Move(from, to);
        }

        private static PendingBuild LoadState()
        {
            if (!File.Exists(StatePath)) return null;
            try { return JsonUtility.FromJson<PendingBuild>(File.ReadAllText(StatePath)); }
            catch (Exception e)
            {
                Debug.LogError($"[XRScanBuildPackages] Unreadable {StatePath} ({e.Message}); restore Packages/manifest.json " +
                               $"from {WorkDir}/backup by hand if a build was interrupted.");
                return null;
            }
        }

        private static void SaveState(PendingBuild state)
        {
            Directory.CreateDirectory(WorkDir);
            File.WriteAllText(StatePath, JsonUtility.ToJson(state, true));
        }
    }
}
