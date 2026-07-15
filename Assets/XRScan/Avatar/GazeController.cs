using UnityEngine;

namespace XRScan.Avatar
{
    /// <summary>
    /// Damped look-at: rotates the avatar body (and optionally the head) toward a target world
    /// point, defaulting to the user's camera. A rig-free implementation that works on a
    /// placeholder or a full humanoid; upgraded to Animation Rigging MultiAimConstraint later.
    /// </summary>
    public class GazeController : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private Transform head;
        [SerializeField] private float turnSpeed = 4f;
        [SerializeField] private bool yawOnlyForBody = true;

        private Vector3? _target;
        private float _holdUntil;

        private void Awake() { if (body == null) body = transform; }

        /// <summary>Look at a world position; holds for <paramref name="holdSeconds"/> then relaxes to the user.</summary>
        public void LookAt(Vector3 worldPoint, float holdSeconds = 4f)
        {
            _target = worldPoint;
            _holdUntil = Time.time + holdSeconds;
        }

        public void LookAtUser() => _target = null;

        public void SetRig(Transform bodyTransform, Transform headTransform)
        {
            if (bodyTransform != null) body = bodyTransform;
            head = headTransform;
        }

        private void LateUpdate()
        {
            Vector3 focus;
            if (_target.HasValue && Time.time < _holdUntil)
            {
                focus = _target.Value;
            }
            else
            {
                var cam = Camera.main;
                if (cam == null) return;
                focus = cam.transform.position;
            }

            if (body != null)
            {
                Vector3 dir = focus - body.position;
                if (yawOnlyForBody) dir.y = 0f;
                if (dir.sqrMagnitude > 1e-4f)
                {
                    var target = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    body.rotation = Quaternion.Slerp(body.rotation, target, Time.deltaTime * turnSpeed);
                }
            }

            if (head != null)
            {
                Vector3 dir = focus - head.position;
                if (dir.sqrMagnitude > 1e-4f)
                {
                    var target = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    head.rotation = Quaternion.Slerp(head.rotation, target, Time.deltaTime * turnSpeed * 1.5f);
                }
            }
        }
    }
}
