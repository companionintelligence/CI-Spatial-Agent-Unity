using System;
using System.Collections.Generic;
using UnityEngine;

namespace XRScan.Core
{
    /// <summary>A semantic scene plane (floor / wall / table / ceiling / ...) in world space.</summary>
    public struct ScenePlane
    {
        public Vector3 center;
        public Vector3 normal;
        public Vector2 size;
        public string label;
    }

    /// <summary>
    /// Generalizes XR scene understanding (AR Foundation planes, Meta / Pico semantic anchors)
    /// behind one contract. On Pico this feeds the scan-fallback (scene-semantics pins) when
    /// live-camera CV is unavailable. Cross-platform: the same ARFoundationSceneProvider serves
    /// both headsets; vendor-specific enrichers bind later without changing app logic.
    /// </summary>
    public interface ISceneProvider
    {
        bool IsAvailable { get; }
        event Action OnSceneUpdated;
        IReadOnlyList<ScenePlane> GetPlanes();
        bool TryGetSemanticLabel(Vector3 worldPoint, out string label);
    }
}
