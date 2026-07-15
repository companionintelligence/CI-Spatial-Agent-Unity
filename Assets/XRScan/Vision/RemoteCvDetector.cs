using System.Threading.Tasks;
using UnityEngine;
using XRScan.Core;

namespace XRScan.Vision
{
    /// <summary>IObjectDetector backed by the remote CV server. The default Phase-1 detector;
    /// swaps to an on-device Sentis detector (Phase 4) with no downstream change.</summary>
    public class RemoteCvDetector : IObjectDetector
    {
        private readonly CVServerClient _client;
        private readonly ServerConfig _config;

        public RemoteCvDetector(ServerConfig config)
        {
            _config = config;
            _client = new CVServerClient(config);
        }

        public bool IsReady => _config != null && !string.IsNullOrWhiteSpace(_config.ResolveBaseUrl());

        public Task<DetectionResult> DetectAsync(CaptureFrame frame, string prompt)
            => _client.DetectAsync(frame.jpegBytes, prompt);

        public Task<SegmentationResult> SegmentAsync(CaptureFrame frame, Rect roi)
            => _client.SegmentAsync(frame.jpegBytes, string.Empty);

        public Task<FlorenceResult> CaptionAsync(CaptureFrame frame)
            => _client.FlorenceAsync(frame.jpegBytes, "<CAPTION>");
    }
}
