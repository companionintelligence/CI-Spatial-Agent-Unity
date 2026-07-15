using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using XRScan.Core;

namespace XRScan.Platform
{
    /// <summary>
    /// IRoomMeshProvider backed by AR Foundation's ARMeshManager. The generated room-mesh
    /// GameObjects live as children of the manager; NavMeshBaker consumes them. Cross-platform.
    /// </summary>
    public class ARFoundationRoomMeshProvider : MonoBehaviour, IRoomMeshProvider
    {
        [SerializeField] private ARMeshManager meshManager;

        public event Action OnMeshUpdated;

        public bool IsAvailable => meshManager != null && meshManager.enabled;

        private void Awake()
        {
            if (meshManager == null) meshManager = FindAnyObjectByType<ARMeshManager>();
        }

        private void OnEnable()
        {
            if (meshManager != null) meshManager.meshesChanged += HandleMeshesChanged;
        }

        private void OnDisable()
        {
            if (meshManager != null) meshManager.meshesChanged -= HandleMeshesChanged;
        }

        private void HandleMeshesChanged(ARMeshesChangedEventArgs _) => OnMeshUpdated?.Invoke();

        public IReadOnlyList<MeshFilter> GetRoomMeshes()
        {
            if (meshManager == null) return Array.Empty<MeshFilter>();
            return meshManager.GetComponentsInChildren<MeshFilter>();
        }

        public bool TryGetRoomBounds(out Bounds bounds)
        {
            bounds = default;
            var meshes = GetRoomMeshes();
            if (meshes.Count == 0) return false;

            bool init = false;
            foreach (var mf in meshes)
            {
                if (mf == null) continue;
                var r = mf.GetComponent<Renderer>();
                if (r == null) continue;
                if (!init) { bounds = r.bounds; init = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return init;
        }
    }
}
