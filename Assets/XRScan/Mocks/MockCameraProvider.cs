using UnityEngine;
using XRScan.Core;

namespace XRScan.Mocks
{
    /// <summary>Synthetic camera for in-Editor / test runs. Produces a solid-colour JPEG frame
    /// posed at the main camera, so the whole scan slice runs with no device or webcam.</summary>
    public class MockCameraProvider : MonoBehaviour, ICameraProvider
    {
        [SerializeField] private int width = 640;
        [SerializeField] private int height = 480;
        [SerializeField] private Color fillColor = new Color(0.3f, 0.4f, 0.5f);

        private Texture2D _frame;
        private bool _capturing;

        public bool IsAvailable => true;
        public bool SupportsRgbFrames => true;

        public void StartCapture() => _capturing = true;
        public void StopCapture() => _capturing = false;

        public byte[] GetCurrentFrameJpeg(int quality = 75)
        {
            if (!_capturing)
            {
                return null;
            }

            if (_frame == null)
            {
                _frame = new Texture2D(width, height, TextureFormat.RGB24, false);
                var pixels = new Color[width * height];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = fillColor;
                _frame.SetPixels(pixels);
                _frame.Apply();
            }
            return _frame.EncodeToJPG(quality);
        }

        public Matrix4x4 GetCameraIntrinsics() => Matrix4x4.identity;

        public Pose GetCameraPose()
        {
            var cam = Camera.main;
            return cam != null
                ? new Pose(cam.transform.position, cam.transform.rotation)
                : new Pose(transform.position, transform.rotation);
        }
    }
}
