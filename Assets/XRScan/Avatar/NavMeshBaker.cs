using UnityEngine;
using Unity.AI.Navigation;
using XRScan.Core;

namespace XRScan.Avatar
{
    /// <summary>
    /// Rebuilds a runtime NavMesh over the room mesh as it arrives from an IRoomMeshProvider, so
    /// the avatar can walk the real space. Throttled to avoid thrashing while the mesh streams in.
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    public class NavMeshBaker : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour roomMeshBehaviour; // IRoomMeshProvider
        [SerializeField] private float minRebakeInterval = 3f;

        private NavMeshSurface _surface;
        private IRoomMeshProvider _room;
        private float _nextAllowedBake;
        private bool _dirty;

        private void Awake() => _surface = GetComponent<NavMeshSurface>();

        private void OnEnable()
        {
            _room = roomMeshBehaviour as IRoomMeshProvider;
            if (_room != null) _room.OnMeshUpdated += MarkDirty;
        }

        private void OnDisable()
        {
            if (_room != null) _room.OnMeshUpdated -= MarkDirty;
        }

        public void Bind(IRoomMeshProvider provider)
        {
            if (_room != null) _room.OnMeshUpdated -= MarkDirty;
            _room = provider;
            if (_room != null) _room.OnMeshUpdated += MarkDirty;
        }

        private void MarkDirty() => _dirty = true;

        private void Update()
        {
            if (!_dirty || Time.time < _nextAllowedBake) return;
            _dirty = false;
            _nextAllowedBake = Time.time + minRebakeInterval;
            Rebake();
        }

        public void Rebake()
        {
            if (_surface == null) return;
            _surface.BuildNavMesh();
        }
    }
}
