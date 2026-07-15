using UnityEngine;
using UnityEngine.AI;

namespace XRScan.Avatar
{
    /// <summary>
    /// Drives the avatar around the room via a NavMeshAgent and reports normalized movement speed
    /// (for an Animator's locomotion blend, when a real rig is present). Works on the placeholder
    /// too — the capsule simply slides to the destination once a NavMesh exists.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class LocomotionController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private float arriveDistance = 0.4f;

        private NavMeshAgent _agent;
        private int _speedHash;

        public bool HasDestination { get; private set; }
        public bool IsMoving => _agent != null && _agent.velocity.sqrMagnitude > 0.01f;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _speedHash = Animator.StringToHash(speedParam);
        }

        /// <summary>Walk to a world point (snapped to the NavMesh). No-op if no NavMesh is baked yet.</summary>
        public bool WalkTo(Vector3 worldPoint)
        {
            if (_agent == null || !_agent.isOnNavMesh) return false;
            if (NavMesh.SamplePosition(worldPoint, out var hit, 1.5f, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
                HasDestination = true;
                return true;
            }
            return false;
        }

        public void Stop()
        {
            if (_agent != null && _agent.isOnNavMesh) _agent.ResetPath();
            HasDestination = false;
        }

        private void Update()
        {
            if (_agent == null) return;

            if (HasDestination && !_agent.pathPending && _agent.remainingDistance <= arriveDistance)
            {
                HasDestination = false;
            }

            if (animator != null && _agent.speed > 0f)
            {
                animator.SetFloat(_speedHash, _agent.velocity.magnitude / _agent.speed);
            }
        }
    }
}
