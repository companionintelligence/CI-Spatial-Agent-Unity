using XRScan.Core;

namespace XRScan.Platform
{
    /// <summary>Selects the active camera provider for the current platform — the one place
    /// camera platform-branching lives.</summary>
    public static class CameraProviderFactory
    {
        /// <summary>
        /// Picks a provider: an explicit mock (Editor / testing) wins; otherwise the RGB-capable
        /// AR Foundation provider when the platform grants frames; otherwise the desktop webcam.
        /// </summary>
        public static ICameraProvider Select(
            IPlatformCapabilities capabilities,
            ICameraProvider mock,
            ICameraProvider arFoundation,
            ICameraProvider desktop)
        {
            if (mock != null) return mock;
            if (capabilities != null && capabilities.HasRgbCameraAccess && arFoundation != null && arFoundation.SupportsRgbFrames)
            {
                return arFoundation;
            }
            return desktop;
        }
    }
}
