# CI Unity XREAL (Fresh)

This is a clean Unity 6.3 LTS project targeted at XREAL SDK 3.1.0.

## Setup
1. Open with Unity `6000.3.7f1`.
2. Ensure the XREAL SDK tarball exists at `Packages/com.xreal.xr.tar.gz`.
3. Install Git (>= 2.14) and Git LFS so Unity can fetch git-based packages (RPM SDK and glTFast dependencies).
4. Open the dev scene: `Assets/Scenes/XREAL-Dev.unity`.
5. (Optional) Start Unity MCP server for tools:
   - `scripts/start_unity_mcp_server.sh`

## Ready Player Me (RPM) Avatar Animation
We use the RPM Unity SDK core package to load avatars at runtime.

Before importing the RPM animation library, make sure Git + Git LFS are installed (Unity Package Manager requires Git, and Git LFS is required when a repo uses LFS).

### RPM Animation Library Import (Required)
1. Place the RPM animation library at `Assets/AnimationLibrary` (or `Assets/ThirdParty/readyplayerme-animation-library`).
2. In Unity, run: `Tools > Avatar > Setup RPM Animation Library` (runs all setup steps).
   - Alternatively run each step manually:
     - `Tools > Avatar > Configure RPM Animation Imports`.
   - This sets TPose FBX files to Humanoid and assigns animation clips to use the correct TPose Avatar.
   - `Tools > Avatar > Build Animation Library`
   - `Tools > Avatar > Build Animation Map`

### Runtime Loading
`AvatarBootstrap` uses the RPM loader first, and falls back to glTFast if RPM fails. When RPM succeeds, it uses the avatar metadata (OutfitGender) to select masculine/feminine animation sets.
Set `CI_AVATAR_PATH` to override the avatar folder, or place avatars in `Assets/Avatars` (editor) or `StreamingAssets/Avatars` (builds).

## Notes
- Legacy project archived at `Archive/CI-Unity-XR-legacy-2026-02-06/`.
- Roadmap and planning live in `ROADMAP.md`.

## Zero-Shot CV Subscene
The `ZeroShot-CV` scene runs a local zero-shot endpoint and overlays 2D bounding boxes.

### Local Server (stub)
Run the local stub server:
```
scripts/grounding_dino_server.py --host 127.0.0.1 --port 8000
```
The Unity client (`ZeroShotDetector`) posts to `http://127.0.0.1:8000/detect`.
