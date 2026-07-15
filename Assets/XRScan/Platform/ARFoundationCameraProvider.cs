using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using XRScan.Core;

namespace XRScan.Platform
{
    /// <summary>
    /// AR Foundation passthrough camera provider — the primary cross-platform RGB source.
    /// On Quest 3 the Meta-OpenXR camera subsystem delivers real CPU images + intrinsics; the
    /// same code path serves any AR Foundation camera. Generalized from CompanionScan's
    /// MobileCameraProvider (which was #if-guarded to iOS/Android) — AR Foundation is now a
    /// hard package dependency, so it compiles on all targets and the polyfill lives in the
    /// provider selection, not in preprocessor branches.
    /// </summary>
    [RequireComponent(typeof(ARCameraManager))]
    public class ARFoundationCameraProvider : MonoBehaviour, ICameraProvider
    {
        private ARCameraManager _cameraManager;
        private Texture2D _frame;

        public bool IsAvailable => _cameraManager != null && _cameraManager.enabled;
        public bool SupportsRgbFrames => _cameraManager != null;

        private void Awake() => _cameraManager = GetComponent<ARCameraManager>();

        public void StartCapture() { if (_cameraManager != null) _cameraManager.enabled = true; }
        public void StopCapture() { if (_cameraManager != null) _cameraManager.enabled = false; }

        public byte[] GetCurrentFrameJpeg(int quality = 75)
        {
            if (_cameraManager == null || !_cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            {
                return null;
            }

            using (image)
            {
                if (_frame == null || _frame.width != image.width || _frame.height != image.height)
                {
                    _frame = new Texture2D(image.width, image.height, TextureFormat.RGB24, false);
                }

                var conversionParams = new XRCpuImage.ConversionParams
                {
                    inputRect = new RectInt(0, 0, image.width, image.height),
                    outputDimensions = new Vector2Int(image.width, image.height),
                    outputFormat = TextureFormat.RGB24,
                    transformation = XRCpuImage.Transformation.MirrorY
                };

                int bufferSize = image.GetConvertedDataSize(conversionParams);
                var buffer = new NativeArray<byte>(bufferSize, Allocator.Temp);
                image.Convert(conversionParams, buffer);
                _frame.LoadRawTextureData(buffer);
                _frame.Apply();
                buffer.Dispose();
                return _frame.EncodeToJPG(quality);
            }
        }

        public Matrix4x4 GetCameraIntrinsics()
        {
            if (_cameraManager != null && _cameraManager.TryGetIntrinsics(out var intrinsics))
            {
                var m = Matrix4x4.zero;
                m.m00 = intrinsics.focalLength.x;
                m.m11 = intrinsics.focalLength.y;
                m.m02 = intrinsics.principalPoint.x;
                m.m12 = intrinsics.principalPoint.y;
                m.m22 = 1f;
                return m;
            }

            return Matrix4x4.zero;
        }

        public Pose GetCameraPose() => new Pose(transform.position, transform.rotation);
    }
}
