# Ollama Server Setup Guide for Companion Intelligence XREAL

## Overview

This guide walks you through setting up a local Ollama server for the Companion Intelligence XREAL AI assistant. The system uses local AI processing for privacy and low latency.

## Prerequisites

- **Operating System:** Windows 10/11, macOS 10.15+, or Linux Ubuntu 18.04+
- **RAM:** Minimum 8GB, recommended 16GB+ for optimal performance
- **Storage:** 10GB+ free space for AI models
- **Network:** Local network connection to XREAL device
- **GPU (Optional):** NVIDIA GPU with 4GB+ VRAM for faster processing

## Installation Steps

### 1. Install Ollama

#### Windows
```bash
# Download and run the Windows installer
# Visit: https://ollama.ai/download/windows
# Or use winget
winget install Ollama.Ollama
```

#### macOS
```bash
# Download the macOS installer
# Visit: https://ollama.ai/download/macos
# Or use Homebrew
brew install ollama
```

#### Linux
```bash
# Install using curl
curl -fsSL https://ollama.ai/install.sh | sh

# Or download manually
# Visit: https://ollama.ai/download/linux
```

### 2. Start Ollama Server

```bash
# Start the Ollama service
ollama serve

# The server will start on http://localhost:11434
# You should see: "Ollama is running on http://127.0.0.1:11434"
```

### 3. Install Required AI Models

The Companion Intelligence system works best with multi-modal models that can process both text and images.

#### Recommended Models

##### LLaVA (Vision + Language) - Recommended
```bash
# Install LLaVA 7B model (good balance of speed and capability)
ollama pull llava:7b

# Or LLaVA 13B for better quality (requires more RAM)
ollama pull llava:13b

# Verify installation
ollama list
```

##### Alternative Models
```bash
# Moondream2 (smaller, faster vision model)
ollama pull moondream

# CLIP + Llama2 combination
ollama pull llama2:7b
ollama pull clip
```

### 4. Configure Network Access

By default, Ollama only accepts connections from localhost. To allow your XREAL device to connect:

#### Option A: Configure Ollama for Network Access
```bash
# Stop Ollama if running
pkill ollama

# Set environment variable to allow all network connections
export OLLAMA_HOST=0.0.0.0:11434

# Restart Ollama
ollama serve
```

#### Option B: Use systemd (Linux) or launchd (macOS)

**Linux (systemd):**
```bash
# Create systemd override
sudo mkdir -p /etc/systemd/system/ollama.service.d/

# Create override configuration
sudo tee /etc/systemd/system/ollama.service.d/override.conf << EOF
[Service]
Environment="OLLAMA_HOST=0.0.0.0:11434"
EOF

# Reload and restart
sudo systemctl daemon-reload
sudo systemctl restart ollama
```

**macOS (launchd):**
```bash
# Create plist file
sudo tee /Library/LaunchDaemons/com.ollama.server.plist << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.ollama.server</string>
    <key>ProgramArguments</key>
    <array>
        <string>/usr/local/bin/ollama</string>
        <string>serve</string>
    </array>
    <key>EnvironmentVariables</key>
    <dict>
        <key>OLLAMA_HOST</key>
        <string>0.0.0.0:11434</string>
    </dict>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
</dict>
</plist>
EOF

# Load and start service
sudo launchctl load /Library/LaunchDaemons/com.ollama.server.plist
```

### 5. Firewall Configuration

#### Windows Firewall
```powershell
# Allow Ollama through Windows Firewall
New-NetFirewallRule -DisplayName "Ollama Server" -Direction Inbound -Port 11434 -Protocol TCP -Action Allow
```

#### macOS Firewall
```bash
# Add firewall rule (if firewall is enabled)
sudo /usr/libexec/ApplicationFirewall/socketfilterfw --add /usr/local/bin/ollama
sudo /usr/libexec/ApplicationFirewall/socketfilterfw --unblockapp /usr/local/bin/ollama
```

#### Linux (UFW)
```bash
# Allow port 11434
sudo ufw allow 11434/tcp
```

### 6. Verify Installation

#### Test Local Connection
```bash
# Test basic API endpoint
curl http://localhost:11434/api/version

# Expected response:
# {"version":"0.1.x"}
```

#### Test Network Connection from XREAL Device
```bash
# Replace YOUR_SERVER_IP with your computer's IP address
curl http://YOUR_SERVER_IP:11434/api/version

# If successful, you should see the version response
```

#### Test Multi-Modal Capabilities
```bash
# Test text generation
curl http://localhost:11434/api/generate -d '{
  "model": "llava:7b",
  "prompt": "Why is the sky blue?",
  "stream": false
}'

# Test with image (base64 encoded)
curl http://localhost:11434/api/generate -d '{
  "model": "llava:7b",
  "prompt": "Describe this image",
  "images": ["iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg=="],
  "stream": false
}'
```

## Configuration for Companion Intelligence

### 1. Find Your Server IP Address

#### Windows
```cmd
ipconfig | findstr "IPv4"
```

#### macOS/Linux
```bash
ifconfig | grep "inet " | grep -v 127.0.0.1
# Or
ip addr show | grep "inet " | grep -v 127.0.0.1
```

### 2. XREAL App Configuration

When the Companion Intelligence app starts:

1. **Automatic Discovery:** The app will scan your local network for Ollama servers
2. **Manual Configuration:** If auto-discovery fails, you can manually enter your server IP
3. **Model Selection:** Choose from available models (llava:7b recommended)

### 3. Performance Optimization

#### GPU Acceleration (NVIDIA)
```bash
# Verify CUDA is available
nvidia-smi

# Ollama will automatically use GPU if available
# Monitor GPU usage:
nvidia-smi -l 1
```

#### Memory Management
```bash
# Set memory limits for Ollama (optional)
export OLLAMA_MAX_LOADED_MODELS=1
export OLLAMA_MAX_QUEUE=10

# Restart Ollama
ollama serve
```

#### Model Preloading
```bash
# Preload model to reduce first-request latency
curl http://localhost:11434/api/generate -d '{
  "model": "llava:7b",
  "prompt": "Hello",
  "keep_alive": "5m"
}'
```

## Troubleshooting

### Common Issues

#### 1. Connection Refused
```bash
# Check if Ollama is running
ps aux | grep ollama

# Check if port is open
netstat -an | grep 11434

# Restart Ollama
ollama serve
```

#### 2. Model Not Found
```bash
# List installed models
ollama list

# Pull missing model
ollama pull llava:7b
```

#### 3. Slow Performance
- **Reduce model size:** Use `llava:7b` instead of `llava:13b`
- **Enable GPU acceleration:** Ensure NVIDIA drivers are installed
- **Increase RAM:** Close other applications
- **Use SSD storage:** Ensure models are on fast storage

#### 4. Network Discovery Issues
```bash
# Verify server is accessible from network
# From another device on same network:
curl http://YOUR_SERVER_IP:11434/api/version

# Check firewall settings
# Ensure port 11434 is open for incoming connections
```

#### 5. High Memory Usage
```bash
# Monitor memory usage
htop
# Or on Windows: Task Manager
# Or on macOS: Activity Monitor

# Limit concurrent models
export OLLAMA_MAX_LOADED_MODELS=1
```

## Security Considerations

### Network Security
- **Local Network Only:** Only expose Ollama on your local network
- **VPN Access:** For remote access, use VPN instead of internet exposure
- **Firewall Rules:** Restrict access to specific IP ranges if needed

### Model Privacy
- **Data Processing:** All AI processing happens locally on your machine
- **No External Calls:** Models don't send data to external servers
- **Privacy First:** Your conversations and images stay on your network

## Advanced Configuration

### Custom Model Fine-tuning
```bash
# Create custom model from Modelfile
ollama create custom-assistant -f Modelfile

# Example Modelfile for XREAL assistant:
cat << EOF > Modelfile
FROM llava:7b

SYSTEM """
You are a helpful AI assistant for XREAL AR device users.
Respond concisely and naturally in a conversational tone.
Focus on what the user is seeing through their AR device.
Provide practical, actionable assistance.
"""

PARAMETER temperature 0.7
PARAMETER top_p 0.9
PARAMETER stop "</s>"
EOF
```

### Performance Monitoring
```bash
# Monitor Ollama performance
curl http://localhost:11434/api/ps

# Enable debug logging
export OLLAMA_DEBUG=1
ollama serve
```

### Auto-start Configuration

#### Windows (Task Scheduler)
1. Open Task Scheduler
2. Create Basic Task: "Ollama Server"
3. Trigger: At startup
4. Action: Start program `ollama.exe serve`
5. Set to run with highest privileges

#### macOS/Linux (System Service)
Already covered in installation steps above.

## Model Recommendations by Use Case

### Balanced Performance (Recommended)
- **Model:** `llava:7b`
- **RAM Required:** 8GB+
- **Performance:** Good speed and accuracy
- **Use Case:** General XREAL assistant usage

### High Accuracy
- **Model:** `llava:13b`
- **RAM Required:** 16GB+
- **Performance:** Better accuracy, slower
- **Use Case:** Detailed image analysis

### Fast Response
- **Model:** `moondream`
- **RAM Required:** 4GB+
- **Performance:** Very fast, basic vision
- **Use Case:** Quick object identification

### Text-Only Fallback
- **Model:** `llama2:7b`
- **RAM Required:** 6GB+
- **Performance:** Fast text responses
- **Use Case:** Voice-only interactions

## Support and Updates

### Updating Ollama
```bash
# Update Ollama itself
# Windows: Use installer or winget upgrade
# macOS: Use installer or brew upgrade
# Linux: curl -fsSL https://ollama.ai/install.sh | sh

# Update models
ollama pull llava:7b
```

### Getting Help
- **Ollama Documentation:** https://ollama.ai/docs
- **Community Discord:** https://discord.gg/ollama
- **GitHub Issues:** https://github.com/ollama/ollama/issues
- **Companion Intelligence Support:** Check project README

---

**Note:** This setup provides a fully local AI infrastructure for your XREAL Companion Intelligence system. All processing happens on your local network, ensuring privacy and low latency for your AR assistant experience.
