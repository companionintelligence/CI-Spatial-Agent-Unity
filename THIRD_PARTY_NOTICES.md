# Third-Party Notices

CI Spatial Companion is proprietary software of LifeScope Inc. dba Companion Intelligence (see [LICENSE.md](LICENSE.md)). This file lists every third-party package and asset the Unity project depends on, whether it ends up in the Android players, the licence it is under, and the notices those licences require. Ship these notices with every release (an in-app "Licences" screen or a document next to the store listing); the full licence texts referenced below must accompany the distribution.

Reviewed 2026-11-12 against `Packages/packages-lock.json`, Unity 6000.3.19f1 and the contents of Quest and Pico APKs built with `XRScan.EditorTools.XRScanBuilder.BuildQuest` / `BuildPico` (see [Verification](#6-how-this-was-verified)). When a dependency or asset is added, removed or upgraded, update this file in the same change.

Licence abbreviations: **UCL** = [Unity Companion License](https://unity3d.com/legal/licenses/unity_companion_license) · **UPDL** = [Unity Package Distribution License](https://unity3d.com/legal/licenses/Unity_Package_Distribution_License) · **Oculus SDK** = [Oculus SDK License Agreement](https://developer.oculus.com/licenses/oculussdk/).

## 1. Shipped in the player builds

`Quest` = build made by **XRScan → Build → Quest 3** (`XRSCAN_QUEST`); `Pico` = **XRScan → Build → Pico** (`XRSCAN_PICO`). See [`docs/BUILD.md`](docs/BUILD.md#what-each-apk-contains) for how each build is trimmed.

### 1.1 Engine and Unity packages

| Component | Version | Licence (as stated in the package's `LICENSE.md`) | Bundled third-party components (notices in §5) | Quest | Pico |
|---|---|---|---|---|---|
| Unity runtime (engine, IL2CPP class libraries) | 6000.3.19f1 | [Unity Editor Software Terms](https://unity.com/legal/editor-terms-of-service/software) §2.2 (runtime may be distributed as part of the project) | Unity's own notices | yes | yes |
| Universal RP `com.unity.render-pipelines.universal` | 17.3.0 | UCL | FXAA 3.11 (NVIDIA, BSD-3-Clause style) | yes | yes |
| SRP Core `com.unity.render-pipelines.core` / Shader Graph `com.unity.shadergraph` | 17.3.0 | UCL | RadeonRays 4.1 (MIT), Bullet Physics (zlib), Sobol sampler (MIT) | yes | yes |
| Burst `com.unity.burst` | 1.8.29 | UCL (source) / UPDL | LLVM (Apache-2.0 WITH LLVM-exception), musl (MIT), SLEEF (BSL-1.0), mimalloc (MIT) and others listed in the package's `Third Party Notices.md` | yes | yes |
| Collections 2.5.1, Mathematics 1.3.3, Input System 1.19.0, AI Navigation 2.0.0, Animation Rigging 1.3.0, Timeline 1.8.6, uGUI 2.0.0 (incl. TextMesh Pro) | see left | UCL | — | yes | yes |
| Visual Scripting `com.unity.visualscripting` | 1.9.1 | UPDL | Full Serializer, YamlDotNet, NCalc, SQLite .NET … (MIT), DotNetZip, AQN Parser (Ms-PL), SQLite (public domain) — see the package's `Third Party Notices.md` | yes | yes |
| XR Plug-in Management 4.5.4, XR Core Utilities 2.6.0, Composition Layers 2.4.0, AR Foundation 6.5.0 | see left | UCL (Composition Layers: UCL source / UPDL) | — | yes | yes |
| XR Legacy Input Helpers `com.unity.xr.legacyinputhelpers` | 2.1.13 | UCL | ArmModel / SwingArmModel / TransitionArmModel (Google, Apache-2.0) | yes | yes |
| OpenXR Plugin `com.unity.xr.openxr` | 1.16.1 | UCL (source) / UPDL | Khronos OpenXR loader (OpenXR-SDK-Source, Apache-2.0) — Quest builds use this loader; Oculus OpenXR Mobile SDK (Oculus SDK) when the Meta Quest feature is enabled | yes | yes (PICO's own loader replaces the Khronos one) |
| Unity OpenXR: Meta `com.unity.xr.meta-openxr` | 2.5.1 | UCL (source) / UPDL | OpenXR-SDK-Source (Apache-2.0), Oculus OpenXR Mobile SDK and AudioPluginOculusSpatializer (Oculus SDK) | yes | managed code only; `libUnityARFoundationMeta.so` stripped |
| Oculus XR Plugin `com.unity.xr.oculus` | 4.5.4 | UCL (source) / UPDL | OVRPlugin, AudioPluginOculusSpatializer (Oculus SDK) | managed `Unity.XR.Oculus` and Unity's `OculusUnity` Java helper only; OVRPlugin and the spatializer are not packaged (the Oculus loader is off) | same as Quest |
| ARCore XR Plugin `com.unity.xr.arcore` | 6.3.5 | UCL | Google ARCore client library ([Google APIs Terms of Service](https://developers.google.com/terms/)) | managed `Unity.XR.ARCore` only; Google's client library is not packaged (the ARCore loader is off on Android) | same as Quest |
| Mock HMD `com.unity.xr.mock-hmd` | 1.5.0-exp.3 | UCL | — (`libUnityMockHMD.so` stripped from both APKs) | managed only | managed only |
| Unity glTFast `com.unity.cloud.gltfast` | 6.14.1 | Apache-2.0 | — | yes | yes |
| Newtonsoft Json `com.unity.nuget.newtonsoft-json` | 3.2.1 | UCL (package) | Newtonsoft.Json, Json.Net.Unity3D, Newtonsoft.Json-for-Unity, com.newtonsoft.json (all MIT) | yes | yes |

### 1.2 Other packages

| Component | Version / source | Licence | Quest | Pico |
|---|---|---|---|---|
| uLipSync `com.hecomi.ulipsync` | 3.1.5, git `hecomi/uLipSync` @ `ece3272762f2148dd8d3e658ec04fcbd79d4d97e` | MIT, © 2021 hecomi ([LICENSE.md](https://github.com/hecomi/uLipSync/blob/ece3272762f2148dd8d3e658ec04fcbd79d4d97e/LICENSE.md)) | yes | yes |
| PICO OpenXR Plugin `com.unity.xr.openxr.picoxr` | 1.4.1, git `Pico-Developer/PICO-Unity-OpenXR-SDK` @ `3aa3e62bff41df618529eeb60ff02c29a515dafe` (tag `release_1.4.0`) | **Proprietary.** © 2015-2022 PICO Technology Co., Ltd., all rights reserved; its [LICENSE.md](https://github.com/Pico-Developer/PICO-Unity-OpenXR-SDK/blob/3aa3e62bff41df618529eeb60ff02c29a515dafe/LICENSE.md) forbids dissemination or reproduction without PICO's prior written permission | **no** — managed assemblies, Java plugins, native libs and the `AUTH_CHECK` permission are stripped | yes — **needs PICO's written permission before distribution** |

### 1.3 Assets

| Asset | Origin and licence | Quest | Pico |
|---|---|---|---|
| `Assets/XRScan/Avatar/Models/CI_Companion.glb` | Companion Intelligence. Generated entirely by [`tools/avatar/build_ci_companion.py`](tools/avatar/build_ci_companion.py) (no imported meshes, textures, scans or fonts); its glTF `asset.copyright` reads "Copyright (c) 2026 LifeScope Inc. dba Companion Intelligence. All rights reserved." Proprietary ([LICENSE.md](LICENSE.md)). Blender, used to run the script, does not license its output ([blender.org/about/license](https://www.blender.org/about/license/)). | yes | yes |
| uLipSync profile `uLipSync-Profile-Sample-Male.asset` (referenced by `AvatarLipSync`) | Part of the uLipSync package (MIT) | yes | yes |
| Scenes, prefabs, scripts, materials and settings under `Assets/` | Companion Intelligence ([LICENSE.md](LICENSE.md)); Unity-generated settings files | yes | yes |

No third-party fonts, textures, audio clips or 3D models are shipped. The uLipSync `Samples~` (Unity-chan assets under the Unity-Chan License) are not imported into the project.

## 2. Development tools (never in a player)

| Component | Version | Licence | Notes |
|---|---|---|---|
| MCP for Unity `com.coplaydev.unity-mcp` | 10.3.0, git `CoplayDev/unity-mcp` @ `aa5fc638d623f56178d50329d9ad8c541a57fe66` (tag `v10.3.0`, path `/MCPForUnity`) | MIT, © 2025 CoplayDev ([LICENSE](https://github.com/CoplayDev/unity-mcp/blob/aa5fc638d623f56178d50329d9ad8c541a57fe66/LICENSE)) | Edit-time bridge used with the Unity MCP tools (see `docs/HANDOFF.md`). `XRScanPlayerAssemblyFilter` keeps its `MCPForUnity.Runtime` assembly out of every player build. Its editor telemetry is on unless opted out: set `UNITY_MCP_DISABLE_TELEMETRY=1` (or `DISABLE_TELEMETRY` / `MCP_DISABLE_TELEMETRY`) or use its settings window. It does not start its bridge in batch mode unless `UNITY_MCP_ALLOW_BATCH` is set. |
| Test Framework 1.6.0, Performance Testing 3.5.0 | — | UCL | Test assemblies are excluded from players. |
| Custom NUnit `com.unity.ext.nunit` 2.0.5 | — | UPDL; NUnit (MIT) | Editor/test only. |
| Rider 3.0.27, Visual Studio 2.0.22 IDE integrations | — | MIT | Editor only. |
| Version Control (collab-proxy) 2.2.0, Editor Coroutines 1.1.0, Searcher 4.9.4, Mono.Cecil 1.11.6, URP Config 17.0.3 | — | UPDL / UCL; Mono.Cecil (MIT) | Editor only. |
| Blender | 5.1 | GPL-2.0-or-later | Tool only: runs `tools/avatar/build_ci_companion.py` headless. Not distributed; its licence does not extend to generated output. |

## 3. Removed

| Item | Why |
|---|---|
| Ready Player Me Unity SDK `com.readyplayerme.core` (git `v7.4.0`), its `Assets/Ready Player Me` settings (`CoreSettings`, `AvatarLoaderSettings`), the `READY_PLAYER_ME` scripting define on every platform, and its `glTFastShaderVariantsURP` collection in Graphics Settings → Preloaded Shaders | Ready Player Me shut its services down on 31 January 2026 after its acquisition by Netflix ([PocketGamer.biz](https://www.pocketgamer.biz/netflix-acquires-avatar-creation-platform-ready-player-me/)) and its SDK repository is archived. Its assemblies (including Amplitude analytics loggers under `Runtime/Core/Scripts/Analytics`) shipped in the APK although no project code used them, and its `[InitializeOnLoad]` `OneTimeSetup` calls `AnalyticsEditorLogger.Enable()` — which writes `EnableAnalytics = true` — whenever its first-time-setup flag is not yet stored, i.e. on the first open of every fresh checkout (v7.4.0, [`OneTimeSetup.cs`](https://github.com/readyplayerme/rpm-unity-sdk-core/blob/v7.4.0/Editor/Core/Scripts/UI/EditorWindows/SetupGuide/OneTimeSetup.cs)), overriding the committed `EnableAnalytics: 0`. The SDK itself was MIT-licensed. glTFast, which it pulled in, is now a direct dependency. |
| `Assets/XRScan/Avatar/Models/CI_Brad.glb`, `LiamCES.glb` | Third-party avatars: their glTF `asset` blocks read `"generator": "Ready Player Me"`, `"copyright": "Ready Player Me"`. Replaced by `CI_Companion.glb`. |
| Previous `docs/images/ci_companion_avatar.png` | Screenshot of the Ready Player Me avatar; replaced by a render of `CI_Companion.glb`. |
| `static/concepts/Frame 6.png`, `Frame 7.png`, `Screenshot 2025-01-12 at 20.18.31.png` | Concept mock-ups with no recorded source or licence: composites over interior photographs of unknown origin (one with a framed Tokyo / Mt Fuji photo), an avatar resembling a Ready Player Me render, and a browser screenshot of the web companion with a Ready Player Me-style avatar. Never shipped. Removed as an unclear licence; restore with `git checkout fb8cf21 -- static/concepts` once ownership is confirmed. |

## 4. Open items for the owner

1. **PICO**: obtain PICO's written permission (or confirm that the PICO developer agreement grants it) before distributing the Pico APK, which must contain the PICO SDK.
2. **Meta**: the Quest APK contains Meta components under the Oculus SDK License Agreement; distribution through the Meta Horizon Store is governed by that agreement and the Meta developer terms.
3. **Unused packages**: ARCore (`com.unity.xr.arcore`), Mock HMD, Visual Scripting and the Oculus XR Plugin add managed code to both APKs without being used on the headsets (their native/Java SDKs are already left out). Removing them from `Packages/manifest.json`, if no phone build is planned, shrinks the APK and the notices.
4. **Concept images** removed from `static/concepts` (see §3): confirm whether CI owns or licensed the photographs and avatar renders before restoring them.
5. **Notices in the product**: surface §5 (and the full Apache-2.0, UCL/UPDL and Oculus SDK terms, by link or text) in the app or store listing.

## 5. Notices

### 5.1 MIT License

The following components are licensed under the MIT License, reproduced once below; each copyright line applies to its component.

- uLipSync — Copyright (c) 2021 hecomi
- Newtonsoft.Json — Copyright (c) 2007 James Newton-King
- Json.Net.Unity3D — Copyright (c) 2016 SaladLab
- Newtonsoft.Json-for-Unity — Copyright (c) 2019 Kalle Jillheden (jilleJr)
- com.newtonsoft.json — Copyright (c) 2019 Mike Wuetherick
- RadeonRays 4.1 — Copyright (c) 2021 Advanced Micro Devices, Inc. All rights reserved.
- Sobol sampler — Copyright (c) 2023 Leonhard Gruenschloss
- musl — Copyright © 2005-2019 Rich Felker, et al.
- mimalloc — Copyright (c) 2018-2021 Microsoft Corporation, Daan Leijen
- MCP for Unity (development tool, not shipped) — Copyright (c) 2025 CoplayDev

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### 5.2 Apache License 2.0

Licensed under the Apache License, Version 2.0 (the "License"); you may not use these files except in compliance with the License. You may obtain a copy of the License at <https://www.apache.org/licenses/LICENSE-2.0> (a verbatim copy also ships in the glTFast package's `LICENSE.md`). Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License.

None of these components distributes a `NOTICE` file, so there is no NOTICE text to carry forward; their copyright notices are:

- Unity glTFast — "Unity glTFast copyright © 2023 Unity Technologies and the Unity glTFast authors"
- Khronos OpenXR loader (OpenXR-SDK-Source, bundled by the OpenXR and Meta OpenXR packages) — "Copyright © 2014-2022, The Khronos Group Inc."
- ArmModel.cs, SwingArmModel.cs, TransitionArmModel.cs (XR Legacy Input Helpers) — "Copyright 2016 Google Inc. All rights reserved."
- LLVM (Burst compiler) — Apache-2.0 WITH LLVM-exception; the exception waives attribution for compiled output.

### 5.3 Unity Companion License and Unity Package Distribution License

Each Unity package states its own licence in its `LICENSE.md`, for example: "com.unity.xr.openxr copyright © 2025 Unity Technologies ApS. Source code of the package is licensed under the Unity Companion License …; otherwise licensed under the Unity Package Distribution License". The UCL permits use of the package only in Unity-dependent projects; both licences are published at the links at the top of this file. The packages' own third-party notices are in each package's `Third Party Notices.md` (copied into `Library/PackageCache/<package>/` on import).

### 5.4 Meta (Oculus SDK License Agreement)

OVRPlugin, the Oculus OpenXR Mobile SDK and AudioPluginOculusSpatializer — "Copyright © Facebook Technologies, LLC and its affiliates. All rights reserved. Your use of this SDK or tool is subject to the Oculus SDK License Agreement, available at https://developer.oculus.com/licenses/oculussdk/" (as stated in the Oculus XR Plugin, OpenXR and Meta OpenXR packages' notices).

### 5.5 Google ARCore

Google ARCore client library (`arcore_client.aar`) — "Copyright © 2017 Google LLC". Use requires agreeing to and complying with the Google APIs Terms of Service, <https://developers.google.com/terms/> (as stated in the ARCore XR Plugin's notices). Not packaged in the current APKs (the ARCore loader is off on Android); this applies again if a phone build enables ARCore.

### 5.6 PICO

PICO OpenXR Plugin (Pico APK only) — Copyright © 2015-2022 PICO Technology Co., Ltd. All rights reserved. Proprietary; see its [LICENSE.md](https://github.com/Pico-Developer/PICO-Unity-OpenXR-SDK/blob/3aa3e62bff41df618529eeb60ff02c29a515dafe/LICENSE.md).

### 5.7 Other permissive licences

- **FXAA 3.11** (`FXAA3_11.hlsl`, Universal RP shaders):

  ```
  Copyright (c) 2014-2015, NVIDIA CORPORATION. All rights reserved.

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:
   * Redistributions of source code must retain the above copyright
     notice, this list of conditions and the following disclaimer.
   * Redistributions in binary form must reproduce the above copyright
     notice, this list of conditions and the following disclaimer in the
     documentation and/or other materials provided with the distribution.
   * Neither the name of NVIDIA CORPORATION nor the names of its
     contributors may be used to endorse or promote products derived
     from this software without specific prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ``AS IS'' AND ANY
  EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
  IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
  PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT OWNER OR
  CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
  EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
  PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
  PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY
  OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
  (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
  OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
  ```
- **Bullet Physics SDK** (SRP Core) — zlib License.
- **SLEEF** (Burst) — Boost Software License 1.0.
- Visual Scripting's bundled components — see that package's `Third Party Notices.md`.

## 6. How this was verified

- **Licences** were read from each package's `LICENSE.md` / `Third Party Notices.md` at the locked versions (the copies Unity imports into `Library/PackageCache`) and, for git packages, from the upstream repository at the pinned commit (links above). The Ready Player Me shutdown date is from the PocketGamer.biz report linked in §3.
- **Avatar provenance**: the glTF JSON `asset` block and `extras` of every GLB were inspected. `CI_Brad.glb` and `LiamCES.glb` declared `"generator": "Ready Player Me"`, `"copyright": "Ready Player Me"`; `CI_Companion.glb` declares the Companion Intelligence copyright and its scene `extras` name the generator script and licence. `AvatarModelTests` (EditMode) fails if any GLB in `Assets/XRScan/Avatar/Models` lacks a copyright or names Ready Player Me.
- **Quest APK** (`XRScanBuilder.BuildQuest`, Unity 6000.3.19f1, 2026-11-12, 47 MB): `assets/bin/Data/ScriptingAssemblies.json` lists 145 assemblies and none from MCP for Unity, the PICO SDK or Ready Player Me (the build log shows `XRScanPlayerAssemblyFilter` dropping `MCPForUnity.Runtime`, `PICO.Platform`, `PICO.TobSupport`, `Unity.XR.OpenXR.Features.PICOSupport` and `Unity.XR.OpenXRPico`); the only "Pico" string in the IL2CPP metadata is the project's own `XRVendor.Pico` enum value. Native libraries: `libunity`, `libil2cpp`, `libmain`, `lib_burst_generated`, `libUnityOpenXR`, `libUnityARFoundationMeta` and `libopenxr_loader` (1,597,864 bytes — the Khronos loader from `com.unity.xr.openxr`'s `openxr_loader.aar`, not PICO's 1,141,360-byte build). `classes.dex` holds only `com.unity3d.*`, `com.unity.oculus.OculusUnity` and `org.khronos.openxr` vendor classes; the manifest has no `pvr.*` entries or PICO permissions. The Gradle project's `unityLibrary/libs` contained only `openxr_loader.aar`, `unity-classes.jar` and `unityandroidpermissions.aar` after `XRScanNativeLibExcluder` removed PICO's six `.aar`/`.jar` plugins.
- **Pico APK** (`XRScanBuilder.BuildPico`, same day, 48 MB): the PICO SDK is intact — `PICO.Platform`, `PICO.TobSupport`, `Unity.XR.OpenXR.Features.PICOSupport` and `Unity.XR.OpenXRPico` among 149 assemblies, PICO's native libraries (`libopenxr_pico`, `libPICO_TOBAPI`, `libpvrcapturelib`, `libpxrplatformloader`, `libCameraRenderingPlugin`) and its 1,141,360-byte `libopenxr_loader`, its `com.pvr.*` / `com.picoxr.*` Java classes and the `AUTH_CHECK` permission. `MCPForUnity.Runtime` is the only assembly the filter removed; `libUnityARFoundationMeta.so` and `libUnityMockHMD.so` are excluded. No Ready Player Me or MCP for Unity content.
