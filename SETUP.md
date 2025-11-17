# XREAL Unity Project Setup Guide

This guide will help you set up the CI-Unity-Xreal project for development and building.

## Prerequisites

- Unity 6000.2.0b9 (or compatible version from Unity Hub)
- Android Build Support module installed in Unity
- Git for version control

## Required SDK Installation

### XREAL SDK Installation

The XREAL SDK is required for this project to build successfully. Follow these steps to install it:

1. **Download the XREAL SDK**
   - Visit the XREAL Developer Portal: https://developer.xreal.com/download
   - Create an account or log in if you don't have one
   - Download the `com.xreal.xr.tar.gz` file (version 3.0.0-pre.4 or later)

2. **Install the SDK in Unity**
   - Open the project in Unity
   - Go to `Window` → `Package Manager`
   - Click the `+` button in the top-left corner
   - Select `Add package from tarball...`
   - Navigate to where you downloaded `com.xreal.xr.tar.gz`
   - Select the file and click `Open`

3. **Verify Installation**
   - In the Package Manager, you should now see "XREAL XR Plugin" listed under "Custom" packages
   - The version should be 3.0.0-pre.4 or higher
   - Sample scenes should be available in `Assets/Samples/XREAL XR Plugin/`
   - Unity will automatically update `Packages/packages-lock.json` to include the XREAL package reference

## Project Setup

1. **Clone the Repository**
   ```bash
   git clone https://github.com/companionintelligence/CI-Unity-Xreal.git
   cd CI-Unity-Xreal
   ```

2. **Open in Unity**
   - Open Unity Hub
   - Click "Add" and select the cloned project folder
   - Open the project with Unity 6000.2.0b9 or compatible version

3. **Install XREAL SDK**
   - Follow the steps in the "XREAL SDK Installation" section above

4. **Configure Build Settings**
   - Go to `File` → `Build Settings`
   - Select `Android` as the target platform
   - Click `Switch Platform` if not already on Android
   - Verify that scenes are added to the build (the first enabled scene should be RGBCameraAndCapture.unity)

## Troubleshooting

### "Package 'com.xreal.xr' not found" Error

This error occurs when the XREAL SDK is not installed. Follow the "XREAL SDK Installation" steps above.

### Missing Dependencies

If you see errors about missing packages, use the Package Manager to install required dependencies:
- XR Interaction Toolkit (should be 3.2.0-pre.1 or compatible)
- AR Foundation (should be 6.2.0 or compatible)
- Universal Render Pipeline (should be 17.2.0 or compatible)

### Build Fails on Android

Make sure you have:
- Android Build Support installed in Unity
- Android SDK and NDK properly configured in Unity preferences
- A valid Android keystore configured (for release builds)

## Additional Resources

- [XREAL SDK Documentation](https://docs.xreal.com/Getting%20Started%20with%20XREAL%20SDK)
- [XREAL SDK Template on GitHub](https://github.com/dengxian-xreal/XREALSDKTemplate)
- [Unity XR Documentation](https://docs.unity3d.com/Manual/XR.html)

## Project Structure

```
CI-Unity-Xreal/
├── Assets/
│   ├── Samples/          # XREAL SDK sample scenes
│   ├── Scripts/          # Custom scripts for the project
│   ├── Scenes/           # Project scenes
│   └── ...
├── Packages/
│   ├── manifest.json     # Package dependencies
│   └── packages-lock.json
├── ProjectSettings/      # Unity project settings
├── SETUP.md             # This file
└── README.md            # Project overview
```

## Getting Help

If you encounter issues:
1. Check this SETUP.md file for common solutions
2. Review the [XREAL Developer Portal](https://developer.xreal.com/)
3. Check the project's GitHub Issues page
4. Refer to Unity documentation for general Unity questions
