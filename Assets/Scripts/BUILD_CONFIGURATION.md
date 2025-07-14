# Build Configuration for Companion Intelligence XREAL

## Unity Project Settings

### Platform Configuration
- **Target Platform:** Android
- **API Compatibility Level:** .NET Standard 2.1
- **Scripting Backend:** IL2CPP
- **Target Architecture:** ARM64
- **Minimum API Level:** Android 7.0 (API Level 24)
- **Target API Level:** Android 13 (API Level 33)

### XR Settings
- **XR Plugin Provider:** XREAL SDK
- **Rendering Mode:** Multi-Pass (recommended for XREAL)
- **Stereo Rendering Method:** Single Pass Instanced

### Quality Settings
```
// XREAL Ultra Configuration
Quality Level: Ultra
Texture Quality: Full Res
Anisotropic Filtering: Forced On
Anti Aliasing: 4x Multi Sampling
Soft Particles: Enabled
Real-time Reflection Probes: Enabled
Maximum LOD Level: 0

// XREAL Beam Pro Configuration  
Quality Level: High
Texture Quality: Half Res
Anisotropic Filtering: Per Texture
Anti Aliasing: 2x Multi Sampling
Soft Particles: Disabled
Real-time Reflection Probes: Disabled
Maximum LOD Level: 1
```

### Player Settings
```
Company Name: Your Company
Product Name: Companion Intelligence XREAL
Version: 1.0.0
Bundle Identifier: com.yourcompany.companion-intelligence-xreal

Configuration: Release
Scripting Define Symbols: XREAL_DEVICE;AI_COMPANION;LOCAL_PROCESSING

Auto Graphics API: Disabled
Graphics APIs: 
  - OpenGLES3
  - Vulkan (if supported)

Multithreaded Rendering: Enabled
Static Batching: Enabled
Dynamic Batching: Enabled
GPU Skinning: Enabled
```

## Required Permissions (AndroidManifest.xml)

```xml
<!-- Camera access for visual analysis -->
<uses-permission android:name="android.permission.CAMERA" />

<!-- Microphone access for speech recognition -->
<uses-permission android:name="android.permission.RECORD_AUDIO" />

<!-- Network access for Ollama server communication -->
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
<uses-permission android:name="android.permission.ACCESS_WIFI_STATE" />

<!-- Storage for object database and caching -->
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />

<!-- XREAL device features -->
<uses-feature android:name="android.hardware.camera" android:required="true" />
<uses-feature android:name="android.hardware.microphone" android:required="true" />
<uses-feature android:name="android.hardware.sensor.accelerometer" android:required="true" />
<uses-feature android:name="android.hardware.sensor.gyroscope" android:required="true" />

<!-- XREAL specific permissions -->
<uses-permission android:name="android.permission.BLUETOOTH" />
<uses-permission android:name="android.permission.BLUETOOTH_ADMIN" />
```

## Package Dependencies

### Unity Packages (Package Manager)
```json
{
  "dependencies": {
    "com.unity.xr.arfoundation": "5.0.7",
    "com.unity.xr.arcore": "5.0.7",
    "com.unity.inputsystem": "1.7.0",
    "com.unity.render-pipelines.universal": "14.0.8",
    "com.unity.textmeshpro": "3.0.6",
    "com.unity.ugui": "1.0.0",
    "com.unity.modules.audio": "1.0.0",
    "com.unity.modules.imageconversion": "1.0.0",
    "com.unity.modules.jsonserialize": "1.0.0"
  }
}
```

### External Packages (Manual Installation Required)
- **XREAL Unity SDK** (com.xreal.xr) - Install from local package
- **Newtonsoft.Json** - Available via Package Manager
- **whisper.unity** - Download from GitHub (TBD)
- **piper.unity** - Download from GitHub (TBD)
- **Ready Player Me SDK** - Download from Ready Player Me (TBD)
- **SALSA LipSync Suite** - Purchase from Unity Asset Store (TBD)

## Build Process

### Prerequisites
1. **Unity 6 LTS** installed with Android Build Support
2. **Android SDK** and NDK properly configured
3. **XREAL Unity SDK** imported into project
4. **Ollama server** running on local network

### Build Steps

#### 1. Pre-Build Validation
```csharp
// Add to pre-build script
public class PreBuildValidation
{
    [MenuItem("Build/Validate Dependencies")]
    public static void ValidateDependencies()
    {
        // Check for required components
        bool hasXREALSDK = AssetDatabase.FindAssets("t:Script XREALCamera").Length > 0;
        bool hasOllamaClient = AssetDatabase.FindAssets("t:Script OllamaClient").Length > 0;
        
        if (!hasXREALSDK)
            Debug.LogError("XREAL SDK not found! Please import the SDK.");
            
        if (!hasOllamaClient)
            Debug.LogError("Core scripts missing! Please ensure all scripts are in project.");
            
        Debug.Log("Pre-build validation complete.");
    }
}
```

#### 2. Build Configuration Script
```csharp
public class BuildConfiguration
{
    [MenuItem("Build/Build for XREAL")]
    public static void BuildForXREAL()
    {
        // Set build settings
        EditorUserBuildSettings.buildAppBundle = false; // APK for sideloading
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.allowDebugging = false;
        EditorUserBuildSettings.connectProfiler = false;
        
        // Configure player settings
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33;
        
        // Build
        string buildPath = "Builds/CompanionIntelligence-XREAL.apk";
        BuildPipeline.BuildPlayer(GetScenePaths(), buildPath, BuildTarget.Android, BuildOptions.None);
        
        Debug.Log($"Build completed: {buildPath}");
    }
    
    private static string[] GetScenePaths()
    {
        return new string[] { "Assets/Scenes/MainScene.unity" };
    }
}
```

#### 3. Manual Build Process
1. Open Unity Build Settings (Ctrl+Shift+B)
2. Select Android platform
3. Ensure scenes are added to build
4. Click "Player Settings" and verify configuration
5. Click "Build" and select output location
6. Wait for build completion (5-10 minutes typical)

### Testing Configuration

#### Development Build Settings
```
Development Build: Enabled
Script Debugging: Enabled
Wait for Managed Debugger: Disabled

Compression Method: LZ4
Split Application Binary: Enabled

Build App Bundle (Google Play): Disabled
Export Project: Disabled
```

#### Production Build Settings
```
Development Build: Disabled
Script Debugging: Disabled
Deep Profiling Support: Disabled

Compression Method: LZ4HC
Split Application Binary: Enabled

Build App Bundle (Google Play): Enabled (if publishing)
Export Project: Disabled
```

## Performance Optimization

### Texture Settings
```
Default Texture Settings:
- Max Size: 1024 (UI), 512 (3D objects)
- Compression: ASTC 6x6 (Android)
- Generate Mip Maps: Enabled for 3D textures
- Filter Mode: Bilinear
- Aniso Level: 1
```

### Audio Settings
```
Audio Settings:
- Sample Rate: 44100 Hz
- Compression Format: Vorbis
- Quality: 70 (voices), 50 (effects)
- Load Type: Compressed in Memory
- Preload Audio Data: Critical sounds only
```

### Mesh Settings
```
Model Import Settings:
- Scale Factor: 1
- Mesh Compression: Medium
- Read/Write Enabled: Disabled
- Optimize Mesh: Enabled
- Generate Colliders: Only when needed
```

## Deployment

### XREAL Device Installation

#### Sideloading APK
```bash
# Enable Developer Options on XREAL device
# Enable USB Debugging
# Connect device via USB

# Install APK
adb install CompanionIntelligence-XREAL.apk

# Launch application
adb shell am start -n com.yourcompany.companion-intelligence-xreal/.MainActivity

# Monitor logs
adb logcat -s Unity
```

#### Wireless Installation (if supported)
```bash
# Connect to XREAL device wirelessly
adb connect DEVICE_IP:5555

# Install and launch as above
```

### Network Configuration

#### Ollama Server Discovery
The app will automatically scan the local network for Ollama servers on port 11434. Ensure:
1. Ollama server is running and accessible
2. Firewall allows port 11434
3. Both devices are on same network
4. Server IP is in private range (192.168.x.x, 10.x.x.x, etc.)

#### Manual Configuration
If auto-discovery fails, users can manually configure:
1. Open app settings
2. Enter server IP address
3. Test connection
4. Select AI model to use

## Troubleshooting Build Issues

### Common Problems

#### 1. XREAL SDK Not Found
```
Error: The type or namespace name 'XREALCamera' could not be found

Solution:
1. Import XREAL Unity SDK package
2. Ensure package is in Packages/com.xreal.xr/
3. Refresh Asset Database (Ctrl+R)
4. Reimport package if necessary
```

#### 2. IL2CPP Build Errors
```
Error: IL2CPP error for type 'System.Net.Http.HttpClient'

Solution:
1. Add to link.xml:
<linker>
  <assembly fullname="System.Net.Http" preserve="all"/>
  <assembly fullname="Newtonsoft.Json" preserve="all"/>
</linker>

2. Set Api Compatibility Level to .NET Standard 2.1
```

#### 3. Permission Errors
```
Error: Permission denied for CAMERA

Solution:
1. Verify AndroidManifest.xml has correct permissions
2. Check Player Settings > Publishing Settings
3. Ensure target API level supports runtime permissions
4. Test permission requests in app
```

#### 4. Performance Issues
```
Problem: Low frame rate on XREAL device

Solution:
1. Reduce texture quality in settings
2. Disable unnecessary quality features
3. Optimize shaders for mobile
4. Profile using Unity Profiler
5. Check for memory leaks
```

### Build Verification

#### Post-Build Checklist
- [ ] APK installs without errors
- [ ] App launches and shows main interface
- [ ] Camera permission requested and granted
- [ ] Microphone permission requested and granted
- [ ] Network discovery finds Ollama server
- [ ] Voice activation works
- [ ] Avatar displays correctly
- [ ] Speech recognition responds
- [ ] Object detection identifies items
- [ ] Performance metrics acceptable

#### Performance Targets
- **Startup Time:** < 10 seconds
- **Network Discovery:** < 30 seconds
- **Voice Response:** < 3 seconds
- **Frame Rate:** 30+ FPS consistent
- **Memory Usage:** < 2GB total
- **Battery Life:** 3+ hours typical usage

---

## Distribution

### Internal Testing
1. Build development APK with debugging enabled
2. Install on test XREAL devices
3. Configure test Ollama servers
4. Document issues and performance metrics
5. Iterate based on feedback

### Production Release
1. Build production APK with optimizations
2. Test on multiple XREAL device types
3. Verify all features work without debugging
4. Create user installation guide
5. Package with Ollama setup documentation

### Future: Store Distribution
- Package for XREAL App Store (when available)
- Consider Google Play Store (with XREAL device requirement)
- Create promotional materials and screenshots
- Develop user onboarding flow

---

**Note:** This build configuration ensures optimal performance and compatibility with XREAL devices while maintaining the local-first privacy approach of the Companion Intelligence system.
