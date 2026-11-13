using System.Threading.Tasks;
using UnityEngine;
using XRScan.Core;

namespace XRScan.Vision
{
    /// <summary>IObjectDetector backed by the remote CV server. The default Phase-1 detector;
    /// swaps to an on-device Sentis detector (Phase 4) with no downstream change. It is ready only while
    /// the server endpoint is usable; otherwise <see cref="UnavailableReason"/> says why, so the app can
    /// show that instead of scanning.</summary>
    public class RemoteCvDetector : IObjectDetector
    {
        private readonly CVServerClient _client;
        private readonly ServerConfig _config;

        public RemoteCvDetector(ServerConfig config)
        {
            _config = config;
            _client = new CVServerClient(config);
        }

        public bool IsReady => _config != null && _config.ResolveEndpoint().IsUsable;

        /// <summary>Why scanning is off while <see cref="IsReady"/> is false; empty when it is ready.</summary>
        public string UnavailableReason
        {
            get
            {
                if (_config == null) return NotConfiguredMessage;
                var check = _config.ResolveEndpoint();
                if (check.IsUsable) return string.Empty;
                return check.Status == EndpointStatus.NotConfigured
                    ? NotConfiguredMessage
                    : "Scanning is off: the vision server address cannot be used. " + check.Message;
            }
        }

        public const string NotConfiguredMessage =
            "Scanning is off: no vision server is set. Press the menu button to open Settings and enter it.";

        public Task<DetectionResult> DetectAsync(CaptureFrame frame, string prompt)
            => _client.DetectAsync(frame.jpegBytes, prompt);

        public Task<SegmentationResult> SegmentAsync(CaptureFrame frame, Rect roi)
            => _client.SegmentAsync(frame.jpegBytes, string.Empty);

        public Task<FlorenceResult> CaptionAsync(CaptureFrame frame)
            => _client.FlorenceAsync(frame.jpegBytes, "<CAPTION>");
    }
}
