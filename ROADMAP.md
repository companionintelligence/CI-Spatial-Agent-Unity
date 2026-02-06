# CI Embodied Companion Unity Roadmap

**Goal**  
Build a local‑first, embodied XR agent that feels like a clone of Project Astra / Meta AI, with a Ready Player Me avatar, multimodal perception, and real‑world interaction across Quest 3, XREAL, and mobile.

**Vision**  
An embodied agent that can see (passthrough / camera), listen, speak with expressive visemes, look at the user, gesture, move in space, and interact with real‑world objects using a pluggable AI backend (OpenAI / Claude / internal services / other).

## Milestones

**M0 — Foundations**  
1. New Unity repo (this) built around XR Plugin Management + XREAL SDK  
2. Project settings clean, Android target switchable, XR loader assignment automated  
3. Repo docs + CI test workflow in place  

**M1 — QuestVisionKit Sample 5 Working**  
1. Get `QuestVisionKit/Samples/5` running with API key  
2. Swap the OpenAI call site to our backend API  
3. Validate passthrough camera + image LLM inference path  

**M2 — RPM Avatar in MR Scene**  
1. Load Ready Player Me avatar  
2. Place avatar in MR scene with reasonable scale + anchoring  
3. Basic look‑at user + idle gestures  

**M3 — Voice Loop (Whisper + Piper + Visemes)**  
1. Speech‑to‑text using Whisper  
2. Text‑to‑speech using Piper  
3. Viseme‑driven lip sync (match web behavior)  
4. On‑device/offline fallback where feasible  

**M4 — Unified Embodied Agent Interface**  
1. Pluggable AI backend interface (OpenAI, Claude, CI servers, etc.)  
2. Shared Android camera access for Quest/XREAL/Mobile  
3. Modular “capabilities” system (vision, speech, gestures, movement)  

**M5 — Cross‑Platform Targets**  
1. Quest 3 MR  
2. XREAL Android (Beam Pro / compatible)  
3. Mobile AR (Android first)  

**M6 — Multimodal Interaction**  
1. Object grounding (look at objects the user sees)  
2. Gesture + movement choreography  
3. Scene composition from multiple prefab sources  

## System Components

1. On‑prem backend (already completed)  
2. OpenAI‑compatible API interface  
3. RPM character puppet + animation mixer  
4. Room‑scale walking controller  
5. MR / AR controller  
6. Vision system (camera + inference)  
7. Voice pipeline (Whisper + Piper + viseme driving)  

## Immediate Tasks (Next 2–3 Weeks)

1. Get QuestVisionKit Sample 5 running with OpenAI key  
2. Replace OpenAI call with CI backend API  
3. Import RPM avatar, place in MR scene  
4. Add Whisper + Piper pipeline, drive visemes  
5. Validate on Quest 3 + XREAL  

## Research / Evaluation Backlog

1. Whisper/Piper viseme examples in Unity  
2. RT‑Voice PRO vs SALSA vs SpeechBlend  
3. Dialogue System + OpenAI/ElevenLabs add‑ons  
4. Quest passthrough camera APIs and sample repos  
5. Multimodal vision + robotics examples  

## References (raw links)

```
Existing web implementation:
https://github.com/companionintelligence/CI-Embodied-Companion-Web

Unity repo (this):
https://github.com/companionintelligence/CI-Embodied-Companion-Unity

QuestVisionKit / Sample 5 call site to swap:
https://github.com/xrdevrob/QuestCameraKit/blob/main/Unity-QuestVisionKit/Assets/Samples/5%20ImageLLM/Scripts/ImageOpenAIConnector.cs#L170

Web API integration reference:
https://github.com/companionintelligence/CI-Embodied-Companion-Web/blob/main/index.js#L185

Whisper/Piper example:
https://github.com/companionintelligence/CI-Embodied-Companion-Web/blob/main/chat/stt-whisper.js

Other references:
https://github.com/rikturnbull/xr-ai-text-to-speech
https://github.com/lucas-martinic/Unity-MetaXR-AI-Florence2
https://github.com/nigelhartm/MetaPCARoboflow
https://github.com/Uralstech/UXR.QuestCamera/
https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples

Assorted inspiration:
https://x.com/ehuanglu/status/1907046194238353419
https://x.com/ueda406/status/1904771581135774042
https://x.com/mechpil0t/status/1902879317803294773
```

