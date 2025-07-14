# XREAL Vision System Setup Guide

## Overview

This guide walks you through setting up the complete XREAL Vision-Language system that integrates:
- XREAL camera capture
- Speech-to-Text (STT) for voice commands
- Ollama vision models for image understanding
- Text-to-Speech (TTS) for audio responses

The system provides a complete multimodal AI conversation loop similar to Quest Camera Kit's ImageLLM example but optimized for XREAL devices using local Ollama models.

## Architecture

```
Voice Command → STT → Combined with Camera Image → Ollama Vision Model → TTS → Audio Response
     ↑                                                                              ↓
     └──────────────── Continuous Interaction Loop ──────────────────────────────┘
```

## Prerequisites

### Hardware Requirements
- **XREAL Device:** Air, Air 2, or Air 2 Pro
- **Host Device:** Android phone/tablet or Windows PC with XREAL support
- **Local Server:** Computer with Ollama server (can be same as host device)

### Software Requirements
- **Unity 2022.3 LTS** or newer
- **XREAL SDK** (Unity package)
- **Ollama Server** with vision models
- **Network Connection** between XREAL device and Ollama server

## Step 1: Ollama Server Setup

Follow the detailed [Ollama Setup Guide](OLLAMA_SETUP_GUIDE.md) to:

1. Install Ollama on your local machine
2. Download vision models (llava:7b recommended)
3. Configure network access for XREAL device

### Quick Ollama Setup
```bash
# Install Ollama
curl -fsSL https://ollama.ai/install.sh | sh

# Start server with network access
export OLLAMA_HOST=0.0.0.0:11434
ollama serve

# In another terminal, install vision model
ollama pull llava:7b
```

## Step 2: Unity Project Setup

### Core Components Installation

The XREAL Vision system consists of several core components that work together:

1. **CameraManager** - Handles XREAL camera capture
2. **SpeechToTextManager** - Processes voice commands
3. **TextToSpeechManager** - Converts responses to speech
4. **OllamaClient** - Communicates with Ollama server
5. **VisionLanguageManager** - Orchestrates the complete interaction
6. **XREALVisionController** - Manages UI and user interaction

### Scene Setup

1. **Create a new scene** or use an existing XREAL scene
2. **Add an empty GameObject** named "VisionSystem"
3. **Add the following components** to the VisionSystem GameObject:
   - `CameraManager`
   - `SpeechToTextManager`
   - `TextToSpeechManager`
   - `OllamaClient`
   - `VisionLanguageManager`
   - `XREALVisionController`

### UI Setup

Create a Canvas with the following UI elements:

```
Canvas (Screen Space - Overlay)
├── StatusPanel
│   ├── StatusText (TextMeshPro)
│   ├── CommandText (TextMeshPro)
│   └── ResponseText (TextMeshPro)
├── ControlPanel
│   ├── CaptureButton (Button)
│   ├── SettingsButton (Button)
│   └── ProgressSlider (Slider)
├── SettingsPanel
│   ├── ModelDropdown (Dropdown)
│   ├── ModeDropdown (Dropdown)
│   ├── TemperatureSlider (Slider)
│   └── ContinuousModeToggle (Toggle)
├── FeedbackIndicators
│   ├── ListeningIndicator (Image)
│   ├── ProcessingIndicator (Image)
│   └── ErrorIndicator (Image)
└── CameraFeed
    └── CameraFeedImage (Image)
```

## Step 3: Configuration

### 1. OllamaClient Configuration

```csharp
[Header("Server Configuration")]
[SerializeField] private string serverIP = "192.168.1.100"; // Your Ollama server IP
[SerializeField] private int serverPort = 11434;
[SerializeField] private string defaultModel = "llava:7b";
```

### 2. CameraManager Configuration

```csharp
[Header("Camera Configuration")]
[SerializeField] private ResolutionLevel resolutionLevel = ResolutionLevel.Middle;
[SerializeField] private int targetFrameRate = 30;
[SerializeField] private bool enableContinuousCapture = true;
[SerializeField] private float captureInterval = 2.0f;
```

### 3. VisionLanguageManager Configuration

```csharp
[Header("Vision-Language Settings")]
[SerializeField] private VisionMode currentMode = VisionMode.ImageAndText;
[SerializeField] private string systemPrompt = "You are an AI assistant viewing the world through XREAL smart glasses...";
[SerializeField] private bool enableContinuousMode = false;
```

## Step 4: Testing and Validation

### Basic Functionality Test

1. **Start the application** on your XREAL device
2. **Check connection status** - UI should show "XREAL Vision System Ready"
3. **Test manual capture** - Press the capture button or Space key
4. **Test voice commands** - Say "What do you see?" or "Describe this image"
5. **Verify audio response** - Listen for TTS playback

### Network Connectivity Test

1. **Check Ollama connection** in Unity Console:
   ```
   [OllamaClient] Connection successful!
   [OllamaClient] Model loaded: llava:7b
   ```

2. **Verify camera capture**:
   ```
   [CameraManager] Frame captured: 45232 bytes
   [CameraManager] XREAL RGB Camera initialized
   ```

3. **Test STT/TTS**:
   ```
   [STTManager] Speech recognized: "What do you see"
   [TTSManager] Playing audio clip: response_audio (3.2s)
   ```

## Step 5: Usage Instructions

### Voice Commands

The system responds to natural language voice commands:

- **"What do you see?"** - General scene description
- **"Describe this object"** - Focused object analysis
- **"What colors do you see?"** - Color analysis
- **"Read the text"** - OCR and text reading
- **"Tell me about this place"** - Location/scene understanding

### Interaction Modes

1. **Image + Text Mode** (Default)
   - Combines voice command with camera image
   - Most comprehensive understanding

2. **Image Only Mode**
   - Provides general scene description
   - No specific voice command needed

3. **Text Only Mode**
   - Pure text conversation
   - No camera input used

### Controls

- **Manual Capture:** Capture button or Space key
- **Settings:** Settings button or 'S' key  
- **Test Command:** 'T' key (in editor)
- **Continuous Mode:** Toggle for automatic periodic analysis

## Step 6: Performance Optimization

### Recommended Settings

For optimal performance on XREAL devices:

```csharp
// Camera Settings
resolutionLevel = ResolutionLevel.Middle; // Balance quality/performance
targetFrameRate = 30; // Smooth but not excessive
captureInterval = 2.0f; // Reasonable update rate

// STT Settings
sampleRate = 16000; // Optimal for Whisper
enableNoiseReduction = true;
maxSpeechDuration = 30f;

// TTS Settings
sampleRate = 22050; // Good quality for Piper
enableAudioCaching = true;
maxCacheSize = 50;

// Ollama Settings
maxTokens = 300; // Concise responses
temperature = 0.7f; // Balanced creativity
requestTimeout = 30f; // Reasonable wait time
```

### Performance Monitoring

Monitor these metrics in the Unity Console:

- **Frame Rate:** Target 60 FPS for smooth experience
- **Memory Usage:** Watch for texture memory leaks
- **Network Latency:** Ollama response times should be <5 seconds
- **Audio Quality:** Clear STT recognition and TTS output

## Step 7: Troubleshooting

### Common Issues

#### 1. "Cannot connect to Ollama server"
```bash
# Check if Ollama is running
curl http://YOUR_SERVER_IP:11434/api/version

# Ensure network access is configured
export OLLAMA_HOST=0.0.0.0:11434
ollama serve
```

#### 2. "Camera initialization failed"
- Verify XREAL SDK is properly imported
- Check camera permissions on Android
- Ensure XREAL device is connected

#### 3. "Speech recognition not working"
- Check microphone permissions
- Verify audio input levels
- Test with different noise environments

#### 4. "No TTS audio output"
- Check device audio settings
- Verify AudioSource components are configured
- Test with different audio output methods

### Debug Logging

Enable debug logging in all components:

```csharp
[SerializeField] private bool enableDebugLogging = true;
```

This provides detailed console output for troubleshooting.

## Step 8: Advanced Configuration

### Custom Prompts

Customize the system prompt for specific use cases:

```csharp
// For navigation assistance
systemPrompt = "You are a navigation assistant helping users understand their environment through XREAL glasses. Focus on spatial relationships, obstacles, and directions.";

// For educational purposes
systemPrompt = "You are an educational assistant helping users learn about objects, places, and concepts they encounter. Provide informative and engaging explanations.";

// For accessibility
systemPrompt = "You are an accessibility assistant helping visually impaired users understand their environment. Provide clear, detailed descriptions of what you see.";
```

### Model Selection

Different models for different use cases:

- **llava:7b** - Good balance of speed and accuracy
- **llava:13b** - Better accuracy, slower processing
- **moondream:latest** - Faster, smaller model for real-time use
- **llama3.2-vision:latest** - Latest features, may require more resources

### Continuous Mode

Enable for ambient AI assistance:

```csharp
enableContinuousMode = true;
continuousModeInterval = 10f; // Every 10 seconds
```

## Conclusion

The XREAL Vision system provides a complete multimodal AI experience that combines:

- Real-time camera capture from XREAL devices
- Natural voice interaction through STT/TTS
- Powerful vision understanding via Ollama models
- Flexible UI for different interaction modes

The system is designed to be modular, performant, and easily customizable for various use cases from navigation assistance to educational applications.

For additional support and advanced configurations, refer to the individual component documentation and the Ollama setup guide.
