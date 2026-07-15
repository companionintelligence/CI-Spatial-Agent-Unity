using System.Threading.Tasks;
using UnityEngine;
using XRScan.Core;

namespace XRScan.Vision
{
    /// <summary>
    /// On-device object detector via Unity Inference Engine (Sentis) — SCAFFOLD. When a YOLO
    /// .onnx model + Inference Engine worker are wired (Phase 4b), this drops the CV-server
    /// dependency for fully untethered Quest operation. Until then <see cref="IsReady"/> is false
    /// so the hybrid <see cref="DetectorFactory"/> routes to the server. Detection is Quest-only
    /// regardless (consumer Pico exposes no RGB frames — see IPlatformCapabilities).
    /// </summary>
    public class SentisYoloDetector : IObjectDetector
    {
        private readonly bool _wired;

        public SentisYoloDetector(bool wired = false) => _wired = wired;

        public bool IsReady => _wired;

        public Task<DetectionResult> DetectAsync(CaptureFrame frame, string prompt)
        {
            if (!_wired)
            {
                Debug.LogWarning("[SentisYoloDetector] Not wired (needs a YOLO model + Inference Engine); falling back to server.");
                return Task.FromResult(new DetectionResult());
            }
            // TODO (Phase 4b): decode frame.jpegBytes -> Texture2D -> input Tensor,
            // run the Inference Engine Worker, run NMS, map to DetectionResult.
            return Task.FromResult(new DetectionResult());
        }

        public Task<SegmentationResult> SegmentAsync(CaptureFrame frame, Rect roi)
            => Task.FromResult(new SegmentationResult());

        public Task<FlorenceResult> CaptionAsync(CaptureFrame frame)
            => Task.FromResult(new FlorenceResult { task = "<CAPTION>", text = string.Empty });
    }
}
