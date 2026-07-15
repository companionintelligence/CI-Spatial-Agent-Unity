using System;
using System.Collections.Generic;
using UnityEngine;

namespace XRScan.Core
{
    /// <summary>
    /// Abstracts the room-mesh source (AR Foundation meshing, Meta / Pico spatial mesh) that
    /// feeds runtime NavMesh baking + occlusion. Cross-platform via AR Foundation's ARMeshManager.
    /// </summary>
    public interface IRoomMeshProvider
    {
        bool IsAvailable { get; }
        event Action OnMeshUpdated;
        IReadOnlyList<MeshFilter> GetRoomMeshes();
        bool TryGetRoomBounds(out Bounds bounds);
    }
}
