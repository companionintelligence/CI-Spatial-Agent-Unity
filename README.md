<h1 align="center">CI Spatial Companion</h1>

<p align="center"><strong>Spatial Companion Agent Unity - Xreal OpenXR</strong></p>

[![release](https://img.shields.io/badge/release-not%20published-757575?style=flat-square)](https://github.com/companionintelligence/CI-Spatial-Agent-Unity/releases)
[![platform](https://img.shields.io/badge/platform-Quest%203%20%C2%B7%20Unity-0a6358?style=flat-square)](https://github.com/companionintelligence/CI-Spatial-Agent-Unity)
[![License](https://img.shields.io/badge/license-PolyForm%20NC%201.0.0-0a6358?style=flat-square)](LICENSE)
[![Discord](https://img.shields.io/badge/chat-Discord-5865F2?style=flat-square&logo=discord&logoColor=white)](https://discord.com/invite/yQp9hwpzAa)

<p align="center"><a href="#getting-started">Get started</a> · <a href="https://docs.ci.computer/docs/reference/open-source-systems">Systems map</a> · <a href="README.md#architecture">Documentation</a></p>

<p align="center">
  <a href="#getting-started"><img src="https://img.shields.io/badge/Get%20started-Get%20started-0a6358?style=for-the-badge&logo=unity&logoColor=white" alt="Get started" height="30"></a>
</p>

An embodied mixed-reality companion for **Meta Quest 3** and **Pico** — a spatial agent that scans the room, drops world-anchored pins on detected objects, and answers questions about them through a **Ready Player Me avatar** that looks at you, speaks with viseme lip-sync, and (WIP) walks the space.

> **Ground-up rebuild (July 2026).** Unifies the scanning + embodied-avatar goals on a supported LTS, replacing the earlier XREAL prototype. Original design brief preserved at the bottom of this file.

## Status

- **Unity 6000.3.19f1 (6.3 LTS)** · URP · OpenXR-first (Meta Quest + PICO feature groups)
- All assemblies compile green; **16 automated tests** (13 EditMode + 3 PlayMode), run headless by [CI](docs/CI.md) on every pull request
- Phases 1–2 verified running in-Editor; Phases 3–6 implemented (on-device ML / device-only paths are scaffolded behind interfaces)

## Why the rebuild

The previous prototype targeted a since-broken toolchain. The full Meta XR SDK, UnityGLTF, and NaughtyAttributes do **not** compile on Unity 6.4/6.5 (a hard `CS0619` on `Object.GetInstanceID()`), and those are non-LTS releases. This rebuild targets **Unity 6.3 LTS** (supported to Dec 2027, validated by every vendor SDK in the stack) and uses the **OpenXR** path — `com.unity.xr.meta-openxr` for Quest and the PICO OpenXR SDK for Pico — so a single codebase builds for both headsets. Each headset gets its own APK, because a build turns on only that vendor's OpenXR features (see [docs/BUILD.md](docs/BUILD.md)).


## Features

| Feature | What it provides |
|---|---|
| **Room understanding** | Combine room scan data and spatially anchored object pins. |
| **Perception seam** | Route image and conversation requests through configured services. |
| **Embodied companion** | Render an avatar with spatial context in Unity. |
| **Navigation status** | Walking and autonomous navigation remain work in progress. |

## How it fits together

Unity owns room capture, anchors and avatar rendering; perception and conversation use configured services. Navigation remains explicitly in progress.

```mermaid
flowchart LR
  Room[Quest room scan and anchors] --> Pins[Spatial object pins]
  Pins --> Context[Spatial context]
  Input[Voice or text] --> Agent[Configured agent backend]
  Context --> Agent
  Agent --> Avatar[Unity companion avatar]
  Agent -. navigation work in progress .-> Nav[Movement controller]
```

## Architecture

OpenXR-first: the C# never branches per vendor — only the enabled OpenXR feature group changes. Every platform capability sits behind a `XRScan.Core` interface with a capability flag and graceful fallback (e.g. consumer Pico exposes no per-app RGB frames, so live-camera scanning auto-degrades to scene-semantics pins).

| Assembly | Responsibility |
|---|---|
| `XRScan.Core` | Zero-vendor-dependency interfaces + DTOs (the compile firewall) |
| `XRScan.Platform` | AR Foundation camera / scene / room-mesh providers; `PlatformCapabilities` |
| `XRScan.Vision` | CV server client + `RemoteCvDetector`; on-device `SentisYoloDetector` (scaffold) |
| `XRScan.Mapping` | `ViewportRaycaster`, `SpatialPinManager` (2D detection → world pins) |
| `XRScan.Avatar` | Avatar load, gaze, gesture, viseme lip-sync, NavMesh locomotion |
| `XRScan.Voice` | STT / TTS (cloud + on-device Sentis/Piper scaffolds) |
| `XRScan.Llm` | LLM backend behind `ILlmBackend`: `OllamaBackend` (text + vision). OpenAI / Claude / CI-server backends are not written yet |
| `XRScan.Agent` | `ConversationOrchestrator` + `SceneKnowledgeBase` (grounds the LLM in scanned pins) |
| `XRScan.Export` | glTF export via glTFast + server sync |
| `XRScan.App` | `AppBootstrap` startup wiring + `ScanController` |
| `XRScan.Mocks` | Editor/test doubles so the whole app runs with no device/server |

### Pluggable intelligence (the "hybrid" seam)

Every stage is an interface — swap the implementation via config, no recompile:

- `IObjectDetector` → remote CV server (`RemoteCvDetector`) **or** on-device Sentis (`SentisYoloDetector`, a scaffold until a YOLO model is wired), auto-selected by `DetectorFactory`
- `ILlmBackend` → Ollama (`OllamaBackend`), or `MockLlmBackend` in mock mode. OpenAI, Claude and the Companion Intelligence server would each be another `ILlmBackend` implementation; none exists yet
- `ISpeechToText` / `ITextToSpeech` → cloud Whisper / TTS over OpenAI-compatible endpoints (`CloudWhisperStt`, `CloudTts`; the Companion Intelligence server can stand in) **or** on-device Sentis Whisper / Piper (scaffolds, no models committed)
- `ICameraProvider` → Quest passthrough (AR Foundation) with a Pico/desktop fallback

Endpoints and API keys are read from `ServerConfig` / `LlmConfig` / `VoiceConfig` ScriptableObjects via PlayerPrefs / environment variables — **never committed**.

## Getting started

1. Open with **Unity 6000.3.19f1** (with Android Build Support). Packages resolve automatically (RPM, uLipSync, PICO OpenXR and the Unity MCP bridge are pulled via git; glTFast comes from the Unity registry as an RPM dependency).
2. Open `Assets/XRScan/App/Scenes/XRScan_Main.unity` and press **Play** — it runs entirely on mocks (mock camera → detector → pin → avatar looks + speaks; push-to-talk voice Q&A). No device or server required.
3. To use real backends, set `useMocks = false` on `AppBootstrap` and fill in `ServerConfig` / `LlmConfig` / `VoiceConfig`.
4. Build for Quest 3 / Pico with **XRScan → Build → Quest 3** or **Pico** (one APK per headset). Each applies Android, IL2CPP, ARM64, Vulkan, min SDK 32 and switches on that headset's OpenXR features; see [docs/BUILD.md](docs/BUILD.md).

## Avatars

RPM / Wolf3D GLB avatars live in `Assets/XRScan/Avatar/Models/` (glTFast imports `.glb` at edit-time). `CI_Brad` carries the full **15 Oculus visemes** on `Wolf3D_Head`; swap avatars via `AvatarLoadController.avatarPrefab`.

## Tests

16 tests, run from **Window → General → Test Runner** or headless:

- `Assets/XRScan/Tests/EditMode` — 13 tests: bounding-box math, scan-ray math (camera intrinsics → world ray), the spatial-pin store (dedup + events), and the mock-detector contract.
- `Assets/XRScan/Tests/PlayMode` — 3 scanning-pipeline tests: a detection at a known screen position must land a pin at a known world position, and repeated scans of one object must dedup to one pin.

[CI](docs/CI.md) runs both suites on every pull request, then builds the Quest APK. The same commands work locally (listed in docs/CI.md).

---

# Original Design Brief

_(Preserved from the project's original planning notes.)_

BUILD A CLONE OF GOOGLE ASTRA / META AI WITH LOCAL EMBODIED AGENTS.

- Existing web tech (RPM / Whisper / Piper): https://github.com/companionintelligence/CI-Embodied-Companion-Web
- Get a passthrough-camera vision loop running, put an RPM avatar in the MR scene, make it talk with whisper/piper visemes like the web version.
- The avatar should look at the user, gesture, and walk around the space, and look at objects the user sees.
- Use a similar android camera system for all builds. Build in a pluggable way so it's compatible with any AI API backend — the Companion Intelligence servers, OpenAI, Claude, or anything else.
- Build for Mobile, XREAL, and Quest 3.

Reference projects: [QuestCameraKit / QuestVisionKit](https://github.com/xrdevrob/QuestCameraKit), [UXR.QuestCamera](https://github.com/Uralstech/UXR.QuestCamera/), [Unity-PassthroughCameraApiSamples](https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples), [xr-ai-text-to-speech](https://github.com/rikturnbull/xr-ai-text-to-speech), [Unity-MetaXR-AI-Florence2](https://github.com/lucas-martinic/Unity-MetaXR-AI-Florence2).

Spec doc: https://docs.google.com/document/d/1b7BSgvqpc9Hzb5csGv7V6cEOdlgvahUWlV_8k47lfqM/edit

<!-- companion-hub-readme-deep-review -->

## Editor and target requirements

| Area | Requirement or state |
|---|---|
| Unity project | Open with the Unity version specified in `ProjectSettings/ProjectVersion.txt` |
| Target | Mixed-reality runtime; the README describes Quest and XREAL use |
| Intelligence | Pluggable backend seam; configure the selected local or remote service |
| Validation | Run the documented test suite; device-specific behavior still needs target hardware validation |

```mermaid
flowchart LR
  Device[XR device and input] --> Unity[Unity companion runtime]
  Unity --> UI[Avatar and spatial UI]
  Unity --> Intelligence[Configured intelligence provider]
  Intelligence --> Unity
```

The project is a rebuild with an explicitly documented status. Treat the original design brief as rationale, and the current status/getting-started sections as implementation guidance.


## License

[PolyForm Noncommercial License 1.0.0](https://polyformproject.org/licenses/noncommercial/1.0.0) — free for noncommercial use and research. See [LICENSE](LICENSE) and the [License FAQ](docs/License-FAQ.md) for what that means in practice.
