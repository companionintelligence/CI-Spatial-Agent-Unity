# Handoff — MR scene rig (passthrough + XR origin + avatar placement)

_Written 2026-07-18 after the first on-device run on a Quest 3. Read this first, then `docs/ARCHITECTURE.md` and `docs/BUILD.md`._

## TL;DR — your task

The app **builds, installs, launches, and runs on Quest 3** (validated — see below). But in the headset it renders an **opaque dark skybox with no visible avatar**, because `XRScan_Main.unity` is a plain editor-demo camera, not a mixed-reality rig. **Turn it into a real MR scene:** transparent passthrough camera + an XR Origin with floor tracking + the avatar placed in front of the user, and wire controller input so the user can actually trigger a scan. Then rebuild → `adb install` → verify in the headset.

## What is already true (don't re-do this)

- **Unity 6.3 LTS** (`6000.3.19f1`) project at `C:\Users\hegem\XRScan`. Canonical repo: **companionintelligence/CI-Spatial-Companion-Unity**, PR **#14**, branch `feat/unity6.3-xr-companion-rebuild` (7 commits, mergeable). Working copy is developed in `C:\Users\hegem\XRScan` and synced into the repo clone at `C:\devel\CI-Spatial-Companion-Unity` before committing.
- **Architecture:** OpenXR-first, one APK per headset. 12 module assemblies under `Assets/XRScan/` behind clean interfaces (see `docs/ARCHITECTURE.md`, which has mermaid diagrams).
- **Compiles green.** 16 automated tests pass **headless** (13 EditMode + 3 PlayMode scanning-pipeline). Run: `Unity.exe -batchmode -projectPath C:\Users\hegem\XRScan -runTests -testPlatform EditMode -testResults <xml> -logFile <log>` (and `PlayMode`).
- **Both APKs build one-click** and were verified with `aapt2` (correct package, arm64, minSDK 32, per-headset OpenXR + permissions). Build: menu `XRScan > Build > Quest 3 / Pico`, or headless `-executeMethod XRScan.EditorTools.XRScanBuilder.BuildQuest -buildTarget Android`.
- **Quest 3 device-validated (2026-07-18):** installed, launched, ran in stereo at ~90 FPS, controllers tracked. On-device log proved the cross-platform capability detection works on real hardware:
  `[AppBootstrap] Vendor=MetaQuest RGB=True SceneMesh=True` (in the Editor it can only ever report `Desktop`). App is currently installed on the device.

## The problem — root cause (confirmed by inspecting the scene)

`Assets/XRScan/App/Scenes/XRScan_Main.unity` was built via `execute_code` for the **Editor mock demo**, not for XR. Confirmed:

- Main Camera `m_ClearFlags: 2` (SolidColor) with `m_BackGroundColor: {r:0.06,g:0.07,b:0.1,a:1}` → **opaque** dark-blue fill. That is the "skybox" the user sees; it paints over passthrough.
- **No** `XROrigin`, `TrackedPoseDriver`, `ARCameraManager`, `ARCameraBackground`, or `ARSession` anywhere in the scene. So there is no AR passthrough rig and no floor-relative tracking origin. The avatar sits at world `(0, 0, 2.2)`, but without an XR Origin the user's tracking space doesn't align to scene origin, so the avatar is out of view.

## The fix — plan

1. **Rebuild the scene as an MR rig** (do it in an editor script / via Unity MCP so it's reproducible, the way `XRScanBuilder` is — don't hand-edit YAML):
   - Add **XR Origin (AR)**: `GameObject > XR > XR Origin (AR)` → creates XR Origin + Camera Offset + an AR Camera with `TrackedPoseDriver`, `ARCameraManager`, `ARCameraBackground`. Add an **ARSession** object. Set the XR Origin **Tracking Origin Mode = Floor**.
   - Set the AR camera `clearFlags = SolidColor`, `backgroundColor = (0,0,0,0)` (**alpha 0**) so Meta passthrough shows through. `com.unity.xr.meta-openxr` renders passthrough via the transparent camera + the environment blend mode; `ARFoundationCameraProvider` already targets `ARCameraManager`.
   - Attach `XRScan.Platform.ARFoundationCameraProvider` to the AR camera and point `AppBootstrap.arCamera` at it.
2. **Place the avatar** ~1.5–2 m in front of the XR Origin at floor level (`y≈0`), rotated 180° to face the camera. Keep the `AvatarLoadController` / `GazeController` / `AvatarLipSync` components (all working — `CI_Brad.glb` has the 15 Oculus visemes).
3. **Wire controller input (currently MISSING — the user cannot interact even when things are visible).** Add an XRI Input Actions asset or a small `InputSystem` handler that calls `AppBootstrap.Scan()` on a controller trigger and `AppBootstrap.ToggleListen()` on grip. The Oculus Touch interaction profile is already enabled for Quest builds.
4. **Rebuild + redeploy + verify** (loop below). Confirm in-headset: real room visible (passthrough), avatar standing in front, trigger → a pin appears + avatar looks & speaks.

Note: `AppBootstrap.useMocks` is currently `true`, so even after this the scan uses `MockObjectDetector` / `MockTts` (no real CV/voice). That's fine for validating the MR rig. Real backends are a separate task (below).

## Environment & commands

- **Editor:** `C:\Users\hegem\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe`
- **Unity MCP** (`mcp__UnityMCP__*`): the bridge **disconnects when the Editor is closed**. Open the Editor on the project and it auto-reconnects (`com.coplaydev.unity-mcp` is in the manifest, bridge on port ~6400). Then use `manage_scene` / `manage_gameobject` / `manage_components` / `execute_code` / `read_console` / `run_tests`.
  - `execute_code` runs the **CodeDom C# 6** compiler: no local functions, no `out var` — use `System.Action<>` lambdas and fully-qualified type names. Blocked APIs (e.g. `AssetDatabase.DeleteAsset`) need `safety_checks:false`.
- **adb:** `C:\Users\hegem\Unity\Hub\Editor\6000.3.19f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe`
  - Device: **Quest 3**, serial `2G0YC1ZF8G02NG` (`model Quest_3`, `device eureka`), USB, authorized.
  - Install: `adb install -r -g Builds\CISpatialCompanion-Quest.apk`
  - Launch: `adb shell am start -n com.companionintelligence.xrscan/com.unity3d.player.UnityPlayerActivity`
  - **Gotcha:** Quest gates launch behind a **"Controllers Required" system dialog** (`REQUIRES_CONTROLLERS_LAUNCH_CHECK`) — the app process won't start until the user wakes the Touch controllers (press a button) or dismisses it. The app has no hand-tracking feature, hence the gate.
  - Logs: `adb logcat -s Unity:*` (our `Debug.Log` shows as `Unity :`). APK inspect: use **aapt2** at `<SDK>/build-tools/36.0.0/` (`aapt` v1 returns nothing on modern APKs). Headset frame: `adb exec-out screencap -p > f.png`.

## Gotchas learned this session (save yourself the pain)

- **Validate tests in BATCHMODE, not just the Editor.** PlayMode passed in-editor but failed 2/3 headless because a freshly-created collider isn't in the physics scene after one frame — fixed with `Physics.SyncTransforms()` + `yield return new WaitForFixedUpdate()`. CI runs batchmode.
- **A freshly `Write`-created `.cs` can be orphaned** (assigned to no assembly, no compile error, silently not discovered by the test runner). Fix: `AssetDatabase.DeleteAsset` (safety_checks:false) + recreate under a *new* filename + `CompilationPipeline.RequestScriptCompilation`. Verify via reflection over the assembly's `GetTypes()`.
- **OpenXR features must be toggled per-headset before the build** (`XRScanBuilder.ConfigureOpenXRFeatures`): Quest and Pico can't both be enabled (OpenXR allows one vendor interaction profile). Quest = Meta + OculusTouch profile ON, PICO + deprecated OculusQuest OFF; Pico = inverse. The `XRScan > Build > Dump OpenXR features` menu lists all features + states.
- **Let `com.unity.xr.meta-openxr` own the Meta permissions.** It injects `HEADSET_CAMERA`/`USE_SCENE` gated on its AR features; don't add them in `XRScanManifestPostProcessor` (that leaked them into the Pico APK). `XRScanBuilder` sets `ARCameraFeature.cameraImageSupportEnabled=true` for Quest — required for CPU passthrough-camera images.
- **The editor's Android OpenJDK module can be missing** even with SDK/NDK present → "Failed to update Android SDK package list / JAVA_HOME invalid". Install: `"Unity Hub.exe" -- --headless install-modules --version 6000.3.19f1 -m "android-open-jdk-17.0.18+8"` (exact versioned id).
- The two `AppBootstrap.cs` / manifest edits flagged as "modified by user/linter" are intentional — **don't revert them.**

## Key files

- Scene: `Assets/XRScan/App/Scenes/XRScan_Main.unity` ← the thing to rebuild
- Startup/wiring: `Assets/XRScan/App/AppBootstrap.cs`, `ScanController.cs`
- Platform: `Assets/XRScan/Platform/` — `ARFoundationCameraProvider`, `PlatformCapabilities` (device-detects vendor), `CameraProviderFactory`
- Avatar: `Assets/XRScan/Avatar/` — `AvatarLoadController`, `GazeController`, `AvatarLipSync` (uLipSync 15-viseme), `Models/CI_Brad.glb`
- Build: `Assets/XRScan/Editor/XRScanBuilder.cs`, `XRScanManifestPostProcessor.cs`
- Docs: `docs/ARCHITECTURE.md`, `docs/BUILD.md`

## Backlog beyond the MR rig

- **Controller input binding** (blocks all interaction on device) — do this with the rig.
- **Real backends:** `useMocks=false` + a reachable Ollama host (`LlmConfig`), CV server URL (`ServerConfig`), and an RPM avatar URL. All are config-only (PlayerPrefs/env, never committed).
- On-device **Sentis** YOLO / Whisper / **Piper** are scaffolds needing `.onnx` models.
- **Strip vendor native plugins** before any store release: each APK bundles the other vendor's + ARCore/MockHMD `.so` (~1.5 MB inert, plus a spurious `com.pvr.tobactivate...AUTH_CHECK` on Quest). Use `PluginImporter.SetCompatibleWithPlatform` per build, or drop `com.unity.xr.arcore` / `com.unity.xr.mock-hmd` from the manifest.
