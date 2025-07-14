# XREAL Vision System - Consolidated Architecture

## Overview

This consolidated XREAL Vision System is based on the Quest ImageLLM example but adapted for XREAL devices and Ollama vision models. The architecture follows the proven voice-to-vision pipeline: **Voice Command → Image Capture → Vision Model → Speech Response**.

## Architecture Components

### Core Components (Assets/Scripts/Core/)

1. **XREALVisionSystem.cs** - Main orchestrator component
   - Manages the complete vision pipeline
   - Coordinates all subsystems
   - Handles events and state management

2. **XREALCameraManager.cs** - Camera capture handling
   - Captures images from device camera
   - Optimizes images for vision processing
   - Supports both real camera and dummy images for testing

3. **XREALOllamaConnector.cs** - Ollama vision model integration
   - Communicates with local Ollama server
   - Supports both text and vision requests
   - Handles retries and error recovery

4. **XREALSpeechToText.cs** - Voice input processing
   - Records audio from microphone
   - Converts speech to text via OpenAI Whisper
   - Supports voice activation detection

5. **XREALTextToSpeech.cs** - Audio response generation
   - Converts text responses to speech
   - Supports OpenAI TTS with multiple voices
   - Manages speech queue and playback

6. **XREALVoiceCommandHandler.cs** - Voice interaction flow
   - Bridges voice commands to vision processing
   - Based on Quest VoiceCommandHandler architecture
   - Handles command processing workflow

7. **XREALVisionSceneSetup.cs** - Automated scene setup
   - Automatically configures all components
   - Creates UI and debug controls
   - One-click scene setup

## Quick Setup

### 1. Automatic Setup (Recommended)
1. Add `XREALVisionSceneSetup` component to any GameObject
2. Configure server settings in the inspector:
   - Ollama Server IP (default: 192.168.1.100)
   - Ollama Server Port (default: 11434)
   - Ollama Model (default: llava:7b)
   - OpenAI API Key (for STT/TTS)
3. Check "Setup On Awake" to auto-configure on scene start
4. Click "Setup XREAL Vision Scene" in the context menu (or let it auto-setup)

### 2. Manual Setup
1. Create empty GameObject named "XREAL Vision System"
2. Add all core components:
   - XREALVisionSystem
   - XREALCameraManager
   - XREALSpeechToText
   - XREALTextToSpeech
   - XREALOllamaConnector
   - XREALVoiceCommandHandler
3. Configure component settings in inspector

## Configuration

### Ollama Server Setup
```bash
# Install Ollama
curl -fsSL https://ollama.ai/install.sh | sh

# Start server with network access
export OLLAMA_HOST=0.0.0.0:11434
ollama serve

# Install vision model
ollama pull llava:7b
```

### OpenAI API Configuration
- Get API key from https://platform.openai.com/api-keys
- Set in XREALSpeechToText and XREALTextToSpeech components
- Used for Whisper STT and TTS services

### XREAL Device Configuration
- Ensure XREAL glasses are connected
- Camera should be available for image capture
- Audio output configured for TTS playback

## Usage

### Voice Commands
1. Say "What do you see?" or similar questions
2. System captures image and sends to Ollama
3. Receives vision analysis response
4. Speaks response via TTS

### Debug Controls (Editor)
- **T** - Test vision command
- **1,2,3** - Quick test commands
- **L** - Toggle voice listening
- **Space** - Hold to listen
- **R** - Restart system

### Programmatic Control
```csharp
// Get vision system reference
XREALVisionSystem visionSystem = FindObjectOfType<XREALVisionSystem>();

// Process text command
visionSystem.ProcessTextCommand("What do you see?");

// Start/stop listening
visionSystem.StartListening();
visionSystem.StopListening();
```

## Pipeline Flow

1. **Voice Input**: User speaks command → XREALSpeechToText records and transcribes
2. **Command Processing**: XREALVoiceCommandHandler receives transcription
3. **Image Capture**: XREALCameraManager captures current view (with delay to avoid hands)
4. **Vision Processing**: XREALOllamaConnector sends image + text to Ollama vision model
5. **Response Generation**: Ollama returns description/analysis
6. **Speech Output**: XREALTextToSpeech converts response to audio and plays

## Key Features

### Based on Quest ImageLLM Architecture
- Proven voice-to-vision workflow
- Event-driven component communication
- Robust error handling and retries

### Optimized for XREAL
- Lightweight components suitable for mobile processing
- Efficient image capture and processing
- Spatial audio support for immersive experience

### Ollama Integration
- Local vision model processing (no cloud dependency for vision)
- Support for multiple vision models (llava:7b, llava:13b, etc.)
- Configurable model parameters (temperature, tokens, etc.)

### Production Ready
- Comprehensive error handling
- Configurable retry logic
- Debug logging and testing tools
- Modular component design

## Troubleshooting

### Common Issues

1. **Ollama Connection Failed**
   - Check server IP and port
   - Ensure Ollama server is running with network access
   - Verify model is installed (`ollama list`)

2. **No Microphone Input**
   - Check microphone permissions
   - Verify microphone device availability
   - Test with other audio apps

3. **TTS Not Working**
   - Verify OpenAI API key is valid
   - Check internet connection
   - Ensure audio output device is working

4. **Camera Capture Issues**
   - Verify camera permissions
   - Check if other apps are using camera
   - Test with dummy image in editor

### Debug Logging
Enable debug logging in each component to see detailed operation logs:
- Set "Enable Debug Logging" to true in component inspectors
- Check Unity Console for detailed logs

## Migration from Old Architecture

The new consolidated architecture replaces the scattered components in the previous system:

**Old Components → New Components**
- Multiple camera scripts → XREALCameraManager
- Various STT managers → XREALSpeechToText  
- Multiple TTS managers → XREALTextToSpeech
- Multiple Ollama clients → XREALOllamaConnector
- Various orchestrators → XREALVisionSystem
- Scattered voice handlers → XREALVoiceCommandHandler

The new system is simpler, more reliable, and follows proven patterns from the Quest ImageLLM example.

## Performance Optimization

### Recommended Settings
```csharp
// Camera Settings
captureWidth = 512;
captureHeight = 512;
optimizeForVision = true;

// STT Settings  
sampleRate = 16000;
enableNoiseReduction = true;
maxRecordingLength = 10;

// TTS Settings
allowQueueing = true;
maxQueueSize = 5;

// Ollama Settings
maxTokens = 300;
temperature = 0.7f;
requestTimeout = 30f;
```

## Future Enhancements

- Local Whisper STT integration
- Local TTS options
- Multiple vision model support
- Enhanced UI components
- Gesture-based controls
- Eye tracking integration
