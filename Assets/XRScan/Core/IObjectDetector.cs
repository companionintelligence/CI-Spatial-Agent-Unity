using System.Threading.Tasks;
using UnityEngine;

namespace XRScan.Core
{
    /// <summary>
    /// Hides the vision backend (remote CV server vs on-device Sentis) behind one async
    /// contract so the mapping pipeline never knows which ran. Implementations:
    /// RemoteCvDetector (CV server /v1/*), SentisYoloDetector (on-device), MockObjectDetector.
    /// </summary>
    public interface IObjectDetector
    {
        /// <summary>True when the backend is reachable / loaded and ready to serve requests.</summary>
        bool IsReady { get; }

        /// <summary>
        /// Open-vocabulary detection. <paramref name="prompt"/> is a whitespace/'.'-separated
        /// class list (may be empty for generic detection).
        /// </summary>
        Task<DetectionResult> DetectAsync(CaptureFrame frame, string prompt);

        /// <summary>Promptable segmentation over a region of interest (normalized 0-1 rect).</summary>
        Task<SegmentationResult> SegmentAsync(CaptureFrame frame, Rect roi);

        /// <summary>Vision-language captioning of the whole frame.</summary>
        Task<FlorenceResult> CaptionAsync(CaptureFrame frame);
    }
}
