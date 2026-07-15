using System.Threading.Tasks;
using UnityEngine;
using XRScan.Core;
using XRScan.Mapping;

namespace XRScan.App
{
    /// <summary>
    /// Slice orchestrator: on trigger, grab a frame, run detection, unproject each detection to a
    /// world point, and add a spatial pin. Backend-agnostic — the camera and detector are injected,
    /// so the same flow runs against mocks in the Editor or the real camera + CV server on device.
    /// </summary>
    public class ScanController : MonoBehaviour
    {
        [SerializeField] private ViewportRaycaster raycaster;
        [SerializeField] private SpatialPinManager pinManager;
        [SerializeField] private string detectionPrompt = "object";
        [SerializeField] private float minScore = 0.3f;

        private ICameraProvider _camera;
        private IObjectDetector _detector;
        private bool _busy;
        private int _session;

        public void Configure(ICameraProvider camera, IObjectDetector detector)
        {
            _camera = camera;
            _detector = detector;
            _camera?.StartCapture();
        }

        /// <summary>Trigger one scan pass. Safe to call from input; ignores re-entry while busy.</summary>
        public async void Scan()
        {
            if (_busy) return;
            if (_camera == null || _detector == null)
            {
                Debug.LogWarning("[ScanController] Not configured (camera/detector missing).");
                return;
            }

            _busy = true;
            try { await ScanAsync(); }
            catch (System.Exception e) { Debug.LogError($"[ScanController] scan failed: {e.Message}"); }
            finally { _busy = false; }
        }

        private async Task ScanAsync()
        {
            byte[] jpeg = _camera.GetCurrentFrameJpeg();
            if (jpeg == null)
            {
                Debug.LogWarning("[ScanController] No frame available.");
                return;
            }

            var pose = _camera.GetCameraPose();
            var intrinsics = _camera.GetCameraIntrinsics();
            var frame = CaptureFrame.Create(jpeg, 0, 0, pose, intrinsics);
            frame.sessionId = _session.ToString();
            frame.frameIndex = _session++;

            var detection = await _detector.DetectAsync(frame, detectionPrompt);
            if (detection == null || detection.bboxes.Count == 0)
            {
                Debug.Log("[ScanController] No detections.");
                return;
            }

            int placed = 0;
            for (int i = 0; i < detection.bboxes.Count; i++)
            {
                float score = i < detection.scores.Count ? detection.scores[i] : 1f;
                if (score < minScore) continue;

                string label = i < detection.labels.Count ? detection.labels[i] : "object";
                if (!BoundingBoxUtils.TryNormalize(detection.bboxes[i], frame.imageWidth, frame.imageHeight, out var norm))
                    continue;

                Vector2 uv = new Vector2(norm.center.x, norm.center.y);
                Vector3 point;
                Vector3 normal;
                RaycastHitType hitType = RaycastHitType.Fallback;
                if (raycaster != null)
                {
                    raycaster.TryRaycastViewport(uv, out point, out normal, out hitType);
                }
                else
                {
                    var cam = Camera.main;
                    point = cam != null ? cam.transform.position + cam.transform.forward * 2f : Vector3.forward * 2f;
                    normal = Vector3.up;
                }

                var pin = new SpatialPin
                {
                    label = label,
                    semanticLabel = label,
                    score = score,
                    source = PinSource.CvServer,
                    position = point,
                    normal = normal,
                    sourcePrompt = detectionPrompt,
                    timestamp = Time.timeAsDouble,
                    captureSessionId = frame.sessionId,
                    captureFrameIndex = frame.frameIndex,
                    hitType = hitType,
                    bboxNormalized = new SerializableRect(norm.x, norm.y, norm.width, norm.height)
                };
                pinManager.AddPin(pin);
                placed++;
            }

            Debug.Log($"[ScanController] Placed {placed} pin(s).");
        }
    }
}
