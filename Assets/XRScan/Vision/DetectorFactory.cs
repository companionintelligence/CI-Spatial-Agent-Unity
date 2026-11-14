using XRScan.Core;

namespace XRScan.Vision
{
    /// <summary>
    /// Hybrid detector selection — the "use the server if needed" policy. Prefers the on-device
    /// detector when it's ready (fully untethered), else the remote CV server whenever a ServerConfig
    /// exists, configured or not: an unconfigured server reports itself as not ready
    /// (<see cref="RemoteCvDetector.UnavailableReason"/>), so nothing stands in for it with invented
    /// detections. The fallback is used only without a ServerConfig.
    /// </summary>
    public static class DetectorFactory
    {
        public static IObjectDetector Select(ServerConfig serverConfig, IObjectDetector onDevice, IObjectDetector fallback)
        {
            if (onDevice != null && onDevice.IsReady) return onDevice;
            if (serverConfig != null) return new RemoteCvDetector(serverConfig);
            return fallback;
        }
    }
}
