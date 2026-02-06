Private and Confidential

# XREAL Unity

## Quick Start (Ollama + Quest 3 / XREAL)

1. Install and run Ollama on your desktop.
   ```bash
   ollama pull llava:7b
   ollama serve
   ```
2. Put the XREAL UPM package tarball at `Packages/com.xreal.xr.tar.gz`.
3. Open the project in Unity `6000.2.0b9`.
4. Open `Assets/Scenes/HomeMenu.unity`.
5. Add `XREALVisionSceneSetup` to any GameObject in the scene.
6. In the inspector, set:
   - `Ollama Server IP` to your desktop's LAN IP.
   - `Ollama Server Port` to `11434`.
7. For Quest/XREAL Android builds, keep the included cleartext config files: `Assets/Plugins/Android/AndroidManifest.xml` and `Assets/Plugins/Android/res/xml/network_security_config.xml`.

Notes:
- The headset cannot reach `127.0.0.1` on your desktop. Use the desktop LAN IP.
- If you only want Ollama and no OpenAI usage, keep `SpeechToText` and `TextToSpeech` disabled or switch `TextToSpeech` to `Unity` in the inspector.

<img width="2048" height="1365" alt="AR Headset" src="https://github.com/user-attachments/assets/32533133-bb8e-4d7f-b344-990db11e3aee" />
<img width="2048" height="911" alt="AR Use Caee" src="https://github.com/user-attachments/assets/6e40b940-bd6a-4e23-a1af-004c322b6679" />

https://github.com/user-attachments/assets/3e425d58-8093-4978-b5f8-083bd84c2594


https://docs.google.com/document/d/1b7BSgvqpc9Hzb5csGv7V6cEOdlgvahUWlV_8k47lfqM/edit?userstoinvite=tristanskyhook@gmail.com&sharingaction=manageaccess&role=writer&tab=t.0

Existing Tech on RPM / Whisper / Piper
https://github.com/companionintelligence/CI-Embodied-Companion-Web
New one we want to build
https://github.com/companionintelligence/CI-Embodied-Companion-Unity  

RPM Command Pallate 

CI Commercial 2025-7-10 Media Scratchpad 
GOAL

BUILD A CLONE OF GOOGLE ASTRA / META AI WITH LOCAL EMBODIED AGENTS!
Project Astra - Google DeepMind 

hAI! Friend MR on Meta Quest | Quest VR Games 



Get QuestVisionKit/ /Samples/5 running with openai key
Put rpm avatar in the MR scene in a reasonable place
Make rpm avatar talk with whisper/piper w/ visemes like in web version
Like this https://github.com/companionintelligence/CI-Embodied-Companion-Web/blob/main/chat/stt-whisper.js
THERE IS OTHER EXAMPLES OF whisper/piper w/ visemes IN THE UNITY ASSET STORE
RT-Voice PRO | Audio | Unity Asset Store 
SALSA LipSync Suite | Animation Tools | Unity Asset Store 
SpeechBlend LipSync | Animation Tools | Unity Asset Store 
API for ChatGPT - Easy AI Integration 
Dialogue System for Unity Addon for OpenAI, ElevenLabs & Other Generative AI | AI-ML Integration 

expand our embodied agent interface with Unity. Unity offers us unique abilities to build mixed reality interactions between an embodied agent and the real world using multimodal vision systems in models like LLAMA 3.2. 

build for Mobile, XREAL, and Quest 3. 

The concept is to have a Ready Player Me avatar read with animations and Viseme Voice puppeteering. 

Avatar should be able to look at the user, gesture, and walk around the space. 

Possibly even looking at objects the user sees, looking to combine existing Unity Prefab scenes from different assets that already exist in a unified way. 

Also looking to use a similar android camera system for all builds. The idea is to build this in a pluggable way so that it is compatible with AI API backend, including our companion, intelligence servers or open AI or Claude or garage or anything else.

We already have an existing web implementation based on 3JS 

System components. 
On premises server backend. Already completed.  
Uses an open AI API interface. See example.
RPM character puppet. 
Body animation, mixer. Room walking controller. 
Augmented reality controller. 
Camera access vision system. 
Voice prompting system. 

https://github.com/rikturnbull/xr-ai-text-to-speech 
https://github.com/lucas-martinic/Unity-MetaXR-AI-Florence2 
https://github.com/nigelhartm/MetaPCARoboflow 
https://xraihack.notion.site/XRAI-Hack-in-Cologne-22ad7964cb7c81409748ca5322599341 

TykeAI | AI powered autonomous virtual companions | Patreon 

Passthrough Camera Access For Quest 3 & Quest 3S Now Available!
https://github.com/Uralstech/UXR.QuestCamera/

QUEST 3 PASSTHROUGH EXAMPLE 1 - YOLO V2 RUNNING LOCAL
https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples/blob/main/Media/ObjectDetectionSentis.gif

QUEST 3 PASSTHROUGH EXAMPLE 2 - OPENAI RUNNING IN THE CLOUD
GitHub - xrdevrob/QuestCameraKit: QuestVisionKit is a collection of template and reference projects demonstrating how to use Meta Quest’s new Passthrough Camera API for advanced AR/VR vision, tracking, and shader effects.

EXACT SPOT IT CALLS THE OPENAI API TO BE SWAPPED WITH OUR API
https://github.com/xrdevrob/QuestCameraKit/blob/main/Unity-QuestVisionKit/Assets/Samples/5%20ImageLLM/Scripts/ImageOpenAIConnector.cs#L170

COMP CODE FOR OUR API RUNNING IN WEB VERSION
https://github.com/companionintelligence/CI-Embodied-Companion-Web/blob/main/index.js#L185 

RPM UNITY SDK
Ready Player Me Avatar and Character Creator | Game Toolkits | Unity Asset Store 
Getting Started With Ready Player Me Avatars Integration in Unity 
Get Started With Ready Player Me's Game-Ready Avatar Animation Library in Unity 

XREAL SDK
Download


MULTIPLAYER BOILERPLATE
https://discussions.unity.com/t/new-mr-multiplayer-tabletop-template-now-available/1595933 


Similar Work 
https://github.com/dilmerv/MRDrawingDemo 
GitHub - TakashiYoshinaga/QuestArUcoMarkerTracking 
https://github.com/VampyreVK/DrawMX 
https://github.com/Aupuma/QuestImageTracking 
https://github.com/hsp-iit/handCamera 
https://github.com/CsRic/Augmented-Billiards 
https://github.com/suibiweng/MRBrush/commit/8a28b4095ffddf421f6cc46afe7b2931840a246e 
https://github.com/dilmerv/MRDrawingDemo 
GitHub - doughtmw/AR-Ultrasound-Training 

Project Astra - Google DeepMind 

https://x.com/ehuanglu/status/1907046194238353419?s=46 
https://x.com/ueda406/status/1904771581135774042 
https://x.com/mechpil0t/status/1902879317803294773 






hAI Friend MR with Vision (Meta Quest Passthrough Camera API) & GPT‑4.5 – Coming Soon!  
StudyBuddy GPT4 o Multilingual Conversational AI with Animated Avatar student learning helper app 
Demo - AI Talking Multilingual Banking / Insurance Kiosk  


## Codex + MCP Setup

- Project config: `.codex/config.toml` (used by Codex and shared with IDEs).
- VS Code MCP config: `.vscode/mcp.json` for Copilot Agent.
- If Codex doesn't pick up project settings, open the project in Codex and trust it.

## Local Tests

Run all tests with:

```bash
./scripts/unity-tests.sh
```

Override Unity path if needed:

```bash
UNITY_BIN="/Applications/Unity/Hub/Editor/6000.3.7f1/Unity.app/Contents/MacOS/Unity" ./scripts/unity-tests.sh
```

## GitHub Actions

The workflow is in `.github/workflows/unity-tests.yml` and uses GameCI.
Set one of the following secret sets in the repo settings:
- `UNITY_LICENSE` (recommended for personal license activation)
- or `UNITY_EMAIL`, `UNITY_PASSWORD`, `UNITY_SERIAL` (for serial-based activation)

Artifacts are uploaded to `TestResults` on every run.

## Quick Iteration Tools

- Dev scene: `Assets/Scenes/Dev.unity`
- Menu shortcuts in Unity:
  - `CI/Open Dev Scene`
  - `CI/Run EditMode Tests`
  - `CI/Run PlayMode Tests`
  - `CI/Open Test Results Folder`

## Performance Smoke Test

PlayMode test `PerformanceSmokeTest` enforces a minimum 15 FPS average over 120 frames. Adjust in `Assets/Tests/PlayMode/PerformanceSmokeTest.cs`.
