using System.Threading.Tasks;
using UnityEngine;
using XRScan.Core;

namespace XRScan.Mocks
{
    /// <summary>Scripted detector so the slice runs with no CV server. Returns a single centered
    /// detection, cycling through a small label set on each call.</summary>
    public class MockObjectDetector : IObjectDetector
    {
        private readonly string[] _labels;
        private int _index;

        public bool IsReady => true;

        public MockObjectDetector(string[] labels = null)
        {
            _labels = (labels != null && labels.Length > 0)
                ? labels
                : new[] { "cup", "chair", "plant", "book" };
        }

        public Task<DetectionResult> DetectAsync(CaptureFrame frame, string prompt)
        {
            string label = _labels[_index % _labels.Length];
            _index++;

            var result = new DetectionResult();
            result.bboxes.Add(new[] { 0.4f, 0.4f, 0.6f, 0.6f }); // centered, normalized
            result.labels.Add(label);
            result.scores.Add(0.92f);
            return Task.FromResult(result);
        }

        public Task<SegmentationResult> SegmentAsync(CaptureFrame frame, Rect roi)
            => Task.FromResult(new SegmentationResult());

        public Task<FlorenceResult> CaptionAsync(CaptureFrame frame)
            => Task.FromResult(new FlorenceResult { task = "<CAPTION>", text = "a mock scene" });
    }
}
