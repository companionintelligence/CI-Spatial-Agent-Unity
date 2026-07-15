using UnityEngine;
using XRScan.Core;

namespace XRScan.Platform
{
    /// <summary>
    /// Runtime capability sniffer. Detects the active XR vendor from the loaded XR device /
    /// device model and reports what the app may use, so the scan pipeline degrades gracefully
    /// (e.g. consumer Pico has no per-app RGB frames). Refined per-vendor in later phases.
    /// </summary>
    public class PlatformCapabilities : IPlatformCapabilities
    {
        public XRVendor Vendor { get; }
        public bool HasRgbCameraAccess { get; }
        public bool HasSceneMesh { get; }
        public bool HasSemanticPlanes { get; }

        public PlatformCapabilities()
        {
            Vendor = DetectVendor();
            switch (Vendor)
            {
                case XRVendor.MetaQuest:
                    HasRgbCameraAccess = true; HasSceneMesh = true; HasSemanticPlanes = true; break;
                case XRVendor.Desktop:
                    HasRgbCameraAccess = true; HasSceneMesh = false; HasSemanticPlanes = false; break;
                case XRVendor.Pico:
                    // Consumer Pico: passthrough render yes, per-app RGB frames enterprise-locked.
                    HasRgbCameraAccess = false; HasSceneMesh = true; HasSemanticPlanes = true; break;
                default:
                    HasRgbCameraAccess = false; HasSceneMesh = false; HasSemanticPlanes = false; break;
            }
        }

        private static XRVendor DetectVendor()
        {
            if (Application.isEditor)
            {
                return XRVendor.Desktop;
            }

            string id = (UnityEngine.XR.XRSettings.loadedDeviceName + " " + SystemInfo.deviceModel).ToLowerInvariant();
            if (id.Contains("quest") || id.Contains("oculus") || id.Contains("meta")) return XRVendor.MetaQuest;
            if (id.Contains("pico")) return XRVendor.Pico;
            return XRVendor.Unknown;
        }
    }
}
