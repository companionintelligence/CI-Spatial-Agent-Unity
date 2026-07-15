using UnityEngine;

namespace XRScan.Core
{
    /// <summary>
    /// Provider-agnostic passthrough / RGB camera access. The key cross-platform seam:
    /// the scan pipeline consumes JPEG frames + pose + intrinsics without knowing whether
    /// they came from Quest passthrough, AR Foundation, a desktop webcam, or a mock.
    /// On platforms that do not expose RGB camera frames to apps (e.g. consumer Pico),
    /// <see cref="SupportsRgbFrames"/> is false and the pipeline degrades to scene-only pins.
    /// This is the "passthrough polyfill": one contract, many platform implementations.
    /// </summary>
    public interface ICameraProvider
    {
        /// <summary>True when the underlying camera subsystem is running and delivering frames.</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// True when this platform grants per-app RGB camera frames for computer vision.
        /// Quest 3 (Passthrough Camera API) = true; consumer Pico = false; Desktop = true.
        /// </summary>
        bool SupportsRgbFrames { get; }

        void StartCapture();
        void StopCapture();

        /// <summary>Latest frame as JPEG bytes, or null if unavailable this frame.</summary>
        byte[] GetCurrentFrameJpeg(int quality = 75);

        /// <summary>Camera intrinsics (focal length / principal point) for 2D-&gt;3D unprojection.</summary>
        Matrix4x4 GetCameraIntrinsics();

        /// <summary>World-space pose of the camera at the latest frame.</summary>
        Pose GetCameraPose();
    }
}
