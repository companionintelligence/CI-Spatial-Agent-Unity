# Building for Quest 3 and Pico

The project is OpenXR-first: one codebase, but **one APK per headset**. A Quest APK does not run on a Pico, and a Pico APK does not run on a Quest. The C# never branches per vendor; what changes between the two builds is:

- **OpenXR features.** The build enables the target headset's OpenXR features and disables the other vendor's. Quest gets the Meta features plus the Oculus Touch controller profile; Pico gets the PICO features. OpenXR validation allows only one vendor's interaction profile, so the two sets cannot be on in the same APK.
- **Scripting define.** `XRSCAN_QUEST` or `XRSCAN_PICO` (the other one is removed). It gates any platform-specific code and tells the build post-processors which headset they are packaging for.
- **Packaging.** The project is Quest-first: `Packages/manifest.json` does not contain the PICO SDK, so the project, the tests and the Quest APK never have it. The Pico build adds it for the length of the build (see [The PICO SDK is added only for the Pico build](#the-pico-sdk-is-added-only-for-the-pico-build)). Both builds leave out MCP for Unity, and each APK leaves out the other vendor's native libraries and permissions (see [What each APK contains](#what-each-apk-contains)).

## One-click build

Menu **`XRScan → Build`** (from `Assets/XRScan/Editor/XRScanBuilder.cs`):

| Menu item | Output |
|---|---|
| **Quest 3 (Android APK)** | `Builds/CISpatialCompanion-Quest.apk` (define `XRSCAN_QUEST`, Meta OpenXR features) |
| **Pico (Android APK)** | `Builds/CISpatialCompanion-Pico.apk` (define `XRSCAN_PICO`, PICO OpenXR features). On Unity 6000.6 it stops with an error instead: see [Pico builds need Unity 6000.3 for now](#pico-builds-need-unity-60003-for-now) |
| **Configure Android settings (no build)** | applies the player settings without building |
| **Dump OpenXR features** | logs every Android OpenXR feature and whether it is enabled |

Each build item applies the player settings, sets the define, switches the OpenXR features and builds the app scene (`XRScan_Main`). A build that changes the package set (every build removes MCP for Unity; the Pico build adds the PICO SDK) first makes that change, waits for the script reload, builds, and then restores `Packages/manifest.json`, `Packages/packages-lock.json` and, when the PICO SDK was added or removed, the OpenXR settings from `Library/XRScanBuild`; in the Editor the package set returns with a second script reload. Deploy and start it with:

```
adb install -r Builds/CISpatialCompanion-Quest.apk
adb shell am start -n com.companionintelligence.xrscan/com.unity3d.player.UnityPlayerGameActivity
```

The app's entry point is GameActivity (`com.unity3d.player.UnityPlayerGameActivity`), which the OpenXR Meta Quest validation requires; the APK has no `UnityPlayerActivity`.

The switch is saved into the project: the OpenXR feature set goes to `Assets/XR/Settings/OpenXRPackageSettings.asset` and the define stays in Player Settings. A Pico build puts the OpenXR settings back afterwards, together with the package set, but leaves the define at `XRSCAN_PICO`, so `ProjectSettings/ProjectSettings.asset` shows up as a local change until the next Quest build. Every menu, command-line or CI build sets both again, so their committed values do not affect those builds.

A build also changes tracked files that have nothing to do with the switch, so check `git status` after one and revert what you did not mean to change. A Quest build re-saves `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` (URP fills in its runtime settings list), and any editor session, builds and test runs included, points `.vscode/settings.json` at the local solution name. While a build runs, more files change, and a successful build puts them back: `Packages/manifest.json` and the lock file (and, in a Pico build, the OpenXR settings) for the package selection, the XR Simulation settings (AR Foundation moves `Assets/XR/Resources/XRSimulationRuntimeSettings.asset` and `Assets/XR/UserSimulationSettings/Resources/XRSimulationPreferences.asset` into the git-ignored `Assets/XR/Temp`), the XR loaders in Player Settings' preloaded assets, and the generated `Assets/Resources/PerformanceTestRun*.json` (the performance-testing package's build hook). While the PICO SDK is installed for a Pico build, its editor code writes `Assets/Resources/PXR_PlatformSetting.asset` and `PICOProjectSetting.asset`; the build deletes the ones it created when it ends. They are PICO SDK material, so never commit them (if any are left, a Quest build moves them aside while it runs). The package selection is always restored, at the next editor start if the editor was killed. After a failed or killed build, restore the rest with `git checkout` and delete the generated files in `Assets/Resources` and the folder `Assets/XR/Temp`; a leftover `Assets/XR/Temp` holds second copies of the XR Simulation assets with the same GUIDs.

Always build through `XRScanBuilder`. A plain **File → Build** (or **Build And Run**) skips the OpenXR switch and the package selection, and packages whatever feature set was saved last.

### Command line and CI

The same entry points run headlessly. `-xrscanBuildDir <dir>` (or the `XRSCAN_BUILD_DIR` environment variable) writes the APK outside the working tree instead of `Builds/`:

```
Unity.exe -batchmode -projectPath . -buildTarget Android -executeMethod XRScan.EditorTools.XRScanBuilder.BuildQuest -xrscanBuildDir D:\out
```

(`BuildPico` for the Pico APK, which on Unity 6000.6 exits with code 1 without building; see below.) Add `-aab` to build an Android App Bundle instead (`CISpatialCompanion-Quest.aab` / `CISpatialCompanion-Pico.aab`, same signing and versionCode rules), for stores and tools that take app bundles; to install one on a headset, turn it into APKs with Google's `bundletool`. Do **not** pass `-quit`: changing the packages needs a script reload that `-quit` would cut short (the builder refuses to start and exits 1 if it sees it). The builder exits by itself after restoring the project files: 0 = built; 1 = the build failed, threw, or wrote no fresh APK. If the editor is killed mid-build, the next start of the project restores the files. CI builds the Quest APK this way on every pull request and checks its contents; see [CI.md](CI.md).

### The PICO SDK is added only for the Pico build

The PICO OpenXR SDK (`com.unity.xr.openxr.picoxr`) is not in `Packages/manifest.json`. Its licence forbids reproducing its material without PICO's written permission, and Unity packs an installed package's `Resources`, plugins and settings objects into every player, so leaving it out of the project is what keeps it out of the Quest APK, the test runs and the Editor. **XRScan → Build → Pico** (`XRScanBuilder.BuildPico`) installs it from `XRScanBuildPackages.PicoPackageSource`, PICO's repository at the pinned commit `3aa3e62` (tag `release_1.4.0`, package 1.4.1), builds once the scripts have reloaded, and then restores the manifest, the lock file and the OpenXR settings, so the SDK leaves the project again (in batch mode at the next editor start, in the Editor straight away). Fetching the package needs access to GitHub. To build against another PICO release, change `PicoPackageSource` and update [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md). If you add the SDK to the manifest yourself, the Pico build uses that copy and the Quest build takes it out of the project for its build, as before.

### Pico builds need Unity 6000.3 for now

The project is on Unity 6000.6.9f1, and the pinned PICO OpenXR SDK (1.4.x) does not compile on Unity 6000.6: two of its Enterprise samples call `Object.GetInstanceID()`, which 6000.6 turns into a compile error (`CS0619`). PICO has not released a version that compiles on 6000.6, and its licence does not allow committing a patched copy. So on 6000.6 **XRScan → Build → Pico** (`XRScanBuilder.BuildPico`) stops before it changes anything and logs why; in batch mode the editor exits with code 1. The Quest build, the tests and CI are not affected, since none of them installs the PICO SDK.

Until PICO ships a compatible SDK, build Pico APKs with Unity 6000.3.19f1 from the project as it was before its upgrade to 6000.6.9f1, i.e. the parent of the commit "Upgrade project to Unity 6000.6.9f1" (`37e1d47` on the branch that made the upgrade). There the Pico build adds the SDK as described above; it was verified to build a Pico APK whose manifest matches the earlier ones. When PICO releases an SDK that compiles on 6000.6, point `XRScanBuildPackages.PicoPackageSource` at it, raise `XRScanBuildPackages.PicoSdkFailsFrom` (the first editor version the pinned SDK fails on), update [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md), and check the Pico APK as described in [Verifying a build](#verifying-a-build).

## Release signing and version code

`XRScanBuilder` never stores signing material in the project. It reads it from the environment of the editor process for each build (`AndroidReleaseSettings` in `Assets/XRScan/Settings/Editor`):

| Variable | Used for |
|---|---|
| `KEYSTORE_PATH` | path of the release keystore (`.jks` / `.keystore`) |
| `KEYSTORE_PASS` | keystore password |
| `KEY_ALIAS` | alias of the release key |
| `KEY_PASS` | password of that key |
| `VERSION_CODE` | optional Android versionCode, a whole number from 1 to 2100000000 |

- **Signing.** When all four signing variables are set, the APK is signed with that key; otherwise with Unity's debug key, which is fine for sideloading but not for a store upload. If only some of them are set, the build warns and uses the debug key.
- **`-release`.** Add it to the command line of a store build. The builder then exits with code 1, before it changes anything in the project, when any of the four variables is missing or `KEYSTORE_PATH` is not a file, instead of falling back to the debug key.
- **versionCode.** `VERSION_CODE` when set, otherwise `MAJOR*1000000 + MINOR*1000 + PATCH` of Player Settings → Version (`MAJOR.MINOR.PATCH`, optionally followed by a `-suffix`, with MINOR and PATCH below 1000), so `0.1.0` gives 1000. A version that does not fit fails the build. Each store upload needs a higher versionCode than the last.
- The builder applies these for the build only and puts the previous Player Settings back afterwards, byte for byte, so no keystore path, alias or versionCode stays in `ProjectSettings.asset`; the passwords are never written there. While a release build runs the file does hold the keystore path and alias, so revert it if such a build is killed. Unity also writes the signing values into the Gradle project it generates under `Library/Bee`, so treat that `Library` as sensitive after a release build on a shared machine.
- Create the keystore yourself (for example with the JDK's `keytool -genkeypair`), back it up, and keep it and its passwords out of the repository: a store app can only be updated with the key it was first signed with.

```
Unity.exe -batchmode -projectPath . -buildTarget Android -executeMethod XRScan.EditorTools.XRScanBuilder.BuildQuest -release
```

Set the four variables in that process's environment from a password manager or CI secrets rather than typing the passwords on a command line. In CI, decode the keystore from a secret into the runner's temporary directory and pass the variables to the build step only. The Quest APK workflow does neither today, so CI's APKs are debug-signed.

## What's preconfigured

`XRScanBuilder.ConfigureAndroid()` (run automatically before every build) sets:

- **Scripting backend** IL2CPP · **Architecture** ARM64
- **Graphics API** Vulkan (GLES removed)
- **Min SDK** Android 12 / API 32 (required for XR CPU camera images)
- **Target SDK** API 34. The Meta Horizon Store requires exactly 34 for apps created since March 2026, so the target is pinned instead of *Automatic* (which would pick the highest installed platform; the 6000.6.9f1 editor ships the API 34, 36 and 37 platforms). The Pico APK uses the same value.
- **Color space** Linear · **Stereo rendering** Single Pass Instanced

From Player Settings: **App id** `com.companionintelligence.xrscan` and **Version** `0.1.0`. No keystore is configured there: signing and the versionCode come from the environment at build time (see [Release signing and version code](#release-signing-and-version-code)).

**XR Plug-in Management** (Project Settings): Android uses the OpenXR loader only. The Meta OpenXR feature group is installed; the PICO one only while a Pico build runs. The build turns on the target headset's group as described above. For Quest builds, `XRScanBuilder` also enables two options of the Meta AR camera feature (**Meta Quest: Camera (Passthrough)**): CPU camera images (`cameraImageSupportEnabled`), which the scanner needs, and **Passthrough Pre Splash Screen** (`passthroughPreSplashScreen`), so the app starts in passthrough instead of the headset's opaque loading screen. The committed OpenXR settings have both on.

**Permissions:** `XRScanManifestPostProcessor` adds `android.permission.RECORD_AUDIO` (microphone for speech-to-text) to the generated manifest. It is merge-safe and never overwrites a hand-authored manifest. It deliberately does not add the Meta permissions. `com.unity.xr.meta-openxr` injects `horizonos.permission.HEADSET_CAMERA` (Passthrough Camera API) and `com.oculus.permission.USE_SCENE` (scene understanding) itself, gated on its AR features, so they only appear in Quest builds. On every build except Pico the post-processor also removes the PICO permission `com.pvr.tobactivate.permission.AUTH_CHECK`, which PICO's `tob_api` library merges in.

## What each APK contains

Unity packs every installed package's runtime assemblies, native and Java plugins, `Resources` folders and the settings objects it owns into every player, whatever loader or feature is active, and nothing inside a read-only package can be excluded piecemeal. So the builder first decides which packages are in the project for the build (`XRScanBuildPackages`); build callbacks in `Assets/XRScan/Editor`, keyed off the platform define, then trim the rest:

| Step | Quest APK (`XRSCAN_QUEST`, and any non-Pico build) | Pico APK (`XRSCAN_PICO`) |
|---|---|---|
| `XRScanBuildPackages` (package selection, before the build) | removes MCP for Unity (`com.coplaydev.unity-mcp`) from the project. The PICO SDK (`com.unity.xr.openxr.picoxr`) is not in it; if it was added to the manifest, it is removed too. Moves PICO-generated `Assets/Resources/PXR_PlatformSetting.asset`, `PICOProjectSetting.asset` and `PicoSdkPCConfig.json` aside if any are left | adds the PICO SDK from the pinned commit and removes MCP for Unity |
| `XRScanVendorPackageGuard` (`IPreprocessBuildWithReport`) | fails the build while the PICO SDK or a PICO-generated file is in the project, e.g. a build started from the Build Profiles window while the SDK is installed | — |
| `XRScanPlayerAssemblyFilter` (`IFilterBuildAssemblies`) | second line of defence: drops PICO and MCP for Unity assemblies if they are present | drops MCP for Unity's `MCPForUnity.Runtime` if present |
| `XRScanNativeLibExcluder` (Gradle post-process) | excludes PICO's `.so` (`libPICO_TOBAPI`, `libpvrcapturelib`, `libpxrplatformloader`, `libCameraRenderingPlugin`, `libopenxr_pico`) and `libUnityMockHMD.so`; deletes PICO's `.aar` / `.jar` plugins if present | excludes Meta's `libUnityARFoundationMeta.so` and `libUnityMockHMD.so` |
| `XRScanManifestPostProcessor` | adds `RECORD_AUDIO`; removes PICO's `com.pvr.tobactivate.permission.AUTH_CHECK` | adds `RECORD_AUDIO` |

`libopenxr_loader.so` is the vendor-neutral Khronos loader, so both APKs keep it. Libraries not listed here are packaged as usual.

Why: the PICO SDK's licence forbids reproducing its material without PICO's written permission, and MCP for Unity is edit-time tooling. Filtering assemblies, plugins and permissions is not enough on its own: an earlier Quest APK built that way still carried the 108 files of PICO's `Resources` folder (controller and hand models, textures, shaders), its OpenXR feature objects and PICO script names. See [`THIRD_PARTY_NOTICES.md`](../THIRD_PARTY_NOTICES.md) for what remains in each APK and under which licence. **XRScan → Build → Dump excluded player assemblies** lists the assemblies the filter would drop.

To check an APK, scan every file in it, serialized assets included:

```
python tools/apk/scan_apk.py CISpatialCompanion-Quest.apk --target quest   # exit 0 = no PICO, MCP for Unity, Ready Player Me or Visual Scripting content
python tools/apk/scan_apk.py CISpatialCompanion-Pico.apk --target pico     # PICO content expected; the rest must be absent
```

and read the build report at the end of the build's Editor log ("Used Assets and files from the Resources folder"): for Quest no `Packages/com.unity.xr.openxr.picoxr/` path may appear. Listing assemblies (`assets/bin/Data/ScriptingAssemblies.json`) or files (`unzip -l`) only shows code and native libraries, not assets.

Unity's Gradle build also adds libraries that no Unity package lists: with the GameActivity entry point, AndroidX (AppCompat, Core, ConstraintLayout and their dependencies), the GameActivity library, Kotlin and kotlinx.coroutines, plus `libgame.so` and the NDK's `libc++_shared.so`. [`THIRD_PARTY_NOTICES.md`](../THIRD_PARTY_NOTICES.md#13-android-libraries-added-by-the-gradle-build) lists them; to check that it names everything an APK or App Bundle packages, run

```
python tools/apk/check_notices.py CISpatialCompanion-Quest.apk   # exit 0 = every Maven library (with its version), native library and Java package is named
python tools/apk/check_notices.py CISpatialCompanion-Pico.apk
```

It fails on a `META-INF/*.version` artifact the notices do not name with that version, a native library they do not name, or a class in `classes.dex` outside the package patterns they list. Update the notices whenever it fails.

## Verifying a build

To check an APK, use `aapt2` from the editor's Android SDK (`aapt` v1 prints nothing for modern APKs):

```
set BT=<editor>\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\36.0.0
%BT%\aapt2 dump badging     Builds\CISpatialCompanion-Quest.apk
%BT%\aapt2 dump permissions Builds\CISpatialCompanion-Quest.apk
```

A correct **Quest** build reports:

- `package: com.companionintelligence.xrscan`, `minSdkVersion:'32'`, `targetSdkVersion:'34'`, `native-code: 'arm64-v8a'`
- `android.permission.RECORD_AUDIO` (added by the post-processor) plus `horizonos.permission.HEADSET_CAMERA` and `com.oculus.permission.USE_SCENE` (from `com.unity.xr.meta-openxr`, because `XRScanBuilder` turns on `ARCameraFeature.cameraImageSupportEnabled`, which is also what makes CPU passthrough-camera images available to the scanner)
- OpenXR wiring: `org.khronos.openxr.permission.OPENXR`, and the Meta entries `com.oculus.feature.PASSTHROUGH` / `com.oculus.intent.category.VR` / `com.oculus.vr.focusaware`
- the application meta-data `com.oculus.ossplash.background` = `passthrough-contextual` (passthrough before the splash screen). `badging` does not show meta-data; read it with `%BT%\aapt2 dump xmltree --file AndroidManifest.xml Builds\CISpatialCompanion-Quest.apk`
- no `com.pvr.tobactivate.permission.AUTH_CHECK`, and no PICO or Mock HMD libraries under `lib/arm64-v8a/`
- none of `com.oculus.permission.EYE_TRACKING` (Meta's Social Eye Gaze feature, which `XRScanBuilder` keeps off), `com.oculus.permission.BOUNDARY_VISIBILITY`, `com.oculus.permission.IMPORT_EXPORT_IOT_MAP_DATA` or `com.oculus.permission.USE_COLOCATION_DISCOVERY_API` (the unused Meta features `XRScanBuilder` keeps off)

A correct **Pico** build has `RECORD_AUDIO` + `com.pvr.tobactivate.permission.AUTH_CHECK` and `pvr.*` / `enable_scene_anchor` manifest entries, and **must not** contain the Meta-only permissions above. Verified: the two APKs' manifests differ exactly along these lines.

CI runs the Quest checks on every build it makes ([CI.md](CI.md#quest-apk-checks)). If you change what a Quest build should contain, update both lists.

## Build Profiles

Unity's **Build Profiles** window can set the define per profile, but it does not run `XRScanBuilder`: a profile build does not switch the OpenXR features (it packages whatever feature set was saved last) and cannot add or leave out packages. A Pico profile therefore has no PICO SDK to build with, and a Quest profile (`XRSCAN_QUEST`) fails in `XRScanVendorPackageGuard` if the PICO SDK has been installed by hand. Use the **XRScan → Build** menu or the command line for anything you install.
