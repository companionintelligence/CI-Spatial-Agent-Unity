using XRScan.Core;

namespace XRScan.Vision
{
    /// <summary>
    /// Hybrid detector selection — the "use the server if needed" policy. Prefers the on-device
    /// detector when it's ready (fully untethered), else the remote CV server when a base URL is
    /// configured, else the supplied fallback (e.g. a mock for the Editor).
    /// </summary>
    public static class DetectorFactory
    {
        public static IObjectDetector Select(ServerConfig serverConfig, IObjectDetector onDevice, IObjectDetector fallback)
        {
            if (onDevice != null && onDevice.IsReady) return onDevice;
            if (serverConfig != null && !string.IsNullOrWhiteSpace(serverConfig.ResolveBaseUrl())) return new RemoteCvDetector(serverConfig);
            return fallback;
        }
    }
}
