# Companion Intelligence for XREAL - Embodied AI Assistant & Life Logger

## 📋 Project Overview

This document outlines the technical architecture for creating a persistent, embodied AI agent designed to function as a personal assistant and life-logging tool specifically for XREAL devices. The agent will manifest as a 3D avatar within the user's mixed reality environment using XREAL Beam Pro and XREAL Ultra headsets, capable of continuous perception, contextual conversation, and interaction.

### 🎯 Core Objectives

- **Persistent AI Companion**: Build a system that transcends simple command-response interactions, maintaining memory of past conversations and observations
- **Life-Logging Capabilities**: Enable intelligent recall of information, event summarization, and task assistance based on persistent understanding of the user's world
- **XREAL-Optimized Experience**: Leverage XREAL's unique capabilities and form factors for immersive AI interactions
- **Local-First Privacy**: All data processing occurs within the user's local network, ensuring maximum privacy and security

## 🗃️ Project TODO List 

Use example 5 of this repo as a guide https://github.com/xrdevrob/QuestCameraKit/tree/main/Unity-QuestVisionKit/Assets/Samples/5%20ImageLLM

This ImageLLM unity scene is built for the quest 3 and uses openai
We need to create a similar tool using the xreal sdk and ollam

Reminder of architecture:

Ask Ollama 's vision model (or any other multi-modal LLM) for context of your current scene.

We use stt tts API to create a command. We then send this command together with a screenshot to the Vision model. Lastly, we get the response back and use the Text to speech API to turn the response text into an audio file in Unity to speak the response. 

The user can select different speakers, models, and speed. For the command we can add additional instructions for the model, as well as select an image, image & text, or just a text mode. The whole loop takes anywhere from 2-6 seconds, depending on the internet connection.

#### Research Examples
- [ ] **1** Review the example XREAL projects 
- [ ] **1.1** Create a reference to needed Code in the example projects

#### Review
- [ ] **2.1** Review the current code structure 
- [ ] **2.2** create a new Unity initial scene

#### 🎤 Audio & Visual Input Systems
- [ ] **3.1** Research XREAL microphone API specifications
- [ ] **3.2** Create AudioManager MonoBehaviour script
- [ ] **3.3** Implement microphone permission handling for Android
- [ ] **3.4** Configure audio capture with optimal settings (sample rate, channels)
- [ ] **3.5** Implement audio buffer management for continuous recording
- [ ] **3.6** Add noise reduction and audio preprocessing
- [ ] **3.7** Create audio visualization for debugging
- [ ] **3.8** Test audio quality on both Beam Pro and Ultra
- [ ] **3.9** Implement background audio capture for life-logging
- [ ] **3.10** Add privacy controls for audio recording
- [ ] **4.1** Study XREAL camera API documentation
- [ ] **4.2** Create CameraManager script for RGB and passthrough cameras
- [ ] **4.3** Implement camera permission handling
- [ ] **4.4** Configure camera resolution and frame rate optimization
- [ ] **4.5** Implement frame capture and buffering system
- [ ] **4.6** Add image preprocessing (resizing, format conversion)
- [ ] **4.7** Create camera preview UI for debugging
- [ ] **4.8** Implement continuous background image capture
- [ ] **4.9** Add privacy indicators for camera usage
- [ ] **4.10** Test camera performance on both device types

#### 🧠 AI Analysis & Processing
- [ ] **5.1** Design object detection pipeline architecture
- [ ] **5.2** Create ObjectDetectionManager script
- [ ] **5.3** Implement continuous frame analysis queue
- [ ] **5.4** Add object classification and confidence scoring
- [ ] **5.5** Create object persistence and tracking across frames
- [ ] **5.6** Implement object database for known items
- [ ] **5.7** Add semantic labeling for detected objects
- [ ] **5.8** Create object interaction history tracking
- [ ] **5.9** Test object detection accuracy in various environments
- [ ] **5.10** Optimize detection performance for XREAL devices
- [ ] **20.1** Create ContinuousAnalysisManager script
- [ ] **20.2** Implement frame-by-frame image analysis queue
- [ ] **20.3** Add audio stream analysis for ambient sounds
- [ ] **20.4** Create multi-modal data fusion system
- [ ] **20.5** Implement context switching and attention management
- [ ] **20.6** Add proactive suggestion and notification system
- [ ] **20.7** Create analysis result caching and optimization
- [ ] **20.8** Implement analysis scheduling based on device state
- [ ] **20.9** Test continuous analysis impact on battery life
- [ ] **20.10** Add user controls for analysis intensity

#### 🗣️ Speech Processing
- [ ] **10.1** integrate whisper or another STT / TTS package
- [ ] **10.2** Configure STT / TTS models for XREAL Android architecture
- [ ] **10.3** Implement real-time audio streaming to STT
- [ ] **10.4** Add language detection and multi-language support
- [ ] **10.5** Optimize STT performance for mobile processors
- [ ] **10.6** Implement continuous speech recognition
- [ ] **10.7** Add speaker diarization for multi-person conversations
- [ ] **10.8** Create STT accuracy monitoring and feedback
- [ ] **10.9** Test STT performance in noisy environments
- [ ] **10.10** Implement offline STT capabilities
- [ ] **11.1** Download and integrate piper.unity package
- [ ] **11.2** Configure Piper voice models for natural speech
- [ ] **11.3** Implement TTS audio output optimization
- [ ] **11.4** Add voice customization options
- [ ] **11.5** Generate visemes and phonemes for lip sync
- [ ] **11.6** Implement emotion and tone modulation
- [ ] **11.7** Add SSML support for advanced speech control
- [ ] **11.8** Optimize TTS latency for real-time conversation
- [ ] **11.9** Test TTS quality on XREAL audio hardware
- [ ] **11.10** Implement background TTS processing

#### 🌐 Communication & AI Backend
- [ ] **12.1** Create OllamaClient script with HTTP communication
- [ ] **12.2** Implement JSON payload formatting for multi-modal requests
- [ ] **12.3** Add Base64 image encoding for visual queries
- [ ] **12.4** Implement conversation history management
- [ ] **12.5** Add retry logic and error handling
- [ ] **12.6** Create connection pooling for performance
- [ ] **12.7** Implement request queuing and prioritization
- [ ] **12.8** Add response streaming for real-time updates
- [ ] **12.9** Test communication reliability and performance
- [ ] **12.10** Implement offline mode and data queuing
- [ ] **17.1** Install Ollama on local network server (PC/Mac/Linux)
- [ ] **17.2** Download and configure Llama 3.2 Vision model
- [ ] **17.3** Set up additional multi-modal models (Llava, etc.)
- [ ] **17.4** Configure API endpoints and port settings
- [ ] **17.5** Implement model switching and optimization
- [ ] **17.6** Add GPU acceleration configuration
- [ ] **17.7** Create server monitoring and health checks
- [ ] **17.8** Implement request logging and analytics
- [ ] **17.9** Test server performance with XREAL clients
- [ ] **17.10** Set up automated backup and recovery

#### 🗄️ Data & Memory Systems
- [ ] **19.1** Set up local vector database (ChromaDB/SQLite)
- [ ] **19.2** Design life logging data schemas
- [ ] **19.3** Create LifeLogManager script
- [ ] **19.4** Implement memory extraction pipeline
- [ ] **19.5** Add semantic search and retrieval system
- [ ] **19.6** Create memory categorization and tagging
- [ ] **19.7** Implement privacy-preserving storage encryption
- [ ] **19.8** Add memory visualization and browsing
- [ ] **19.9** Create memory export and sharing features
- [ ] **19.10** Test memory system performance and accuracy

### 🟡 MEDIUM PRIORITY TASKS

#### 🎮 Interaction & User Interface
- [ ] **9.1** Implement XREAL controller input handling
- [ ] **9.2** Configure touch input for Beam Pro touchscreen
- [ ] **9.3** Add gesture recognition system
- [ ] **9.4** Implement head tracking and gaze-based interaction
- [ ] **9.5** Create adaptive UI for different input methods
- [ ] **9.6** Add haptic feedback integration
- [ ] **9.7** Implement voice command shortcuts
- [ ] **9.8** Test interaction responsiveness and accuracy
- [ ] **9.9** Add accessibility features for diverse users
- [ ] **9.10** Create interaction tutorial system

#### 👥 People & Environment Recognition
- [ ] **6.1** Design face detection and recognition pipeline
- [ ] **6.2** Create PersonDetectionManager script
- [ ] **6.3** Implement privacy-preserving person identification
- [ ] **6.4** Add person tracking across conversation sessions
- [ ] **6.5** Create person profile and interaction history
- [ ] **6.6** Implement conversation participant identification
- [ ] **6.7** Add social context analysis (relationships, frequency)
- [ ] **6.8** Create person-specific memory storage
- [ ] **6.9** Test recognition accuracy and privacy compliance
- [ ] **6.10** Implement consent management for person data
- [ ] **7.1** Create EnvironmentAnalysisManager script
- [ ] **7.2** Implement location detection and labeling
- [ ] **7.3** Add scene understanding and context recognition
- [ ] **7.4** Create place-based memory associations
- [ ] **7.5** Implement environment change detection
- [ ] **7.6** Add activity recognition based on environment
- [ ] **7.7** Create location history and patterns
- [ ] **7.8** Implement place-specific AI behavior adaptation
- [ ] **7.9** Test environment recognition in various settings
- [ ] **7.10** Add GPS integration for outdoor location context

#### 📅 Event Detection & Activity Recognition
- [ ] **8.1** Design event detection framework
- [ ] **8.2** Create EventDetectionManager script
- [ ] **8.3** Implement activity recognition algorithms
- [ ] **8.4** Add event classification (meetings, meals, activities)
- [ ] **8.5** Create event timeline and duration tracking
- [ ] **8.6** Implement event significance scoring
- [ ] **8.7** Add automatic event summarization
- [ ] **8.8** Create event search and retrieval system
- [ ] **8.9** Test event detection across daily activities
- [ ] **8.10** Implement event-triggered AI proactive suggestions

#### 📡 Network & Server Discovery
- [ ] **13.1** Implement network scanning and server discovery
- [ ] **13.2** Create manual server configuration UI
- [ ] **13.3** Add connection status monitoring and indicators
- [ ] **13.4** Implement automatic reconnection logic
- [ ] **13.5** Add network quality assessment and adaptation
- [ ] **13.6** Create server health monitoring
- [ ] **13.7** Implement failover to backup servers
- [ ] **13.8** Add network security validation
- [ ] **13.9** Test network switching and roaming
- [ ] **13.10** Create network troubleshooting tools

#### 🎭 Avatar & Animation System
- [ ] **14.1** Install and configure RPM Unity SDK
- [ ] **14.2** Create AvatarManager script for avatar loading
- [ ] **14.3** Implement avatar customization interface
- [ ] **14.4** Add avatar caching and optimization for XREAL
- [ ] **14.5** Configure avatar rendering for XREAL displays
- [ ] **14.6** Implement avatar quality settings and LOD
- [ ] **14.7** Add avatar emotion and expression system
- [ ] **14.8** Create avatar personality and behavior traits
- [ ] **14.9** Test avatar performance on both devices
- [ ] **14.10** Implement avatar backup and restore
- [ ] **15.1** Install and configure SALSA LipSync Suite
- [ ] **15.2** Connect TTS audio output to lip sync system
- [ ] **15.3** Calibrate mouth movements for XREAL display
- [ ] **15.4** Add phoneme-based lip sync accuracy
- [ ] **15.5** Implement real-time lip sync processing
- [ ] **15.6** Add facial expression integration
- [ ] **15.7** Create lip sync quality monitoring
- [ ] **15.8** Test lip sync across different avatar models
- [ ] **15.9** Optimize lip sync performance
- [ ] **15.10** Add lip sync debugging tools
- [ ] **16.1** Create avatar animation state machine
- [ ] **16.2** Implement idle animation variations
- [ ] **16.3** Add gesture recognition and playback system
- [ ] **16.4** Implement look-at behavior with XREAL head tracking
- [ ] **16.5** Create procedural animations for natural movement
- [ ] **16.6** Add emotion-based animation triggers
- [ ] **16.7** Implement conversation-aware body language
- [ ] **16.8** Create animation blending and transitions
- [ ] **16.9** Test animation responsiveness and naturalness
- [ ] **16.10** Add animation customization options

#### 💬 Conversation & Memory Management
- [ ] **18.1** Design conversation data schema and format
- [ ] **18.2** Create ConversationManager script
- [ ] **18.3** Implement conversation history storage
- [ ] **18.4** Add conversation search and retrieval
- [ ] **18.5** Create conversation summarization
- [ ] **18.6** Implement conversation context window management
- [ ] **18.7** Add conversation export and import
- [ ] **18.8** Create conversation privacy controls
- [ ] **18.9** Test conversation recall accuracy
- [ ] **18.10** Implement conversation analytics and insights

**Progress Tracking:**
- **Total Tasks:** 200
- **High Priority:** 106 tasks
- **Medium Priority:** 94 tasks
- **Completed:** 0/200 (0%)

**Legend:** ⭕ Not Started | 🟡 In Progress | ✅ Complete | ❌ Blocked

## ⚙️ Technical Architecture Overview

### 🏗️ System Components

This XREAL AI assistant consists of several interconnected systems working together to create a seamless user experience:

**Input Systems:**
- **Audio Processing**: Continuous microphone capture and preprocessing
- **Visual Processing**: Real-time camera feed analysis and object detection
- **Interaction Handling**: Controller, touch, and gesture input processing

**AI Processing Pipeline:**
- **Speech Recognition**: Local whisper.unity STT processing
- **Computer Vision**: Real-time object, person, and environment detection
- **Multi-modal AI**: Ollama server with Llama 3.2 Vision for context understanding

**Output Systems:**
- **Text-to-Speech**: Local piper.unity TTS with natural voice synthesis
- **Avatar System**: Ready Player Me 3D avatar with SALSA lip sync
- **Memory Storage**: Local vector database for life-logging and recall

### 🔒 Privacy & Security

The system implements a **local-first** approach ensuring all data processing occurs within the user's private network:

- All AI processing happens on local Ollama server
- Speech processing uses local STT/TTS models
- Life-logging data stored in local encrypted databases
- No external API calls or cloud dependencies
- User maintains complete control over personal data

### 📱 XREAL Device Integration

#### XREAL Beam Pro
- **Display**: 2K per eye with 120Hz refresh rate
- **Processing**: Snapdragon XR2 Gen 2 chipset
- **Audio**: Spatial audio with directional speakers
- **Interaction**: Touch-enabled beam controller
- **Cameras**: RGB and passthrough cameras for mixed reality

#### XREAL Ultra
- **Display**: 4K per eye with variable refresh rate
- **Processing**: Enhanced XR processing capabilities
- **Audio**: Advanced spatial audio system
- **Interaction**: Hand tracking and gesture recognition
- **Cameras**: High-resolution multi-camera array

## 🛠️ Development Dependencies

### 🎮 Unity Development Environment
- **Unity Version**: Unity 6 LTS (latest stable)
- **Target Platform**: Android (API Level 29+)
- **XR Framework**: XREAL Unity SDK
- **Build Target**: ARM64 Android devices

### 📦 Core Unity Packages
- **XREAL Unity SDK**: Device integration and tracking
- **Ready Player Me SDK**: Avatar creation and customization
- **SALSA LipSync Suite**: Facial animation and lip synchronization
- **whisper.unity**: Local speech-to-text processing
- **piper.unity**: Local text-to-speech synthesis

### 🤖 AI Infrastructure
- **Ollama Server**: Local multi-modal AI processing
- **Llama 3.2 Vision**: Primary AI model for conversation and analysis
- **ChromaDB/SQLite**: Vector database for semantic memory storage
- **Local Network**: WiFi-based communication between devices

### 🔧 Development Tools
- **Git**: Version control and collaboration
- **Android SDK**: Mobile development tools
- **XREAL Developer Portal**: Device registration and debugging
- **Unity Analytics**: Performance monitoring and optimization

## 📊 Development Phases

### Phase 1: Foundation (Weeks 1-4)
**Core Infrastructure Setup**
- Unity project initialization and XREAL SDK integration
- Basic audio/video capture and processing pipeline
- Ollama server setup and communication protocols
- Initial avatar system implementation

### Phase 2: AI Integration (Weeks 5-8)
**Intelligent Processing Systems**
- Speech recognition and synthesis implementation
- Object detection and environment analysis
- Multi-modal AI conversation system
- Memory storage and retrieval mechanisms

### Phase 3: Enhancement (Weeks 9-12)
**Advanced Features and Polish**
- Avatar animation and lip sync refinement
- Life-logging and contextual memory systems
- Performance optimization for XREAL devices
- User interface and interaction improvements

### Phase 4: Testing & Deployment (Weeks 13-16)
**Quality Assurance and Release**
- Comprehensive testing across device types
- Privacy and security validation
- User experience optimization
- Documentation and deployment preparation

## 🎯 Success Metrics

### 📈 Performance Targets
- **Real-time Response**: < 500ms latency for conversational interactions
- **Battery Life**: Minimum 4-hour continuous operation on XREAL devices
- **Accuracy**: > 95% speech recognition accuracy in quiet environments
- **Memory Efficiency**: < 2GB RAM usage during normal operation

### 👤 User Experience Goals
- **Natural Interaction**: Seamless voice and gesture-based communication
- **Contextual Awareness**: Intelligent responses based on environment and history
- **Privacy Assurance**: Complete local processing with user data control
- **Reliable Operation**: 99%+ uptime during active use sessions

## 🔮 Future Roadmap

### 🚀 Advanced AI Capabilities
- Integration with additional open-source LLMs
- Enhanced computer vision with 3D scene understanding
- Predictive suggestions based on user behavior patterns
- Multi-language support for global accessibility

### 🌟 Extended Platform Support
- Integration with other AR/VR devices
- Cross-platform synchronization capabilities
- IoT device integration for smart home control
- Wearable device connectivity for health monitoring

### 🤝 Community & Ecosystem
- Open-source contribution guidelines
- Plugin architecture for third-party extensions
- Developer SDK for custom integrations
- Community-driven feature development
 

---

This document outlines the technical architecture for creating a persistent, embodied AI agent designed to function as a personal assistant and life-logging tool specifically for XREAL devices. The agent will manifest as a 3D avatar within the user's mixed reality environment using XREAL Beam Pro and XREAL Ultra headsets, capable of continuous perception, contextual conversation, and interaction.

The core objective is to build a system that transcends simple command-response interactions. This agent will maintain a memory of past conversations and observations, enabling it to act as an intelligent companion that understands the user's personal context. This "life-logging" capability positions the agent to not only answer questions but also to recall information, summarize events, and assist with tasks based on a persistent understanding of the user's world.

The architecture is designed specifically for XREAL devices (Beam Pro and Ultra) using Unity and the XREAL Unity SDK. All cognitive and perceptual processing will be handled by a local, on-network Ollama server. This client-server model ensures user privacy by keeping all data within the local network while leveraging the full power of large, multi-modal language models for sophisticated reasoning and visual understanding.

## 🏗️ System Architecture

### Core Architectural Principles

- **Local-First Privacy**: All data, including audio recordings, visual captures, and conversation logs, remains within the user's local network. The Unity client communicates directly with a local Ollama server, eliminating reliance on cloud services for core AI processing and ensuring maximum user privacy.
- **Centralized Cognition**: A single, powerful multi-modal Large Language Model (LLM) running on the Ollama server acts as the agent's brain. This simplifies the XREAL client application, which offloads complex reasoning, visual analysis, and memory management to the server.
- **XREAL-Optimized Performance**: The Unity client is specifically optimized for XREAL's Android-based architecture, ensuring smooth performance and efficient battery usage on both Beam Pro and Ultra devices.
- **Adaptive Interface Design**: The client automatically detects the XREAL device type and adapts the user interface, interaction methods, and performance settings accordingly.

### High-Level Architecture

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

### Core Interaction Flow

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

### System Components

#### Unity XREAL Client Components

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

#### Local Network Server Components

The server runs on a computer within the same local WiFi network as the XREAL device, responsible for running the Ollama service.

- **Ollama Service:** The core application that hosts and serves large language models, exposing a REST API for interaction with XREAL clients
- **Multi-Modal LLM:** A powerful model capable of processing both text and images, such as Llama 3.2 Vision or Llava, handling all cognitive tasks for XREAL interactions
- **Persistent Memory (Optional Extension):** The Ollama server can be extended with a Python script to log conversations and extracted information into a local database (SQLite, ChromaDB) for life-logging capabilities

## 🥽 XREAL Device Support

This project focuses exclusively on XREAL devices to provide an optimized, cohesive experience tailored to XREAL's unique capabilities and form factors.

### Device Support Matrix

| Feature | XREAL Beam Pro | XREAL Ultra | XREAL One Pro |
|---------|----------------|-------------|---------------|
| **Display Resolution** | 1920×1080 per eye | 3840×2160 per eye | 1920×1080 per eye |
| **Refresh Rate** | 120Hz | 144Hz | 120Hz |
| **Camera System** | RGB camera | Passthrough + RGB | RGB camera |
| **Audio** | Stereo speakers | Spatial audio | Stereo speakers |
| **Processing** | Snapdragon XR2 | Custom XREAL chip | Snapdragon XR2 |
| **Battery Life** | 4-6 hours | 6-8 hours | 4-6 hours |
| **Touch Input** | Touchscreen | Controllers | Touchscreen |
| **AI Analysis** | Full support | Enhanced performance | Full support |
| **Avatar Rendering** | Standard quality | High quality | Standard quality |

### XREAL Beam Pro Integration

The XREAL Beam Pro serves as the primary development target, offering:
- **Android-based Development Environment**: Leveraging Unity's Android build pipeline with XREAL-specific optimizations
- **Touchscreen Interface**: Native touch controls for avatar interaction and settings management
- **Portable Form Factor**: Optimized for on-the-go AI assistance and life logging
- **Camera Integration**: RGB camera access for environmental awareness and visual queries

### XREAL Ultra Advanced Features

The XREAL Ultra provides enhanced capabilities for immersive AI interactions:
- **Advanced Tracking**: Superior spatial awareness and head tracking for more natural avatar positioning
- **Enhanced Display**: Higher resolution and field of view for better avatar rendering
- **Passthrough Technology**: Real-time environmental integration for seamless AR experiences
- **Performance Optimization**: Dedicated processing power for smoother AI interactions

### Unified XREAL Experience

Both devices share core functionality while adapting to their specific capabilities:
- **Consistent Avatar Behavior**: Same AI personality and conversation continuity across devices
- **Adaptive UI**: Interface automatically adjusts to device capabilities and form factor
- **Cross-Device Memory**: Conversation history and life logs persist when switching between devices
- **Performance Scaling**: Automatic optimization based on device capabilities

## 🚀 Implementation Strategy

### Phase-Based Development Plan

#### Phase 1: Foundation (Weeks 1-4)
**Priority Focus:** Establish core development environment and basic functionality
- Complete Tasks 1-2: Project Setup & XREAL SDK Integration
- Complete Task 17: Ollama Server Configuration
- Begin Tasks 3-4: Audio & Camera Managers
- **Milestone:** Basic Unity project running on XREAL devices with Ollama connectivity

#### Phase 2: Core AI Pipeline (Weeks 5-8)
**Priority Focus:** Implement essential AI processing capabilities
- Complete Tasks 10-11: Speech-to-Text & Text-to-Speech Integration
- Complete Tasks 12-13: Communication Layer (Ollama Connector & WiFi Discovery)
- Begin Tasks 5-8: AI Analysis Engine (Object, People, Place, Event Detection)
- **Milestone:** Working voice conversation with multi-modal AI analysis

#### Phase 3: Avatar & Interaction (Weeks 9-12)
**Priority Focus:** Develop embodied avatar system and interactions
- Complete Tasks 14-16: Avatar System (Ready Player Me, Lip Sync, Animation)
- Complete Task 9: XREAL Interaction Manager
- Complete Tasks 18-19: Memory System (Conversation & Life Logging)
- **Milestone:** Fully functional AI companion with persistent memory

#### Phase 4: Continuous Analysis & Optimization (Weeks 13-16)
**Priority Focus:** Implement life-logging and optimize performance
- Complete Task 20: Continuous AI Analysis Pipeline
- Complete all Testing & Optimization tasks
- Complete Privacy & Security audit
- **Milestone:** Production-ready AI assistant with life-logging capabilities

## 🛠️ Development Tools & Dependencies

### Essential Development Tools

| Tool | Version | Purpose | Installation |
|------|---------|---------|--------------|
| **Unity Editor** | 6 LTS | Primary development environment | [Download from Unity Hub](https://unity.com/download) |
| **Android Studio** | Latest | Android SDK and build tools | [Download](https://developer.android.com/studio) |
| **Git** | Latest | Version control | `brew install git` (macOS) |
| **Visual Studio Code** | Latest | Code editing and debugging | [Download](https://code.visualstudio.com/) |

### XREAL Development Requirements

| Component | Details | Notes |
|-----------|---------|-------|
| **XREAL Developer Account** | Required for SDK access | Register at [developer.xreal.com](https://developer.xreal.com) |
| **XREAL Unity SDK** | Latest version | Download from developer portal |
| **XREAL Device** | Beam Pro or Ultra | Required for testing and development |
| **USB Debugging** | Enable on device | For direct deployment and debugging |

### Core Platform SDKs

| Tool / SDK | Purpose | Link |
| :--- | :--- | :--- |
| **Unity Engine** | Primary development environment for XREAL client application. | [unity.com](https://unity.com/) |
| **XREAL Unity SDK** | Essential SDK for XREAL device integration and hardware access. | [developer.xreal.com](https://developer.xreal.com/download) |
| **Android SDK & NDK** | Required for building to XREAL's Android-based platform. | [developer.android.com](https://developer.android.com/studio) |

### AI & ML Dependencies

| Package | Purpose | Integration Method |
|---------|---------|-------------------|
| **whisper.unity** | Local speech-to-text | Unity Package Manager |
| **piper.unity** | Local text-to-speech | Unity Package Manager |
| **Unity Sentis** | ML inference runtime | Unity Package Manager |
| **Ready Player Me SDK** | Avatar system | Unity Package Manager |
| **SALSA LipSync Suite** | Lip synchronization | Unity Asset Store |

### AI, Avatar, and Animation Assets

| Asset / Tool | Purpose | Link |
| :--- | :--- | :--- |
| **Ollama** | Runs large language models on the local network server. | [ollama.com](https://ollama.com/) |
| **whisper.unity** | On-device Speech-to-Text (STT) optimized for Android/XREAL. | [GitHub](https://github.com/Macoron/whisper.unity) |
| **piper.unity** | On-device Text-to-Speech (TTS) using Piper and Unity Sentis. | [GitHub](https://github.com/Macoron/piper.unity) |
| **Ready Player Me SDK** | For creating and loading customizable avatars optimized for XREAL displays. | [GitHub](https://github.com/readyplayerme/rpm-unity-sdk-core) |
| **SALSA LipSync Suite** | Real-time, audio-based lip synchronization optimized for XREAL. | [Unity Asset Store](https://assetstore.unity.com/packages/tools/animation/salsa-lipsync-suite-148442) |
| **Unity Animation Rigging** | For procedural animations and XREAL head tracking integration. | [Unity Docs](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.1/manual/index.html) |

### Server Requirements

| Component | Minimum Specs | Recommended Specs |
|-----------|---------------|-------------------|
| **CPU** | 4-core, 2.4GHz | 8-core, 3.0GHz+ |
| **RAM** | 8GB | 16GB+ |
| **GPU** | 4GB VRAM | 8GB+ VRAM (RTX 3070 or better) |
| **Storage** | 50GB available | 100GB+ SSD |
| **Network** | WiFi 5 (802.11ac) | WiFi 6 (802.11ax) |

### Key Reference Projects

| Project / Resource | Description |
| :--- | :--- |
| **XREAL Unity Samples** | Official XREAL Unity SDK examples and starter projects for device integration. |
| **unity-mcp-ollama** | Demonstrates connecting Unity to local Ollama instance, adaptable for XREAL architecture. |
| **XREAL Developer Documentation** | Comprehensive guides for XREAL device development, SDK usage, and best practices. |
| **UnityWebRequest for Android** | Unity networking examples specifically for Android platform, applicable to XREAL devices. |

## 🔒 Privacy & Security

### Data Handling Principles

- **Local-First Architecture:** All personal data remains on local network
- **Encryption at Rest:** Life-logging database encrypted with user keys
- **Minimal Data Collection:** Only capture data necessary for functionality
- **User Consent:** Explicit permission for all data collection activities
- **Data Retention:** Configurable retention periods with automatic cleanup

### Privacy Controls Implementation

1. **Audio Recording Consent:** Visual indicators and explicit permission requests
2. **Camera Usage Notifications:** Clear visual feedback when camera is active
3. **Person Recognition Opt-in:** Consent required before storing person data
4. **Memory Export Controls:** User-controlled data export and deletion
5. **Network Security:** TLS encryption for all client-server communication

## ⚙️ XREAL-Specific Development Considerations

### Device-Specific Optimizations

- **Performance Targeting**: Optimize for XREAL's Android-based architecture and mobile processors
- **Battery Efficiency**: Implement power management strategies for extended use during life-logging sessions
- **Thermal Management**: Monitor and adapt performance based on device temperature
- **Memory Management**: Efficient memory usage for avatar rendering and AI processing

### XREAL SDK Integration Points

- **Device Detection**: Automatically detect Beam Pro vs Ultra and adapt features accordingly
- **Camera Access**: Use XREAL SDK APIs for optimal camera performance and quality
- **Display Optimization**: Leverage XREAL's display capabilities for avatar rendering
- **Audio Integration**: Utilize XREAL's spatial audio capabilities for immersive AI interactions

### Development Workflow

- **XREAL Developer Account**: Required for accessing SDK and device testing
- **Unity Version**: Recommend Unity 6 LTS for XREAL compatibility
- **Build Pipeline**: Configure for Android with XREAL-specific optimizations
- **Testing Strategy**: Test on both Beam Pro and Ultra devices throughout development

### Future Expansion Possibilities

While this project focuses exclusively on XREAL devices, the architecture is designed to be modular. Future versions could potentially expand to support:

- Additional XREAL device models as they become available
- Enhanced XREAL-specific features like advanced spatial mapping
- Integration with XREAL's evolving SDK capabilities
- Performance optimizations for next-generation XREAL hardware
 

## 🧰 Additional Development Resources

### Unity XR + Avatar Tools

| Name | Use | Link |
| ------------------- | ------------------------ | ---------------------------------------------------------------------------------------------------------------------------------- |
| QuestVisionKit | Quest camera passthrough | [GitHub](https://github.com/xrdevrob/QuestCameraKit) |
| RT-Voice PRO | Viseme TTS | [Unity Store](https://assetstore.unity.com/packages/tools/audio/rt-voice-pro-41068) |
| SpeechBlend | Viseme speech generation | [Unity Store](https://assetstore.unity.com/packages/tools/animation/speechblend-lipsync-149023) |

### AI Integrations

| Name | Use | Link |
| --------------------- | ---------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| API for ChatGPT | Simple Unity OpenAI plugin | [Asset Store](https://assetstore.unity.com/packages/tools/ai-ml-integration/api-for-chatgpt-easy-ai-integration-248291) |
| Dialogue System Addon | OpenAI, ElevenLabs, Claude | [Asset Store](https://assetstore.unity.com/packages/tools/ai-ml-integration/dialogue-system-for-unity-addon-for-openai-elevenlabs-other-gene-249287) |
| Unity MCP | AI agent frameworks in Unity | [Unity MCP GitHub](https://github.com/justinpbarnett/unity-mcp) |

### Vision & Spatial XR

| Tool | Use | Link |
| --------------------- | --------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| UXR.QuestCamera | Vision camera plugin | [GitHub](https://github.com/Uralstech/UXR.QuestCamera/) |
| ObjectDetectionSentis | YOLO on Quest | [Sample](https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples/blob/main/Media/ObjectDetectionSentis.gif) |
| Marker Tracking | ArUco, Image Tracking | [Quest Image Tracking](https://github.com/Aupuma/QuestImageTracking), [AR-Ultrasound](https://github.com/doughtmw/AR-Ultrasound-Training) |

### Ollama Integration

Unity app uses a REST connector to forward vision & speech prompts to a **local Ollama server** running multimodal models like LLaVA or Llava-Next.

```mermaid
flowchart LR
    VisionInput --> RESTCall
    AudioInput --> RESTCall
    RESTCall --> Ollama
    Ollama --> JSONResponse
    JSONResponse --> UnityApp
```

### Multiplayer & Collaboration (Optional)

| Tool | Use | Link |
| ----------------------------- | ----------------- | --------------------------------------------------------------------------------------------------------- |
| Unity MR Multiplayer Template | Shared MR context | [Unity Forum](https://discussions.unity.com/t/new-mr-multiplayer-tabletop-template-now-available/1595933) |

### Web Reference Implementation

| Feature | Link |
| ------------------- | --------------------------------------------------------------------------------------------------- |
| 3JS version (web) | [CI-Embodied-Companion-Web](https://github.com/companionintelligence/CI-Embodied-Companion-Web) |
| Unity version (new) | [CI-Embodied-Companion-Unity](https://github.com/companionintelligence/CI-Embodied-Companion-Unity) |

## 🧑‍💻 ADB & Debugging Tips

```sh
adb connect 192.168.0.146:42663
adb -s TGLM4CG11864AA shell am start -n com.unity.template.urpblank
apkanalyzer manifest print ci-xr-test.apk | grep package
aapt dump badging ci-xr-test.apk | grep ^package:
```

---
