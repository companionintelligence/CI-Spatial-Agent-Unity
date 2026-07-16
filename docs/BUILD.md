# Building for Quest 3 and Pico

The project is OpenXR-first: **one APK runs on both headsets**. The only per-headset difference is a scripting define (`XRSCAN_QUEST` / `XRSCAN_PICO`) that gates any platform-specific assembly code — the OpenXR feature groups (Meta Quest + PICO) are both enabled, and the runtime picks the right one on launch.

## One-click build

Menu **`XRScan → Build`** (from `Assets/XRScan/Editor/XRScanBuilder.cs`):

| Menu item | Output |
|---|---|
| **Quest 3 (Android APK)** | `Builds/CISpatialCompanion-Quest.apk` (define `XRSCAN_QUEST`) |
| **Pico (Android APK)** | `Builds/CISpatialCompanion-Pico.apk` (define `XRSCAN_PICO`) |
| **Configure Android settings (no build)** | applies the player settings without building |

Each item applies the correct player settings, sets the platform define, and builds the app scene (`XRScan_Main`). Deploy with:

```
adb install -r Builds/CISpatialCompanion-Quest.apk
```

For CI, call the same code headlessly:

```
Unity.exe -quit -batchmode -projectPath . -executeMethod XRScan.EditorTools.XRScanBuilder.BuildQuest
```

## What's preconfigured

`XRScanBuilder.ConfigureAndroid()` (run automatically before every build) sets:

- **Scripting backend** IL2CPP · **Architecture** ARM64
- **Graphics API** Vulkan (GLES removed)
- **Min SDK** Android 12 / API 32 (required for XR CPU camera images)
- **Color space** Linear · **Stereo rendering** Single Pass Instanced
- **App id** `com.companionintelligence.xrscan`

**XR Plug-in Management** (Project Settings): the OpenXR loader is enabled for Android with the Meta Quest and PICO feature groups.

**Permissions** are injected into the generated manifest at build time by `XRScanManifestPostProcessor` (merge-safe — it never overwrites a hand-authored manifest):

- `android.permission.RECORD_AUDIO` — microphone for speech-to-text
- `horizonos.permission.HEADSET_CAMERA` — Quest Passthrough Camera API
- `com.oculus.permission.USE_SCENE` — Meta scene understanding

## Verifying a build

The Quest APK has been validated end-to-end (IL2CPP + Gradle → ~55 MB installable APK). To check one yourself:

```
set BT=<editor>\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\36.0.0
%BT%\aapt2 dump badging     Builds\CISpatialCompanion-Quest.apk
%BT%\aapt2 dump permissions Builds\CISpatialCompanion-Quest.apk
```

A correct **Quest** build reports:

- `package: com.companionintelligence.xrscan`, `minSdkVersion:'32'`, `native-code: 'arm64-v8a'`
- `android.permission.RECORD_AUDIO` (added by the post-processor) plus `horizonos.permission.HEADSET_CAMERA` and `com.oculus.permission.USE_SCENE` — these come from `com.unity.xr.meta-openxr` itself, gated on its AR features being enabled (`XRScanBuilder` turns on `ARCameraFeature.cameraImageSupportEnabled`, which is also what makes CPU passthrough-camera images available to the scanner)
- OpenXR wiring — `org.khronos.openxr.permission.OPENXR`, and Meta entries `com.oculus.feature.PASSTHROUGH` / `com.oculus.intent.category.VR` / `com.oculus.vr.focusaware`

A correct **Pico** build has `RECORD_AUDIO` + `com.pvr.tobactivate.permission.AUTH_CHECK` and `pvr.*` / `enable_scene_anchor` manifest entries, and **must not** contain the Meta-only permissions above. Verified: the two APKs' manifests differ exactly along these lines.

Install with `adb install -r Builds\CISpatialCompanion-Quest.apk`.

### Known caveat: vendor native plugins are bundled for both headsets

The OpenXR **feature** toggle controls runtime behaviour correctly, but Unity still packages the Android **native plugins** of every installed XR package. So the Quest APK also contains PICO (`libPICO_TOBAPI.so`, `libpvrcapturelib.so`, `com.pvr.tobactivate.permission.AUTH_CHECK`), ARCore, and MockHMD libraries — inert on Quest, but ~1.5 MB of dead weight plus one spurious permission.

Harmless for development. Before a store release, strip them per-build via `PluginImporter.SetCompatibleWithPlatform` in `XRScanBuilder`, or remove the unused packages (`com.unity.xr.arcore`, `com.unity.xr.mock-hmd`) from the manifest.

## GUI Build Profiles (optional)

If you prefer Unity's **Build Profiles** window (Quest / Pico entries): create two Android profiles there and set their *Scripting Define Symbols* to `XRSCAN_QUEST` and `XRSCAN_PICO` respectively. The build menu above does the same thing and is the recommended path for repeatable / CI builds.
