using UnityEngine;
using XRScan.Core;

namespace XRScan.Platform
{
    /// <summary>Editor / desktop webcam provider for in-Editor iteration. Ported from CompanionScan.</summary>
    public class DesktopCameraProvider : MonoBehaviour, ICameraProvider
    {
        private WebCamTexture _webcam;
        private Texture2D _frame;

        public bool IsAvailable => WebCamTexture.devices.Length > 0;
        public bool SupportsRgbFrames => true;

        public void StartCapture()
        {
            if (!IsAvailable)
            {
                return;
            }

            if (_webcam == null)
            {
                _webcam = new WebCamTexture();
                _webcam.Play();
            }
        }

        public void StopCapture() { if (_webcam != null) _webcam.Stop(); }

        public byte[] GetCurrentFrameJpeg(int quality = 75)
        {
            if (_webcam == null || !_webcam.isPlaying)
            {
                return null;
            }

            if (_frame == null || _frame.width != _webcam.width || _frame.height != _webcam.height)
            {
                _frame = new Texture2D(_webcam.width, _webcam.height, TextureFormat.RGB24, false);
            }

            _frame.SetPixels32(_webcam.GetPixels32());
            _frame.Apply();
            return _frame.EncodeToJPG(quality);
        }

        public Matrix4x4 GetCameraIntrinsics() => Matrix4x4.identity;
        public Pose GetCameraPose() => new Pose(transform.position, transform.rotation);
    }
}
