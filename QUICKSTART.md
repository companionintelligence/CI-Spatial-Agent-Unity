# Quick Start Checklist

Use this checklist when setting up the CI-Unity-Xreal project for the first time.

## Prerequisites Setup
- [ ] Install Unity Hub
- [ ] Install Unity 6000.2.0b9 (or compatible version)
- [ ] Install Android Build Support module in Unity
- [ ] Install Git (if not already installed)

## Project Setup
- [ ] Clone this repository: `git clone https://github.com/companionintelligence/CI-Unity-Xreal.git`
- [ ] Navigate to the project folder

## XREAL SDK Installation (REQUIRED)
- [ ] Visit https://developer.xreal.com/download
- [ ] Create/login to XREAL developer account
- [ ] Download `com.xreal.xr.tar.gz` (version 3.0.0-pre.4 or later)
- [ ] Open project in Unity
- [ ] Open Package Manager (Window → Package Manager)
- [ ] Click `+` → Add package from tarball
- [ ] Select downloaded `com.xreal.xr.tar.gz`
- [ ] Wait for Unity to import the package
- [ ] Verify "XREAL XR Plugin" appears in Package Manager

## Build Configuration
- [ ] Open File → Build Settings
- [ ] Select Android platform
- [ ] Click "Switch Platform" (if needed)
- [ ] Verify scenes are listed in build

## Verification
- [ ] Open a sample scene from `Assets/Samples/XREAL XR Plugin/`
- [ ] Press Play in Unity to test (may require device connection for full functionality)
- [ ] Try building the project (File → Build Settings → Build)

## Optional: Development Tools
- [ ] Install Visual Studio Code or Visual Studio for Unity development
- [ ] Configure Unity to use your preferred code editor (Edit → Preferences → External Tools)

## Troubleshooting
If you encounter issues, see:
- [SETUP.md](SETUP.md) - Detailed setup instructions
- [README.md](README.md) - Project overview
- `.xreal-sdk/README.md` - SDK-specific information

## Next Steps
After setup is complete, you can:
- Review the project structure in SETUP.md
- Explore sample scenes
- Start developing!

---

**Need Help?** Check the GitHub Issues page or refer to the XREAL Developer documentation at https://docs.xreal.com/
