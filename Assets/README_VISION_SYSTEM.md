# XREAL Vision-Language System

A complete multimodal AI interaction system for XREAL smart glasses that combines computer vision, speech recognition, and natural language processing using local Ollama models.

## 🌟 Features

- **Real-time Camera Capture** from XREAL devices
- **Voice Command Recognition** using Speech-to-Text
- **Vision-Language Understanding** via Ollama models (LLaVA, Moondream)
- **Natural Speech Responses** using Text-to-Speech
- **Flexible Interaction Modes** (Image+Text, Image Only, Text Only)
- **Local Processing** for privacy and low latency
- **Continuous Analysis** mode for ambient AI assistance
- **Rich UI** with status indicators and controls

## 🏗️ Architecture

```
Voice Input → STT → Text Command
                         ↓
Camera Feed → Image Capture → Combined Request → Ollama Vision Model
                                                        ↓
Audio Output ← TTS ← Text Response ← AI Response
```

## 🚀 Quick Start

### 1. Prerequisites

- Unity 2022.3 LTS or newer
- XREAL SDK for Unity
- Local Ollama server with vision model (llava:7b recommended)
- XREAL Air, Air 2, or Air 2 Pro device

### 2. Setup Ollama Server

```bash
# Install Ollama
curl -fsSL https://ollama.ai/install.sh | sh

# Start server with network access
export OLLAMA_HOST=0.0.0.0:11434
ollama serve

# Install vision model
ollama pull llava:7b
```

### 3. Unity Scene Setup

1. **Add the VisionSystem GameObject:**
```csharp
// Create empty GameObject named "XREALVisionSystem"
// Add these components:
- CameraManager
- SpeechToTextManager  
- TextToSpeechManager
- OllamaClient
- VisionLanguageManager
- XREALVisionController
```

2. **Auto-setup using helper script:**
```csharp
// Add XREALVisionSceneSetup component to any GameObject
// Check "Setup On Awake" in inspector
// Everything will be configured automatically
```

### 4. Configuration

Update the Ollama server settings:

```csharp
[Header("Server Configuration")]
[SerializeField] private string serverIP = "192.168.1.100"; // Your server IP
[SerializeField] private int serverPort = 11434;
[SerializeField] private string defaultModel = "llava:7b";
```

## 📱 Usage

### Voice Commands

Simply speak to the XREAL device:

- **"What do you see?"** - General scene description
- **"Describe this object"** - Focused analysis
- **"What colors are visible?"** - Color analysis
- **"Read the text"** - OCR functionality
- **"Tell me about this place"** - Environmental understanding

### Manual Controls

- **Capture Button** or **Space Key** - Manual image capture
- **Settings Button** or **S Key** - Open configuration panel
- **Number Keys (1-6)** - Test predefined commands
- **D Key** - Start/stop demo mode

### Interaction Modes

1. **Image + Text Mode** (Default)
   - Combines voice command with camera image
   - Most comprehensive understanding

2. **Image Only Mode**
   - Automatic scene description
   - No voice command required

3. **Text Only Mode**
   - Pure conversation without camera
   - Traditional chatbot interaction

## 🛠️ Components Overview

### Core Components

#### VisionLanguageManager
- **Purpose:** Orchestrates the complete vision-language interaction flow
- **Key Features:** 
  - Event-driven architecture
  - Request queuing and processing
  - Interaction history tracking
  - Continuous mode support

#### CameraManager
- **Purpose:** Handles XREAL camera capture and image processing
- **Key Features:**
  - Real-time frame capture
  - Image preprocessing and optimization
  - Privacy controls with visual indicators
  - Multiple resolution levels

#### OllamaClient
- **Purpose:** Communicates with local Ollama server
- **Key Features:**
  - Text and multimodal request support
  - Automatic retry logic
  - Connection monitoring
  - Base64 image encoding for vision models

#### SpeechToTextManager
- **Purpose:** Converts voice input to text commands
- **Key Features:**
  - Continuous speech recognition
  - Noise reduction and audio processing
  - Confidence scoring
  - XREAL-optimized audio settings

#### TextToSpeechManager
- **Purpose:** Converts AI responses to natural speech
- **Key Features:**
  - Multiple voice options
  - Audio caching for performance
  - SSML support for advanced speech control
  - Background processing

#### XREALVisionController
- **Purpose:** Manages UI and user interaction
- **Key Features:**
  - Real-time status display
  - Configuration controls
  - Visual feedback indicators
  - Performance monitoring

### Helper Components

#### XREALVisionSceneSetup
- Automated scene configuration
- UI generation and linking
- Component setup and wiring

#### VisionExampleDemo
- Complete demonstration system
- Performance testing and metrics
- Interactive tutorials

## 📊 Performance Optimization

### Recommended Settings

```csharp
// Camera Settings
resolutionLevel = ResolutionLevel.Middle;  // Balance quality/performance
captureInterval = 2.0f;                    // Reasonable update rate
enableImagePreprocessing = true;           // Optimize for Ollama

// STT Settings  
sampleRate = 16000;                        // Optimal for speech recognition
enableNoiseReduction = true;               // Better accuracy in noisy environments
maxSpeechDuration = 30f;                   // Prevent runaway recording

// TTS Settings
enableAudioCaching = true;                 // Cache common phrases
maxCacheSize = 50;                         // Reasonable memory usage
optimizeForXREAL = true;                   // XREAL-specific audio tuning

// Ollama Settings
maxTokens = 300;                           // Concise responses
temperature = 0.7f;                        // Balanced creativity
requestTimeout = 30f;                      // Reasonable wait time
```

### Performance Monitoring

The system provides real-time metrics:

- **Frame Rate:** Camera capture performance
- **Response Time:** Ollama processing latency  
- **Memory Usage:** Texture and audio cache sizes
- **Success Rate:** Request completion percentage
- **Network Health:** Connection stability

## 🔧 Troubleshooting

### Common Issues

#### "Cannot connect to Ollama server"
```bash
# Verify Ollama is running and accessible
curl http://YOUR_SERVER_IP:11434/api/version

# Check firewall settings
sudo ufw allow 11434/tcp  # Linux
# Windows: Add firewall rule for port 11434
```

#### "Camera initialization failed"
- Verify XREAL SDK is properly imported
- Check Android camera permissions
- Ensure XREAL device is properly connected

#### "No audio output"
- Check device audio settings and volume
- Verify AudioSource components are configured
- Test with headphones vs speakers

#### "Poor speech recognition"
- Reduce background noise
- Speak clearly and at moderate pace
- Check microphone permissions and levels

### Debug Mode

Enable comprehensive logging:

```csharp
[SerializeField] private bool enableDebugLogging = true;
```

This provides detailed console output for all components.

## 🎯 Use Cases

### Navigation Assistant
```csharp
systemPrompt = "You are a navigation assistant helping users understand their environment. Focus on spatial relationships, obstacles, and directions.";
```

### Educational Tool
```csharp
systemPrompt = "You are an educational assistant helping users learn about objects and concepts they encounter. Provide informative explanations.";
```

### Accessibility Aid
```csharp
systemPrompt = "You are an accessibility assistant helping visually impaired users understand their environment. Provide clear, detailed descriptions.";
```

### Security Monitor
```csharp
systemPrompt = "You are a security assistant monitoring for unusual activities or safety concerns. Alert users to potential issues.";
```

## 🔮 Advanced Features

### Continuous Mode
Enables ambient AI assistance with periodic scene analysis:

```csharp
visionManager.StartContinuousMode();
// Automatically analyzes scene every few seconds
```

### Custom Models
Support for different Ollama vision models:

```csharp
// Fast, lightweight model for real-time use
defaultModel = "moondream:latest";

// High-quality model for detailed analysis  
defaultModel = "llava:13b";

// Latest features and capabilities
defaultModel = "llama3.2-vision:latest";
```

### Interaction History
Complete conversation tracking:

```csharp
var history = visionManager.GetInteractionHistory();
foreach(var interaction in history) {
    Debug.Log($"{interaction.timestamp}: {interaction.voiceCommand} -> {interaction.visionResponse}");
}
```

## 📈 Roadmap

- **Multi-language Support** - STT/TTS in multiple languages
- **Edge AI Integration** - On-device model inference
- **Advanced Computer Vision** - Object tracking and recognition
- **Cloud Model Support** - Integration with cloud AI services
- **Gesture Recognition** - Hand gesture-based controls
- **AR Overlay Integration** - Visual information overlay

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🙏 Acknowledgments

- **XREAL** for the smart glasses platform
- **Ollama** for local AI model serving
- **Meta Quest Camera Kit** for architectural inspiration
- **Unity Technologies** for the development platform

## 📞 Support

For issues, questions, or contributions:

- **GitHub Issues:** [Repository Issues](https://github.com/companionintelligence/ci-xreal/issues)
- **Documentation:** [Wiki](https://github.com/companionintelligence/ci-xreal/wiki)
- **Discussions:** [GitHub Discussions](https://github.com/companionintelligence/ci-xreal/discussions)

---

**Built with ❤️ for the XREAL community**
