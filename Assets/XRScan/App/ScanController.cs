using System.Threading.Tasks;
using UnityEngine;
using XRScan.Core;
using XRScan.Mapping;
using XRScan.Vision;

namespace XRScan.App
{
    /// <summary>
    /// Slice orchestrator: on trigger, grab a frame, run detection, unproject each detection to a
    /// world point, and add a spatial pin. Backend-agnostic — the camera and detector are injected,
    /// so the same flow runs against mocks in the Editor or the real camera + CV server on device.
    /// When the detector is not ready (no vision server set) it tells the user so through
    /// <see cref="UserNotices"/> and places nothing.
    /// </summary>
    public class ScanController : MonoBehaviour
    {
        /// <summary>
        /// What the vision server looks for: common objects in a room, in GroundingDINO's "a . b ." form. The
        /// server labels each box with the phrase that matched, so these are also the pin labels.
        /// </summary>
        public const string DefaultDetectionPrompt =
            "chair . table . sofa . bed . lamp . cup . mug . bottle . bowl . book . laptop . monitor . keyboard . " +
            "phone . remote control . plant . vase . clock . picture frame . shelf . cabinet . door . window . box . " +
            "bag . pillow .";

        [SerializeField] private ViewportRaycaster raycaster;
        [SerializeField] private SpatialPinManager pinManager;
        [SerializeField] private string detectionPrompt = DefaultDetectionPrompt;
        [SerializeField] private float minScore = 0.3f;

        private ICameraProvider _camera;
        private IObjectDetector _detector;
        private bool _busy;
        private int _session;

        /// <summary>The prompt sent with each frame (the default list when the field is blank).</summary>
        public string DetectionPrompt => string.IsNullOrWhiteSpace(detectionPrompt) ? DefaultDetectionPrompt : detectionPrompt.Trim();

        public void Configure(ICameraProvider camera, IObjectDetector detector)
        {
            _camera = camera;
            _detector = detector;
            _camera?.StartCapture();
        }

        /// <summary>Trigger one scan pass. Safe to call from input; ignores re-entry while busy.</summary>
        public async void Scan() => await ScanOnceAsync();

        /// <summary>One scan pass; returns the number of pins placed.</summary>
        public async Task<int> ScanOnceAsync()
        {
            if (_busy) return 0;
            if (_detector == null || !_detector.IsReady)
            {
                UserNotices.Post(_detector is RemoteCvDetector remote ? remote.UnavailableReason : RemoteCvDetector.NotConfiguredMessage);
                return 0;
            }
            if (_camera == null)
            {
                Debug.LogWarning("[ScanController] Not configured (no camera provider).");
                UserNotices.Post("Scanning is not available: this device gives the app no camera image.");
                return 0;
            }

            _busy = true;
            try { return await ScanAsync(); }
            catch (CvServerException e)
            {
                Debug.LogError($"[ScanController] scan failed: {e.Message}");
                UserNotices.Post(e.UserMessage);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ScanController] scan failed: {e.Message}");
                UserNotices.Post("The scan failed: " + e.Message);
            }
            finally { _busy = false; }
            return 0;
        }

        private async Task<int> ScanAsync()
        {
            byte[] jpeg = _camera.GetCurrentFrameJpeg();
            if (jpeg == null)
            {
                Debug.LogWarning("[ScanController] No frame available.");
                UserNotices.Post("No camera image yet. If you declined camera access, allow it under Permissions in Settings.");
                return 0;
            }

            var pose = _camera.GetCameraPose();
            var intrinsics = _camera.GetCameraIntrinsics();
            var size = _camera.FrameSize;
            var frame = CaptureFrame.Create(jpeg, size.x, size.y, pose, intrinsics);
            frame.sessionId = _session.ToString();
            frame.frameIndex = _session++;

            string prompt = DetectionPrompt;
            var detection = await _detector.DetectAsync(frame, prompt);
            if (detection == null || detection.bboxes.Count == 0)
            {
                Debug.Log("[ScanController] No detections.");
                UserNotices.Post("No objects found in view.");
                return 0;
            }

            int placed = 0;
            for (int i = 0; i < detection.bboxes.Count; i++)
            {
                float score = i < detection.scores.Count ? detection.scores[i] : 1f;
                if (score < minScore) continue;

                string label = i < detection.labels.Count ? detection.labels[i] : "object";
                if (!BoundingBoxUtils.TryNormalize(detection.bboxes[i], frame.imageWidth, frame.imageHeight, out var norm))
                    continue;

                ResolvePoint(frame, norm, out Vector3 point, out Vector3 normal, out RaycastHitType hitType);

                var pin = new SpatialPin
                {
                    label = label,
                    semanticLabel = label,
                    score = score,
                    source = PinSource.CvServer,
                    position = point,
                    normal = normal,
                    sourcePrompt = prompt,
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
            return placed;
        }

        /// <summary>
        /// World point for a box (normalized, origin top-left). With the camera's intrinsics and the image size the
        /// ray goes through the box centre's pixel from the camera pose; otherwise through the matching viewport
        /// point of the main camera, with y flipped from image (down) to viewport (up).
        /// </summary>
        private void ResolvePoint(CaptureFrame frame, Rect box, out Vector3 point, out Vector3 normal, out RaycastHitType hitType)
        {
            hitType = RaycastHitType.Fallback;
            if (raycaster == null)
            {
                var cam = Camera.main;
                point = cam != null ? cam.transform.position + cam.transform.forward * 2f : Vector3.forward * 2f;
                normal = Vector3.up;
                return;
            }

            if (frame.HasIntrinsics && frame.imageWidth > 0 && frame.imageHeight > 0)
            {
                var pixel = BoundingBoxUtils.CenterInPixels(box, frame.imageWidth, frame.imageHeight);
                raycaster.TryRaycastCapture(frame, pixel, true, out point, out normal, out hitType);
            }
            else
            {
                raycaster.TryRaycastViewport(BoundingBoxUtils.CenterInViewport(box), out point, out normal, out hitType);
            }
        }
    }
}
