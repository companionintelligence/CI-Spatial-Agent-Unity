# Companion Intelligence for XREAL - Embodied AI Assistant & Life Logger

## 1. Project TODO List

### 🎯 Core Development Tasks

| Category | Task | Subtasks | Priority | Status |
|----------|------|----------|----------|--------|
| **Project Setup** | Initialize Unity XREAL Project | Set up Unity 2022.3 LTS project; Configure XREAL XR project settings; Install XREAL Unity SDK; Set up version control (Git) | High | ⭕ |
| **XREAL SDK Integration** | XREAL Development Environment | Install XREAL Unity SDK; Configure build settings for XREAL devices; Set up XREAL developer account and device registration; Test SDK functionality | High | ⭕ |
| **Input Subsystem** | XREAL Audio Manager | Implement audio capture for XREAL devices; Configure microphone access via XREAL SDK; Handle audio permissions; Test audio quality on Beam Pro and Ultra | High | ⭕ |
| **Input Subsystem** | XREAL Camera Manager | Implement XREAL camera access; Configure RGB camera feed for Beam Pro; Implement passthrough camera for Ultra; Handle camera permissions and privacy | High | ⭕ |
| **Input Subsystem** | XREAL Interaction Manager | Implement XREAL controller support; Configure touch input for Beam Pro; Set up hand tracking (if supported); Test interaction responsiveness | Medium | ⭕ |
| **Local Processing** | Speech-to-Text Integration | Install whisper.unity package; Implement local STT processing optimized for XREAL; Optimize for Android-based XREAL OS; Test accuracy with XREAL audio | High | ⭕ |
| **Local Processing** | Text-to-Speech Integration | Install piper.unity package; Implement local TTS synthesis; Optimize audio output for XREAL speakers; Generate visemes for lip sync | High | ⭕ |
| **Communication Layer** | Ollama Connector | Implement HTTP client service for XREAL devices; Create JSON payload formatting; Handle Base64 image encoding; Implement error handling and retry logic | High | ⭕ |
| **Communication Layer** | WiFi Network Discovery | Auto-discover Ollama server on local WiFi network; Manual server configuration UI for XREAL; Connection status monitoring; Handle network switching | Medium | ⭕ |
| **Avatar System** | Ready Player Me Integration | Install RPM SDK; Implement avatar loading system optimized for XREAL; Create avatar customization UI for XREAL display; Handle avatar caching on device | Medium | ⭕ |
| **Avatar System** | Lip Sync Implementation | Install SALSA LipSync Suite; Connect TTS audio to lip sync; Calibrate mouth movements for XREAL display; Test across different avatar models | Medium | ⭕ |
| **Avatar System** | Animation Controller | Implement idle animations; Create gesture system; Implement look-at behavior for XREAL tracking; Add procedural animations optimized for XREAL | Medium | ⭕ |
| **Server Setup** | Ollama Server Configuration | Install Ollama on local network server; Download multi-modal LLM (Llama 3.2 Vision); Configure API endpoints; Test server performance with XREAL clients | High | ⭕ |
| **Memory System** | Conversation Persistence | Design conversation history format; Implement memory storage interface; Create conversation recall system; Test memory retrieval accuracy | Medium | ⭕ |
| **Memory System** | Life Logging Database | Set up local vector database (ChromaDB); Implement memory extraction pipeline; Create search and retrieval system; Design privacy-preserving storage | Low | ⭕ |

### 🥽 XREAL Device-Specific Implementation

| Device | Task | Subtasks | Priority | Status |
|--------|------|----------|----------|--------|
| **XREAL Beam Pro** | Beam Pro Integration | Configure Unity for Beam Pro Android environment; Implement touchscreen UI controls; Optimize for Beam Pro's processing capabilities; Test camera and audio integration | High | ⭕ |
| **XREAL Ultra** | Ultra Integration | Configure Unity for XREAL Ultra; Implement advanced tracking features; Optimize for Ultra's enhanced capabilities; Test passthrough and spatial awareness | High | ⭕ |
| **Both Devices** | Cross-Device Compatibility | Implement device detection and configuration; Create adaptive UI for different form factors; Test feature parity between devices; Handle device-specific limitations | Medium | ⭕ |

### 🧪 Testing & Optimization

| Category | Task | Subtasks | Priority | Status |
|----------|------|----------|----------|--------|
| **Performance** | XREAL Optimization Pass | Profile Unity performance on XREAL devices; Optimize network communication for mobile processors; Test battery life impact; Memory usage optimization for Android | Medium | ⭕ |
| **User Experience** | XREAL UX Testing | Test conversation flow on XREAL displays; Validate multi-modal interactions; Test avatar presence and engagement; Collect user feedback from XREAL users | Medium | ⭕ |
| **Privacy & Security** | Security Audit | Validate local-only data processing; Test network security on XREAL devices; Implement data encryption; Create privacy documentation | High | ⭕ |

### 📚 Documentation & Deployment

| Category | Task | Subtasks | Priority | Status |
|----------|------|----------|----------|--------|
| **Documentation** | XREAL Technical Documentation | API documentation for XREAL SDK integration; Setup and installation guide for XREAL development; Troubleshooting guide for XREAL-specific issues; Architecture deep-dive | Low | ⭕ |
| **Deployment** | XREAL Build & Distribution | Configure build pipelines for XREAL devices; Create XREAL app packages; Set up XREAL testing environments; Prepare release artifacts for XREAL Store | Low | ⭕ |

**Legend:** ⭕ Not Started | 🟡 In Progress | ✅ Complete | ❌ Blocked

---

This document outlines the technical architecture for creating a persistent, embodied AI agent designed to function as a personal assistant and life-logging tool specifically for XREAL devices. The agent will manifest as a 3D avatar within the user's mixed reality environment using XREAL Beam Pro and XREAL Ultra headsets, capable of continuous perception, contextual conversation, and interaction.

The core objective is to build a system that transcends simple command-response interactions. This agent will maintain a memory of past conversations and observations, enabling it to act as an intelligent companion that understands the user's personal context. This "life-logging" capability positions the agent to not only answer questions but also to recall information, summarize events, and assist with tasks based on a persistent understanding of the user's world.

The architecture is designed specifically for XREAL devices (Beam Pro and Ultra) using Unity and the XREAL Unity SDK. All cognitive and perceptual processing will be handled by a local, on-network Ollama server. This client-server model ensures user privacy by keeping all data within the local network while leveraging the full power of large, multi-modal language models for sophisticated reasoning and visual understanding.

## 2. XREAL Device Support & User Experience

This project focuses exclusively on XREAL devices to provide an optimized, cohesive experience tailored to XREAL's unique capabilities and form factors.

### 2.1. XREAL Beam Pro Integration

The XREAL Beam Pro serves as the primary development target, offering:
- **Android-based Development Environment**: Leveraging Unity's Android build pipeline with XREAL-specific optimizations
- **Touchscreen Interface**: Native touch controls for avatar interaction and settings management
- **Portable Form Factor**: Optimized for on-the-go AI assistance and life logging
- **Camera Integration**: RGB camera access for environmental awareness and visual queries

### 2.2. XREAL Ultra Advanced Features

The XREAL Ultra provides enhanced capabilities for immersive AI interactions:
- **Advanced Tracking**: Superior spatial awareness and head tracking for more natural avatar positioning
- **Enhanced Display**: Higher resolution and field of view for better avatar rendering
- **Passthrough Technology**: Real-time environmental integration for seamless AR experiences
- **Performance Optimization**: Dedicated processing power for smoother AI interactions

### 2.3. Unified XREAL Experience

Both devices share core functionality while adapting to their specific capabilities:
- **Consistent Avatar Behavior**: Same AI personality and conversation continuity across devices
- **Adaptive UI**: Interface automatically adjusts to device capabilities and form factor
- **Cross-Device Memory**: Conversation history and life logs persist when switching between devices
- **Performance Scaling**: Automatic optimization based on device capabilities

## 3. Core Architectural Principles

* **Local-First Privacy**: All data, including audio recordings, visual captures, and conversation logs, remains within the user's local network. The Unity client communicates directly with a local Ollama server, eliminating reliance on cloud services for core AI processing and ensuring maximum user privacy.
* **Centralized Cognition**: A single, powerful multi-modal Large Language Model (LLM) running on the Ollama server acts as the agent's brain. This simplifies the XREAL client application, which offloads complex reasoning, visual analysis, and memory management to the server.
* **XREAL-Optimized Performance**: The Unity client is specifically optimized for XREAL's Android-based architecture, ensuring smooth performance and efficient battery usage on both Beam Pro and Ultra devices.
* **Adaptive Interface Design**: The client automatically detects the XREAL device type and adapts the user interface, interaction methods, and performance settings accordingly.

## 4. System Architecture Overview

The system is divided into two primary components: the **Unity XREAL Client**, which runs on XREAL devices, and the **Local Network Server**, which runs Ollama and the multi-modal LLM.

### 4.1. High-Level Architecture Diagram

```mermaid
graph TD
    subgraph Unity XREAL Client (Beam Pro / Ultra)
        A[User] -- Speaks & Looks --> B(XREAL Input Subsystem);
        B -- Audio Stream --> C{Speech-to-Text (Local Whisper)};
        B -- Image Stream --> D{XREAL Camera Manager};
        C -- Transcribed Text --> E[Communication Layer];
        D -- Encoded Image --> E;
        E -- JSON (Text + Image) --> F{Ollama REST API Request};
        G -- Parsed Text --> H{Text-to-Speech (Local Piper)};
        H -- Audio Stream & Visemes --> J[Avatar Expression];
        J -- Lip Sync & Animation --> K(Ready Player Me Avatar);
        K -- Speaks & Gestures --> A;
    end

    subgraph "Local Server (PC/Mac/Linux)"
        L(Ollama Server) -- Processes Request --> M((Multi-Modal LLM));
        M -- Generates Response --> L;
    end

    F -- HTTP POST via WiFi --> L;
    L -- HTTP Response --> G;

    style A fill:#f9f,stroke:#333,stroke-width:2px
    style K fill:#f9f,stroke:#333,stroke-width:2px
    style M fill:#9f9,stroke:#333,stroke-width:2px
```

### 4.2. Unity XREAL Client Components

The client application, built in Unity with the XREAL SDK, is responsible for all user-facing interactions and communication with the local server.

- **XREAL Input Subsystem:**
  - **XREAL Audio Manager:** Captures audio from XREAL device microphones, optimized for the specific audio hardware of Beam Pro and Ultra devices
  - **XREAL Camera Manager:** Accesses XREAL camera feeds (RGB camera on Beam Pro, passthrough cameras on Ultra) using XREAL SDK APIs for visual analysis
  - **XREAL Interaction Manager:** Handles device-specific input including touch controls on Beam Pro, controller input, and head tracking using XREAL SDK

- **Local Processing Subsystem:**
  - **Speech-to-Text (STT):** Utilizes `whisper.unity` optimized for Android/XREAL architecture to transcribe user speech on-device, providing fast response and reducing network traffic
  - **Text-to-Speech (TTS):** Employs `piper.unity` to synthesize LLM responses into audio, optimized for XREAL audio output systems

- **Communication Layer:**
  - **Ollama Connector:** A dedicated C# service optimized for XREAL's WiFi capabilities, formatting and sending requests to the Ollama server. Constructs JSON payloads containing user prompts, conversation history, and Base64-encoded images from XREAL cameras.

- **XREAL Embodiment Subsystem:**
  - **Avatar Manager:** Uses Ready Player Me SDK with XREAL display optimizations for 3D avatar rendering
  - **Lip Sync Manager:** Integrates SALSA LipSync Suite optimized for XREAL audio output and display capabilities
  - **XREAL Animation Controller:** Manages avatar animations with XREAL-specific optimizations for head tracking and spatial awareness

### 4.3. Local Network Server Components

The server runs on a computer within the same local WiFi network as the XREAL device, responsible for running the Ollama service.

- **Ollama Service:** The core application that hosts and serves large language models, exposing a REST API for interaction with XREAL clients
- **Multi-Modal LLM:** A powerful model capable of processing both text and images, such as Llama 3.2 Vision or Llava, handling all cognitive tasks for XREAL interactions
- **Persistent Memory (Optional Extension):** The Ollama server can be extended with a Python script to log conversations and extracted information into a local database (SQLite, ChromaDB) for life-logging capabilities

## 5. Core Interaction Flowchart

This flowchart details the process for a multi-modal query on XREAL devices, representing the most complex interaction loop.

```mermaid
flowchart TD
    A[User asks: "What is this object in front of me?"] --> B(XREAL Client: Mic records audio);
    B --> C{whisper.unity transcribes audio to text};
    C --> D[XREAL Client: Camera captures image frame];
    D --> E{Image encoded to Base64 string};
    subgraph "JSON Payload to Ollama"
        direction LR
        F1["prompt: 'What is this...'"]
        F2["images: ['...base64...']"]
        F3["history: [...]"]
    end
    E --> F1;
    E --> F2;
    C --> F3;
    F1 & F2 & F3 --> G((Ollama Server));
    G --> H{Multi-Modal LLM processes text and image};
    H --> I[LLM generates response text];
    I --> J[XREAL Client: Parse response text];
    J --> K{piper.unity generates speech audio};
    K --> L[Audio stream plays through XREAL speakers];
    K --> M{SALSA LipSync animates avatar mouth};
    L & M --> N(Avatar responds: "That appears to be a coffee mug.");
```

## 6. XREAL Development Tooling & SDK Reference

### 6.1. Core Development & XREAL Platform SDKs

| Tool / SDK | Purpose | Link |
| :--- | :--- | :--- |
| **Unity Engine** | Primary development environment for XREAL client application. | [unity.com](https://unity.com/) |
| **XREAL Unity SDK** | Essential SDK for XREAL device integration and hardware access. | [developer.xreal.com](https://developer.xreal.com/download) |
| **Android SDK & NDK** | Required for building to XREAL's Android-based platform. | [developer.android.com](https://developer.android.com/studio) |

### 6.2. AI, Avatar, and Animation Assets

| Asset / Tool | Purpose | Link |
| :--- | :--- | :--- |
| **Ollama** | Runs large language models on the local network server. | [ollama.com](https://ollama.com/) |
| **whisper.unity** | On-device Speech-to-Text (STT) optimized for Android/XREAL. | [GitHub](https://github.com/Macoron/whisper.unity) |
| **piper.unity** | On-device Text-to-Speech (TTS) using Piper and Unity Sentis. | [GitHub](https://github.com/Macoron/piper.unity) |
| **Ready Player Me SDK** | For creating and loading customizable avatars optimized for XREAL displays. | [GitHub](https://github.com/readyplayerme/rpm-unity-sdk-core) |
| **SALSA LipSync Suite** | Real-time, audio-based lip synchronization optimized for XREAL. | [Unity Asset Store](https://assetstore.unity.com/packages/tools/animation/salsa-lipsync-suite-148442) |
| **Unity Animation Rigging** | For procedural animations and XREAL head tracking integration. | [Unity Docs](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.1/manual/index.html) |

### 6.3. Key Reference Projects & XREAL Examples

| Project / Resource | Description |
| :--- | :--- |
| **XREAL Unity Samples** | Official XREAL Unity SDK examples and starter projects for device integration. |
| **unity-mcp-ollama** | Demonstrates connecting Unity to local Ollama instance, adaptable for XREAL architecture. |
| **XREAL Developer Documentation** | Comprehensive guides for XREAL device development, SDK usage, and best practices. |
| **UnityWebRequest for Android** | Unity networking examples specifically for Android platform, applicable to XREAL devices. |

## 7. XREAL-Specific Development Considerations

### 7.1. Device-Specific Optimizations

- **Performance Targeting**: Optimize for XREAL's Android-based architecture and mobile processors
- **Battery Efficiency**: Implement power management strategies for extended use during life-logging sessions
- **Thermal Management**: Monitor and adapt performance based on device temperature
- **Memory Management**: Efficient memory usage for avatar rendering and AI processing

### 7.2. XREAL SDK Integration Points

- **Device Detection**: Automatically detect Beam Pro vs Ultra and adapt features accordingly
- **Camera Access**: Use XREAL SDK APIs for optimal camera performance and quality
- **Display Optimization**: Leverage XREAL's display capabilities for avatar rendering
- **Audio Integration**: Utilize XREAL's spatial audio capabilities for immersive AI interactions

### 7.3. Development Workflow

- **XREAL Developer Account**: Required for accessing SDK and device testing
- **Unity Version**: Recommend Unity 2022.3 LTS for XREAL compatibility
- **Build Pipeline**: Configure for Android with XREAL-specific optimizations
- **Testing Strategy**: Test on both Beam Pro and Ultra devices throughout development

### 7.4. Future Expansion Possibilities

While this project focuses exclusively on XREAL devices, the architecture is designed to be modular. Future versions could potentially expand to support:

- Additional XREAL device models as they become available
- Enhanced XREAL-specific features like advanced spatial mapping
- Integration with XREAL's evolving SDK capabilities
- Performance optimizations for next-generation XREAL hardware