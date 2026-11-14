# Continuous integration

GitHub Actions runs the Unity tests and builds the Meta Quest 3 APK headlessly, in the editor version the project is pinned to. The workflows live in `.github/workflows/`:

| Workflow | Runs on | What it does |
|---|---|---|
| `ci.yml` (**CI**) | pushes to `main`, every pull request, manual | Calls the two workflows below: the tests first, then the Quest APK once both test suites pass. Changes that only touch Markdown, `docs/`, `static/`, `LICENSE*` or `.vscode/` do not start it. |
| `unity-tests.yml` (**Unity tests**) | called by CI and Release, or manual | EditMode and PlayMode tests, as two jobs that run one after the other. |
| `quest-apk.yml` (**Quest APK**) | called by CI, or manual | Builds `CISpatialCompanion-Quest.apk` through `XRScanBuilder.BuildQuest`, signed with Unity's debug key, checks what was packaged and how it is signed, uploads the APK. |
| `release.yml` (**Release**) | `v*` tags on `main`, manual from `main` or such a tag | The tests, then a build signed with the release keystore, in its own job in the `release` environment: the APK for the Meta Horizon Store, or an Android App Bundle. See [Release builds](#release-builds). |

To run CI on a branch that has no pull request, open **Actions → CI → Run workflow** and pick the branch. The two inner workflows can be started the same way on their own.

A newer push to the same branch or pull request queues behind a run that has already started instead of cancelling it, because cancelling a Unity job part-way can skip returning its licence activation.

The Pico APK is not built in CI. `XRScanBuilder.BuildPico` would slot in as a second build job the same way once the PICO SDK compiles on the project's editor; on Unity 6000.6 it stops with exit code 1 (see [BUILD.md](BUILD.md#pico-builds-need-unity-60003-for-now)).

## Licence secrets

Unity needs an activated licence even in batch mode. The Unity steps activate it through [game-ci/cli](https://github.com/game-ci/cli), which reads the Unity ID from repository secrets (**Settings → Secrets and variables → Actions → New repository secret**), returns the activation when the job ends, and never prints the values:

| Secret | Unity Personal | Paid seat (Pro, Enterprise, Industry) |
|---|---|---|
| `UNITY_EMAIL` | required: e-mail of the Unity ID that holds the licence | required |
| `UNITY_PASSWORD` | required: password of that Unity ID | required |
| `UNITY_SERIAL` | leave unset | required: the seat's serial key (`XX-XXXX-XXXX-XXXX-XXXX-XXXX`), shown with the subscription on cloud.unity.com |

- **Personal** activates from the Unity ID alone. Sign in to Unity Hub once with that ID and accept the Personal licence terms there before the first run.
- **`UNITY_LICENSE` is not used.** Do not add it: game-ci tries a `.ulf` licence file before anything else, and on Unity 6000.6 a file activated on another machine fails (`Machine bindings don't match`).
- **The Unity ID.** Use one that signs in with a password (not Google or Apple sign-in) and has two-factor authentication off, ideally an ID used only by CI. Keep `"` and `\` out of its password: game-ci passes the values into the build container on a quoted command line. Unity may send a new-device e-mail for the runners' addresses the first time; confirm it.
- **Seats.** The test jobs run one after the other and the build waits for them, so a run holds one activation at a time. Runs on different branches can overlap; a paid seat allows two activations, one of which may be a workstation signed in with the same ID. If a run reports that no seat is free, release the activations at id.unity.com.

With the GitHub CLI, from a shell outside the repository (`gh secret set` prompts for the value, so it never lands in your shell history):

```
gh secret set UNITY_EMAIL --repo companionintelligence/CI-Spatial-Agent-Unity
gh secret set UNITY_PASSWORD --repo companionintelligence/CI-Spatial-Agent-Unity
gh secret set UNITY_SERIAL --repo companionintelligence/CI-Spatial-Agent-Unity   # paid seat only
```

Each workflow starts with a small **Setup** job that checks the secrets before any Unity job is queued:

- On a push or a pull request from a branch of this repository, missing secrets fail Setup with an error that names them.
- Pull requests from forks and from Dependabot never receive repository secrets. For those, the Unity jobs are skipped with a notice instead of failing. A maintainer can run the workflow from a branch of this repository.

CI passes only these three secrets to its workflows, so no pull request or branch build ever receives the release signing secrets.

## Release builds

A **release build** is signed with the app's release keystore, the same key and the same code path as a local `-release` build ([BUILD.md](BUILD.md#release-signing-and-version-code)). Only the **Release** workflow makes one, and only for:

- a pushed tag `v<version>` whose commit is on `main`, for example `v0.1.1`. The version must be Player Settings → Version of that commit ([BUILD.md](BUILD.md#version)), or Setup fails. A tag builds the APK;
- a manual run, **Actions → Release → Run workflow**, from `main` or from such a tag. It asks for the package: `apk` (for the Meta Horizon Store) or `aab` (an Android App Bundle of the same build).

Setup fails on any other branch or tag, and pull requests never start the workflow. CI builds on every other push and pull request stay debug-signed.

### The release environment

The signed build is its own job, **Build the signed Quest APK** (or app bundle), and it runs in the environment `release`. Set the environment up once, before the first release run (a job that names a missing environment creates it without any protection):

1. **Settings → Environments → New environment**, name it `release`.
2. **Required reviewers:** add the people who may approve a release build, and tick **Prevent self-review** if more than one person can approve. The build job then waits for an approval after Setup and the tests have passed.
3. **Deployment branches and tags:** choose **Selected branches and tags** and add the branch rule `main` and the tag rule `v*`. A run from any other branch, including a branch that edits the workflow, then cannot use the environment or its secrets.
4. **Environment secrets:** add the four signing secrets below there, not as repository secrets. The Unity licence secrets stay repository secrets, since every Unity job needs them.

| Secret (environment `release`) | Value |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | the release keystore `ci-spatial-companion-release.p12`, base64-encoded on one line |
| `ANDROID_KEYSTORE_PASS` | keystore password: at least 20 characters, letters and digits only |
| `ANDROID_KEYALIAS_NAME` | `ci-spatial-companion` |
| `ANDROID_KEYALIAS_PASS` | key password; in a PKCS12 keystore it is the keystore password |

In PowerShell, without writing the encoded key to disk (`gh secret set` prompts for the other values):

```
[Convert]::ToBase64String([IO.File]::ReadAllBytes("$env:USERPROFILE\ci-signing\ci-spatial-companion-release.p12")) | gh secret set ANDROID_KEYSTORE_BASE64 --env release --repo companionintelligence/CI-Spatial-Agent-Unity
gh secret set ANDROID_KEYSTORE_PASS --env release --repo companionintelligence/CI-Spatial-Agent-Unity
gh secret set ANDROID_KEYALIAS_NAME --env release --repo companionintelligence/CI-Spatial-Agent-Unity
gh secret set ANDROID_KEYALIAS_PASS --env release --repo companionintelligence/CI-Spatial-Agent-Unity
```

The workflow also works with the four as repository secrets (which is what `gh secret set` without `--env` makes), but then a workflow on any branch could read them. If they were set that way, set them in the environment as above and delete the repository copies with `gh secret delete <name> --repo companionintelligence/CI-Spatial-Agent-Unity`.

### What a release run does

1. **Setup**, without the signing secrets: checks that the run is on `main` or on a `v*` tag whose commit `main` contains, reads the Unity version and the app version, fails a tag that is not `v<version>`, and checks the Unity licence secrets.
2. **Unity tests:** the same EditMode and PlayMode jobs as CI, with the Unity secrets only.
3. **The signed build**, in the `release` environment, once a reviewer approves:
   1. It fails, naming them, when a signing secret is missing, the keystore is not base64, or a password is shorter than 20 characters or holds anything but letters and digits. It never prints a value.
   2. It restores the Android `Library` cache that the debug builds on `main` save, read-only: it never saves a cache.
   3. game-ci decodes the keystore to `release.keystore` in the project folder (git-ignored) and starts the editor with `ANDROID_KEYSTORE_NAME=release.keystore`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME` and `ANDROID_KEYALIAS_PASS` in its environment, the variables `XRScanBuilder` reads, plus `-release` (and `-aab` for an app bundle), so the builder fails rather than sign with the debug key. Unity signs the package itself: an APK with APK signature scheme v2, an app bundle with a JAR signature.
   4. The next step deletes `release.keystore`, whether the build passed or not.
   5. `tools/apk/check_quest_package.sh` runs every [check a debug APK gets](#quest-apk-checks), plus: exactly one signer, and not Unity's debug key (`CN=Android Debug`). It writes the signer's certificate DN and SHA-256 digest to the run summary; compare the digest with the one recorded when the key was made before uploading the package to a store.
   6. It uploads the checked package and nothing else, as `CISpatialCompanion-Quest-<version>.apk` (or `.aab`); a manual run from `main` adds the short commit, `CISpatialCompanion-Quest-<version>-<commit>.apk`, since every build of one version has the same versionCode.

Why the signed build is kept apart like this:

- **Unity copies the passwords to disk.** During a signed build Unity 6000.6 writes the keystore and key passwords in plain text into the project's `Library/Bee` (its Gradle project and build records), and Gradle compiles its build scripts into a cache under `GRADLE_USER_HOME`. Anyone who can open a pull request in this public repository can read its Actions caches. So the job that sees the signing secrets never saves a `Library` or Gradle cache and never uploads `Library`, logs or anything but the package. `XRScanBuilder` also removes those copies itself ([BUILD.md](BUILD.md#release-signing-and-version-code)); the workflow does not rely on that.
- **The passwords travel on command lines.** game-ci passes them into the build container as `--env NAME="value"` and to the editor as `-androidKeystorePass` and `-androidKeyaliasPass`, and Unity puts them in single quotes in its Gradle script. GitHub masks secret values in the job log, which only works while no quoting changes them. Hence long, random passwords of letters and digits; the job refuses others.
- **Pull requests and branch builds never see the secrets.** CI passes only the Unity secrets to its workflows, the signing secrets live in the environment, and the environment admits only `main` and `v*` tags.
- **Nothing re-signs the package.** If you ever re-sign an APK with `apksigner`, pass `--v2-signing-enabled true`: with its defaults it writes only a v3 signature for this app's minimum SDK, and the Meta Horizon Store accepts only v1 or v2.

To make a release: raise Player Settings → Version, merge that to `main`, tag the merge commit `v<version>` and push the tag ([BUILD.md](BUILD.md#version)), then approve the build job when it asks. The signed package is in the run's artifacts.

## Unity version

Every run reads `m_EditorVersion` from `ProjectSettings/ProjectVersion.txt` and runs in the matching [game-ci](https://game.ci) Docker image:

- tests: `unityci/editor:ubuntu-<version>-linux-il2cpp-3`
- Quest APK and release builds: `unityci/editor:ubuntu-<version>-android-3`

Upgrading the editor therefore needs no workflow change, as long as game-ci publishes images for the new version. Check the `unityci/editor` tags on Docker Hub before upgrading. The version is also part of the cache keys, so the first run after an upgrade starts from a cold `Library/`.

## Library cache

Each Unity job restores `Library/` from the Actions cache before it opens the project. The test jobs and the debug APK job save it again after a successful run; the signed release build only restores it ([Release builds](#release-builds)).

- Key: `Library-Linux-<tests|android>-<editor version>-<hash of Assets/, Packages/ and ProjectSettings/>`.
- When nothing matches exactly, the newest cache with the same platform and editor version is restored, so a run after a script change re-imports only what changed.
- The tests and the APK use separate caches (desktop Linux versus Android target). The EditMode and PlayMode jobs import the same project, so only the EditMode job saves the tests cache.
- A failed run never saves, so a broken import is not cached.
- After a restore, each job deletes `Library/ilpp.pid` and `Library/MSBuild/buildhost.pid`. Unity kills the processes those files name when it starts, and in a new container such a process ID can be the editor itself, which then dies with exit code 137.
- To start clean, delete the caches under **Actions → Caches**, or run `gh cache delete --all --repo companionintelligence/CI-Spatial-Agent-Unity`.

The Unity images are large, so every Unity job first deletes preinstalled SDKs that the runner image ships and these jobs never use (the Unity image brings its own Android SDK, NDK and JDK).

## Artifacts

| Artifact | Uploaded by | Contents |
|---|---|---|
| `test-results-editmode` | EditMode job | `editmode-results.xml` (NUnit 3) and `editmode.log` (editor log) |
| `test-results-playmode` | PlayMode job | `playmode-results.xml` and `playmode.log` |
| `CISpatialCompanion-Quest.apk` | Quest APK job | The debug-signed APK itself, not zipped. |
| `CISpatialCompanion-Quest-<version>.apk` or `.aab` | Release build job | The signed APK or app bundle, not zipped, uploaded only when every check passed. |

Artifacts are kept for 14 days, a release build's package for 30. Each job also writes a run summary: pass, fail and skip counts with the failing tests, or the package's size, SHA-256, version, signer and manifest highlights.

The Quest APK job's APK is signed with Unity's debug key. Sideload it with `adb install -r CISpatialCompanion-Quest.apk`; it is not suitable for a store upload. A release build is signed with the release keystore, so `adb install -r` over a debug-signed build fails until that build is uninstalled.

## Quest APK checks

The build step runs the editor without `-quit` (game-ci's `manualExit: true`): `XRScanBuilder` takes MCP for Unity out of the project before the Quest build (the PICO SDK is not in it; see [BUILD.md](BUILD.md#the-pico-sdk-is-added-only-for-the-pico-build)), which needs a script reload that `-quit` would cut short, and exits by itself once the build has ended and the project files are restored. It exits with code 1, failing the step, when the build fails, throws or writes no APK. The `Verify the APK` step then runs `tools/apk/check_quest_package.sh`, which fails when any of these is not true:

- `Builds/CISpatialCompanion-Quest.apk` exists and is not empty.
- `tools/apk/scan_apk.py --target quest` finds no PICO SDK, MCP for Unity, Ready Player Me or Visual Scripting content in any file of the APK, serialized assets included.
- `tools/apk/check_notices.py` finds every library the APK packages named in `THIRD_PARTY_NOTICES.md`: each Maven artifact that left a `META-INF/*.version` file, with that version, each native library, and each Java package in `classes.dex`. Unity's Gradle build adds libraries (AndroidX, Kotlin, kotlinx.coroutines, GameActivity, the NDK's `libc++_shared.so`) that `Packages/packages-lock.json`, and so `ThirdPartyNoticesTests`, never sees; when a dependency change adds one, this check fails until the notices list it.
- Native libraries are ARM64 only. `libil2cpp.so`, `libunity.so` and `libopenxr_loader.so` are present, and none of the PICO or Mock HMD libraries that `XRScanNativeLibExcluder` strips from Quest builds are.
- The manifest, read with `aapt2` from the Android SDK inside the Unity image, has package `com.companionintelligence.xrscan`, the versionName and versionCode that Setup derived from Player Settings → Version, minimum SDK 32, target SDK 34, native code `arm64-v8a` only, the permissions `android.permission.RECORD_AUDIO`, `horizonos.permission.HEADSET_CAMERA` and `com.oculus.permission.USE_SCENE`, install location `auto`, and none of the PICO permission `com.pvr.tobactivate.permission.AUTH_CHECK`, the Meta permissions `EYE_TRACKING`, `BOUNDARY_VISIBILITY`, `IMPORT_EXPORT_IOT_MAP_DATA` and `USE_COLOCATION_DISCOVERY_API` (`com.oculus.permission.*`, from Meta features the builder keeps off), `android.permission.BLUETOOTH`, `android.permission.MODIFY_AUDIO_SETTINGS` or the feature `android.hardware.bluetooth`, declared or implied. Its `com.oculus.ossplash.background` meta-data is `passthrough-contextual` and its `com.oculus.supportedDevices` meta-data exactly `quest3|quest3s` (read with `aapt2 dump xmltree`).
- `apksigner verify`, from the same SDK, finds an APK signature scheme v2 signature by exactly one signer. The summary names the signer.

The release build runs the same script on its package and also requires a signer other than Unity's debug key. For an app bundle it reads the manifest with `bundletool dump manifest`, finds the native libraries under `base/lib/`, and verifies the JAR signature with `jarsigner` and `keytool -printcert`. These are the expectations from [BUILD.md](BUILD.md#verifying-a-build). If what a Quest build should contain changes, update `tools/apk/check_quest_package.sh` and BUILD.md together.

## Pinned actions

Every action is pinned to a full commit SHA, with its release in a trailing comment:

| Action | Release | Commit |
|---|---|---|
| `actions/checkout` | v7.0.1 | `3d3c42e5aac5ba805825da76410c181273ba90b1` |
| `actions/cache/restore`, `actions/cache/save` | v6.1.0 | `55cc8345863c7cc4c66a329aec7e433d2d1c52a9` |
| `actions/upload-artifact` | v7.0.2 | `cf430e030ddbb5b0abf93d22962f4752f3646cd9` |
| `game-ci/unity-test-runner` | v4.4.0 | `32e57712352b500e17974b245a6dce9e11a73213` |
| `game-ci/unity-builder` | v6.0.0 | `eb1b9fba120c6e62c9fb7a7a81d6c107ce004c45` |

These game-ci releases are thin wrappers: at run time they install the `game-ci/cli` release named by their `cliVersion` input, which does the activation, the build and the test run. Every Unity step pins it to `v0.1.72` (October 2026), a release that activates Unity Personal from the Unity ID alone and returns the activation when the job ends; without `cliVersion` they would install the latest release on every run. The CLI is fetched by its release tag, not by commit, so this part of the pipeline is pinned less tightly than the actions themselves. The older all-in-one releases (`unity-builder` v5.0.1, `unity-test-runner` v4.3.2) activate only with a serial key or a `.ulf` licence file, which is no supported route for Unity Personal on 6000.6. When moving to a newer CLI, change `cliVersion` in all three workflows; from v0.1.74 on, the CLI deletes the stale process-ID files itself.

To update an action, pick its new release, look up the commit (`gh api repos/<owner>/<repo>/commits/<tag> --jq .sha`), and replace both the SHA and the comment everywhere the action is used.

## Running the same jobs locally

From the repository root, with the editor from `ProjectSettings/ProjectVersion.txt` (do not add `-quit` to any of these: the test runner and the builder exit by themselves):

```
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results/editmode-results.xml -logFile results/editmode.log
Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults results/playmode-results.xml -logFile results/playmode.log
Unity -batchmode -projectPath . -buildTarget Android -executeMethod XRScan.EditorTools.XRScanBuilder.BuildQuest -logFile results/quest-build.log
```

`Unity` is the editor executable (`Unity.exe` on Windows). The build writes `Builds/CISpatialCompanion-Quest.apk` and exits with 0 when it was built, 1 when it failed. Then run `python tools/apk/scan_apk.py Builds/CISpatialCompanion-Quest.apk --target quest` and `python tools/apk/check_notices.py Builds/CISpatialCompanion-Quest.apk`. A signed release build, APK or app bundle, is described in [BUILD.md](BUILD.md#building-a-signed-release-locally).

## Troubleshooting

- **Setup fails with "Unity licence secrets missing".** Add the secrets named in the error (see [Licence secrets](#licence-secrets)).
- **Release Setup fails with "Not a release ref".** Release builds run only from `main` or from a `v*` tag on a commit of `main`; push the tag on the merge commit, or start the run from `main`.
- **Release Setup fails because the tag does not match the app version.** Tag the commit that sets Player Settings → Version: the tag of version `0.1.1` is `v0.1.1`.
- **The signed build fails with "Release signing secrets missing" or "invalid".** Add the four `ANDROID_*` secrets to the `release` environment ([The release environment](#the-release-environment)). Re-encode the keystore if the base64 check fails. The job refuses a password shorter than 20 characters or with anything but letters and digits, since such a value does not survive the quoting described above.
- **The signed build waits.** It needs an approval from a required reviewer of the `release` environment: open the run and choose **Review deployments**.
- **Activation fails in the Unity step.** game-ci retries a few times before giving up. Check that the e-mail and password belong to the Unity ID that holds the licence, that the ID signs in with a password and has two-factor authentication off, that `UNITY_SERIAL` is set only for a paid seat, and that no `UNITY_LICENSE` secret is set. For Unity Personal, sign in to Unity Hub with that ID once and accept the Personal terms.
- **No space left on device.** The `Free disk space` step prints `df` before and after its cleanup, so you can see how much room the image had.
- **PlayMode fails during XR start-up rather than on a test.** In the committed XR settings, the desktop (Standalone) target has *Initialize XR on Startup* on, with the OpenXR, XR Simulation and Mock HMD loaders. CI runners have no OpenXR runtime. The PlayMode tests build their own camera rig and do not need XR, so turning off *Initialize XR on Startup* for Standalone is a safe fix if this ever happens.
