# Continuous integration

GitHub Actions runs the Unity tests and builds the Meta Quest 3 APK headlessly, in the editor version the project is pinned to. The workflows live in `.github/workflows/`:

| Workflow | Runs on | What it does |
|---|---|---|
| `ci.yml` (**CI**) | pushes to `main`, every pull request, manual | Calls the two workflows below: the tests first, then the Quest APK once both test suites pass. Changes that only touch Markdown, `docs/`, `static/`, `LICENSE*` or `.vscode/` do not start it. |
| `unity-tests.yml` (**Unity tests**) | called by CI, or manual | EditMode and PlayMode tests, as two parallel jobs. |
| `quest-apk.yml` (**Quest APK**) | called by CI, or manual | Builds `CISpatialCompanion-Quest.apk` through `XRScanBuilder.BuildQuest`, checks what was packaged, uploads the APK. |

To run CI on a branch that has no pull request, open **Actions → CI → Run workflow** and pick the branch. The two inner workflows can be started the same way on their own.

A newer push to the same branch or pull request queues behind a run that has already started instead of cancelling it, because cancelling a Unity job part-way can skip returning its licence activation.

The Pico APK is not built in CI. `XRScanBuilder.BuildPico` would slot in as a second build job the same way.

## Licence secrets

Unity needs an activated licence even in batch mode. The workflows read it from repository secrets (**Settings → Secrets and variables → Actions → New repository secret**) and never print them:

| Secret | Unity Personal | Unity Plus / Pro |
|---|---|---|
| `UNITY_EMAIL` | required: e-mail of the Unity ID that holds the licence | required |
| `UNITY_PASSWORD` | required: password of that Unity ID | required |
| `UNITY_LICENSE` | required: the full contents of `Unity_lic.ulf` | leave unset |
| `UNITY_SERIAL` | leave unset | required: the serial key |

For a Personal licence, sign in to Unity Hub on any machine and activate a Personal licence, then copy the licence file it writes:

- Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
- macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
- Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`

With the GitHub CLI, from a shell outside the repository:

```
gh secret set UNITY_LICENSE --repo companionintelligence/CI-Spatial-Companion-Unity < Unity_lic.ulf
gh secret set UNITY_EMAIL --repo companionintelligence/CI-Spatial-Companion-Unity
gh secret set UNITY_PASSWORD --repo companionintelligence/CI-Spatial-Companion-Unity
```

(`gh secret set` without `<` prompts for the value, so it never lands in your shell history.) Never commit a licence file.

Each workflow starts with a small **Setup** job that checks the secrets before any Unity job is queued:

- On a push or a pull request from a branch of this repository, missing secrets fail Setup with an error that names them.
- Pull requests from forks and from Dependabot never receive repository secrets. For those, the Unity jobs are skipped with a notice instead of failing. A maintainer can run the workflow from a branch of this repository.

## Unity version

Every run reads `m_EditorVersion` from `ProjectSettings/ProjectVersion.txt` and runs in the matching [game-ci](https://game.ci) Docker image:

- tests: `unityci/editor:ubuntu-<version>-linux-il2cpp-3`
- Quest APK: `unityci/editor:ubuntu-<version>-android-3`

Upgrading the editor therefore needs no workflow change, as long as game-ci publishes images for the new version. Check the `unityci/editor` tags on Docker Hub before upgrading. The version is also part of the cache keys, so the first run after an upgrade starts from a cold `Library/`.

## Library cache

Each Unity job restores `Library/` from the Actions cache before it opens the project and saves it again after a successful run.

- Key: `Library-Linux-<tests|android>-<editor version>-<hash of Assets/, Packages/ and ProjectSettings/>`.
- When nothing matches exactly, the newest cache with the same platform and editor version is restored, so a run after a script change re-imports only what changed.
- The tests and the APK use separate caches (desktop Linux versus Android target). The EditMode and PlayMode jobs import the same project, so only the EditMode job saves the tests cache.
- A failed run never saves, so a broken import is not cached.
- To start clean, delete the caches under **Actions → Caches**, or run `gh cache delete --all --repo companionintelligence/CI-Spatial-Companion-Unity`.

The Unity images are large, so every Unity job first deletes preinstalled SDKs that the runner image ships and these jobs never use (the Unity image brings its own Android SDK, NDK and JDK).

## Artifacts

| Artifact | Uploaded by | Contents |
|---|---|---|
| `test-results-editmode` | EditMode job | `editmode-results.xml` (NUnit 3) and `editmode.log` (editor log) |
| `test-results-playmode` | PlayMode job | `playmode-results.xml` and `playmode.log` |
| `CISpatialCompanion-Quest.apk` | Quest APK job | The APK itself, not zipped. |

Artifacts are kept for 14 days. Each job also writes a run summary: pass, fail and skip counts with the failing tests, or the APK's size, SHA-256 and manifest highlights.

The workflow passes no release keystore (see [Release signing](BUILD.md#release-signing-and-version-code)), so the APK is signed with Unity's debug key. Sideload it with `adb install -r CISpatialCompanion-Quest.apk`. It is not suitable for a store upload.

## Quest APK checks

The build step runs the editor without `-quit` (game-ci's `manualExit: true`): `XRScanBuilder` takes the PICO SDK and MCP for Unity out of the project before the Quest build, which needs a script reload that `-quit` would cut short, and exits by itself once the build has ended and the project files are restored. It exits with code 1, failing the step, when the build fails, throws or writes no APK. The `Verify the APK` step then checks what was packaged and fails when any of these is not true:

- `Builds/CISpatialCompanion-Quest.apk` exists and is not empty.
- `tools/apk/scan_apk.py --target quest` finds no PICO SDK, MCP for Unity, Ready Player Me or Visual Scripting content in any file of the APK, serialized assets included.
- `tools/apk/check_notices.py` finds every library the APK packages named in `THIRD_PARTY_NOTICES.md`: each Maven artifact that left a `META-INF/*.version` file, with that version, each native library, and each Java package in `classes.dex`. Unity's Gradle build adds libraries (AndroidX, Kotlin, kotlinx.coroutines, GameActivity, the NDK's `libc++_shared.so`) that `Packages/packages-lock.json`, and so `ThirdPartyNoticesTests`, never sees; when a dependency change adds one, this check fails until the notices list it.
- Native libraries are ARM64 only. `libil2cpp.so`, `libunity.so` and `libopenxr_loader.so` are present, and none of the PICO or Mock HMD libraries that `XRScanNativeLibExcluder` strips from Quest builds are.
- The manifest, read with `aapt2` from the Android SDK inside the Unity image, has package `com.companionintelligence.xrscan`, minimum SDK 32, target SDK 34, native code `arm64-v8a` only, the permissions `android.permission.RECORD_AUDIO`, `horizonos.permission.HEADSET_CAMERA` and `com.oculus.permission.USE_SCENE`, and none of the PICO permission `com.pvr.tobactivate.permission.AUTH_CHECK` or the Meta permissions `EYE_TRACKING`, `BOUNDARY_VISIBILITY`, `IMPORT_EXPORT_IOT_MAP_DATA` and `USE_COLOCATION_DISCOVERY_API` (`com.oculus.permission.*`, from Meta features the builder keeps off). Its `com.oculus.ossplash.background` meta-data is `passthrough-contextual` (read with `aapt2 dump xmltree`).

These are the expectations from [BUILD.md](BUILD.md#verifying-a-build). If what a Quest build should contain changes, update the `Verify the APK` step in `quest-apk.yml` and BUILD.md together.

## Pinned actions

Every action is pinned to a full commit SHA, with its release in a trailing comment:

| Action | Release | Commit |
|---|---|---|
| `actions/checkout` | v7.0.1 | `3d3c42e5aac5ba805825da76410c181273ba90b1` |
| `actions/cache/restore`, `actions/cache/save` | v6.1.0 | `55cc8345863c7cc4c66a329aec7e433d2d1c52a9` |
| `actions/upload-artifact` | v7.0.2 | `cf430e030ddbb5b0abf93d22962f4752f3646cd9` |
| `game-ci/unity-test-runner` | v4.3.2 | `fa6ced25861c16ef56187828c43f76d00df43a23` |
| `game-ci/unity-builder` | v5.0.1 | `adaf7cd8f42fd48fbbf2d4f5f67ca30d87f819f1` |

The game-ci releases are the ones that carry all of their logic in the pinned commit. The newer thin-wrapper releases (`unity-builder` v6, `unity-test-runner` v4.4) download the latest `game-ci/cli` binary at run time unless their `cliVersion` input is pinned as well, which would move the actual build logic outside the pin.

To update an action, pick its new release, look up the commit (`gh api repos/<owner>/<repo>/commits/<tag> --jq .sha`), and replace both the SHA and the comment everywhere the action is used.

## Running the same jobs locally

From the repository root, with the editor from `ProjectSettings/ProjectVersion.txt` (do not add `-quit` to any of these: the test runner and the builder exit by themselves):

```
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results/editmode-results.xml -logFile results/editmode.log
Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults results/playmode-results.xml -logFile results/playmode.log
Unity -batchmode -projectPath . -buildTarget Android -executeMethod XRScan.EditorTools.XRScanBuilder.BuildQuest -logFile results/quest-build.log
```

`Unity` is the editor executable (`Unity.exe` on Windows). The build writes `Builds/CISpatialCompanion-Quest.apk` and exits with 0 when it was built, 1 when it failed. Then run `python tools/apk/scan_apk.py Builds/CISpatialCompanion-Quest.apk --target quest` and `python tools/apk/check_notices.py Builds/CISpatialCompanion-Quest.apk`.

## Troubleshooting

- **Setup fails with "Unity licence secrets missing".** Add the secrets named in the error (see [Licence secrets](#licence-secrets)).
- **Activation fails in the Unity step.** game-ci retries a few times before giving up. Check that the e-mail and password belong to the Unity ID that holds the licence, and that `UNITY_LICENSE` is the whole `.ulf` file, including its XML header.
- **No space left on device.** The `Free disk space` step prints `df` before and after its cleanup, so you can see how much room the image had.
- **PlayMode fails during XR start-up rather than on a test.** In the committed XR settings, the desktop (Standalone) target has *Initialize XR on Startup* on, with the OpenXR, XR Simulation and Mock HMD loaders. CI runners have no OpenXR runtime. The PlayMode tests build their own camera rig and do not need XR, so turning off *Initialize XR on Startup* for Standalone is a safe fix if this ever happens.
