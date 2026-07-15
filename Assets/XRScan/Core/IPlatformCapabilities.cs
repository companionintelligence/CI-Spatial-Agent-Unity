namespace XRScan.Core
{
    /// <summary>Identifies the active XR runtime / vendor at boot.</summary>
    public enum XRVendor
    {
        Unknown,
        MetaQuest,
        Pico,
        Desktop
    }

    /// <summary>
    /// Single runtime switch the whole app consults to degrade gracefully across
    /// headsets. Sniffs the active OpenXR runtime + enabled feature group at boot.
    /// The scan pipeline gates live-camera CV on <see cref="HasRgbCameraAccess"/>,
    /// so consumer Pico (no per-app RGB frames) automatically falls back to
    /// scene-understanding pins with no per-vendor branching in app code.
    /// </summary>
    public interface IPlatformCapabilities
    {
        XRVendor Vendor { get; }

        /// <summary>True when this platform grants per-app RGB camera frames (Quest 3), false on consumer Pico.</summary>
        bool HasRgbCameraAccess { get; }

        /// <summary>True when a room / scene mesh is available for NavMesh baking + occlusion.</summary>
        bool HasSceneMesh { get; }

        /// <summary>True when semantic scene planes (floor / wall / table labels) are available.</summary>
        bool HasSemanticPlanes { get; }
    }
}
