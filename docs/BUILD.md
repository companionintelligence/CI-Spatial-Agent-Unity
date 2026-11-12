# Building for Quest 3 and Pico

The project is OpenXR-first: one codebase, but **one APK per headset**. A Quest APK does not run on a Pico, and a Pico APK does not run on a Quest. The C# never branches per vendor; what changes between the two builds is:

- **OpenXR features.** The build enables the target headset's OpenXR features and disables the other vendor's. Quest gets the Meta features plus the Oculus Touch controller profile; Pico gets the PICO features. OpenXR validation allows only one vendor's interaction profile, so the two sets cannot be on in the same APK.
- **Scripting define.** `XRSCAN_QUEST` or `XRSCAN_PICO` (the other one is removed). It gates any platform-specific code and tells the build post-processors which headset they are packaging for.
- **Packaging.** Each APK is built from a project without the vendor packages it must not contain (Quest: the PICO SDK; both: MCP for Unity), and leaves out the other vendor's native libraries and permissions (see [What each APK contains](#what-each-apk-contains)).

## One-click build

Menu **`XRScan → Build`** (from `Assets/XRScan/Editor/XRScanBuilder.cs`):

| Menu item | Output |
|---|---|
| **Quest 3 (Android APK)** | `Builds/CISpatialCompanion-Quest.apk` (define `XRSCAN_QUEST`, Meta OpenXR features) |
| **Pico (Android APK)** | `Builds/CISpatialCompanion-Pico.apk` (define `XRSCAN_PICO`, PICO OpenXR features) |
| **Configure Android settings (no build)** | applies the player settings without building |
| **Dump OpenXR features** | logs every Android OpenXR feature and whether it is enabled |

Each build item applies the player settings, sets the define, switches the OpenXR features and builds the app scene (`XRScan_Main`). A build that has packages to leave out (every build: MCP for Unity; Quest: also the PICO SDK) first removes them from the project, waits for the script reload, builds, and then restores `Packages/manifest.json`, `Packages/packages-lock.json`, the OpenXR settings and any PICO-generated files from `Library/XRScanBuild`; in the Editor the packages come back with a second script reload. Deploy with:

```
adb install -r Builds/CISpatialCompanion-Quest.apk
```

The switch is saved into the project: the OpenXR feature set goes to `Assets/XR/Settings/OpenXRPackageSettings.asset` and the define stays in Player Settings. After building for the other headset, those settings show up as local changes. Every menu, command-line or CI build sets them again, so their committed values do not affect those builds.

Always build through `XRScanBuilder`. A plain **File → Build** (or **Build And Run**) skips the OpenXR switch and the package selection, and packages whatever feature set was saved last.

### Command line and CI

The same entry points run headlessly. `-xrscanBuildDir <dir>` (or the `XRSCAN_BUILD_DIR` environment variable) writes the APK outside the working tree instead of `Builds/`:

```
Unity.exe -batchmode -projectPath . -buildTarget Android -executeMethod XRScan.EditorTools.XRScanBuilder.BuildQuest -xrscanBuildDir D:\out
```

(`BuildPico` for the Pico APK.) Do **not** pass `-quit`: removing the packages needs a script reload that `-quit` would cut short (the builder refuses to start and exits 1 if it sees it). The builder exits by itself after restoring the project files: 0 = built; 1 = the build failed, threw, or wrote no fresh APK. If the editor is killed mid-build, the next start of the project restores the files. CI builds the Quest APK this way on every pull request and checks its contents; see [CI.md](CI.md).

## What's preconfigured

`XRScanBuilder.ConfigureAndroid()` (run automatically before every build) sets:

- **Scripting backend** IL2CPP · **Architecture** ARM64
- **Graphics API** Vulkan (GLES removed)
- **Min SDK** Android 12 / API 32 (required for XR CPU camera images)
- **Color space** Linear · **Stereo rendering** Single Pass Instanced

From Player Settings: **App id** `com.companionintelligence.xrscan`, and no custom keystore, so APKs are signed with Unity's debug key. That is fine for sideloading but not for a store upload.

**XR Plug-in Management** (Project Settings): Android uses the OpenXR loader only. Both vendors' OpenXR feature groups are installed, and the build turns on one of them as described above. For Quest builds, `XRScanBuilder` also enables CPU camera images on the Meta AR camera feature (`ARCameraFeature.cameraImageSupportEnabled`), which the scanner needs.

**Permissions:** `XRScanManifestPostProcessor` adds `android.permission.RECORD_AUDIO` (microphone for speech-to-text) to the generated manifest. It is merge-safe and never overwrites a hand-authored manifest. It deliberately does not add the Meta permissions. `com.unity.xr.meta-openxr` injects `horizonos.permission.HEADSET_CAMERA` (Passthrough Camera API) and `com.oculus.permission.USE_SCENE` (scene understanding) itself, gated on its AR features, so they only appear in Quest builds. On every build except Pico the post-processor also removes the PICO permission `com.pvr.tobactivate.permission.AUTH_CHECK`, which PICO's `tob_api` library merges in.

## What each APK contains

Unity packs every installed package's runtime assemblies, native and Java plugins, `Resources` folders and the settings objects it owns into every player, whatever loader or feature is active, and nothing inside a read-only package can be excluded piecemeal. So the builder first decides which packages are in the project for the build (`XRScanBuildPackages`); build callbacks in `Assets/XRScan/Editor`, keyed off the platform define, then trim the rest:

| Step | Quest APK (`XRSCAN_QUEST`, and any non-Pico build) | Pico APK (`XRSCAN_PICO`) |
|---|---|---|
| `XRScanBuildPackages` (package selection, before the build) | removes the PICO SDK (`com.unity.xr.openxr.picoxr`) and MCP for Unity (`com.coplaydev.unity-mcp`) from the project, and moves PICO-generated `Assets/Resources/PXR_PlatformSetting.asset`, `PICOProjectSetting.asset` and `PicoSdkPCConfig.json` aside | removes MCP for Unity |
| `XRScanVendorPackageGuard` (`IPreprocessBuildWithReport`) | fails the build while the PICO SDK or a PICO-generated file is still in the project, e.g. a build started from the Build Profiles window | — |
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

## Verifying a build

To check an APK, use `aapt2` from the editor's Android SDK (`aapt` v1 prints nothing for modern APKs):

```
set BT=<editor>\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\36.0.0
%BT%\aapt2 dump badging     Builds\CISpatialCompanion-Quest.apk
%BT%\aapt2 dump permissions Builds\CISpatialCompanion-Quest.apk
```

A correct **Quest** build reports:

- `package: com.companionintelligence.xrscan`, `minSdkVersion:'32'`, `native-code: 'arm64-v8a'`
- `android.permission.RECORD_AUDIO` (added by the post-processor) plus `horizonos.permission.HEADSET_CAMERA` and `com.oculus.permission.USE_SCENE` (from `com.unity.xr.meta-openxr`, because `XRScanBuilder` turns on `ARCameraFeature.cameraImageSupportEnabled`, which is also what makes CPU passthrough-camera images available to the scanner)
- OpenXR wiring: `org.khronos.openxr.permission.OPENXR`, and the Meta entries `com.oculus.feature.PASSTHROUGH` / `com.oculus.intent.category.VR` / `com.oculus.vr.focusaware`
- no `com.pvr.tobactivate.permission.AUTH_CHECK`, and no PICO or Mock HMD libraries under `lib/arm64-v8a/`

A correct **Pico** build has `RECORD_AUDIO` + `com.pvr.tobactivate.permission.AUTH_CHECK` and `pvr.*` / `enable_scene_anchor` manifest entries, and **must not** contain the Meta-only permissions above. Verified: the two APKs' manifests differ exactly along these lines.

CI runs the Quest checks on every build it makes ([CI.md](CI.md#quest-apk-checks)). If you change what a Quest build should contain, update both lists.

## Build Profiles

Unity's **Build Profiles** window can set the define per profile, but it does not run `XRScanBuilder`: a profile build does not switch the OpenXR features (it packages whatever feature set was saved last) and cannot leave packages out. A Quest profile (`XRSCAN_QUEST`) therefore fails in `XRScanVendorPackageGuard` while the PICO SDK is installed. Use the **XRScan → Build** menu or the command line for anything you install.
