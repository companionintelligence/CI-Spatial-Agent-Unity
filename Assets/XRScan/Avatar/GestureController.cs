using UnityEngine;

namespace XRScan.Avatar
{
    /// <summary>
    /// Points the avatar's arm (or a designated "pointer" transform) toward a world target — a
    /// rig-free damped aim that works on the placeholder and upgrades to Animation Rigging
    /// TwoBoneIKConstraint on a real humanoid. Also raises OnGesture for animation triggers.
    /// </summary>
    public class GestureController : MonoBehaviour
    {
        [SerializeField] private Transform pointer;
        [SerializeField] private float aimSpeed = 6f;
        [SerializeField] private float holdSeconds = 3f;

        private Vector3? _target;
        private float _holdUntil;

        /// <summary>Point at a world position for a few seconds.</summary>
        public void PointAt(Vector3 worldPoint)
        {
            _target = worldPoint;
            _holdUntil = Time.time + holdSeconds;
        }

        public void ClearPoint() => _target = null;

        public void SetPointer(Transform t) => pointer = t;

        private void LateUpdate()
        {
            if (pointer == null) return;

            if (_target.HasValue && Time.time < _holdUntil)
            {
                Vector3 dir = _target.Value - pointer.position;
                if (dir.sqrMagnitude > 1e-4f)
                {
                    var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    pointer.rotation = Quaternion.Slerp(pointer.rotation, rot, Time.deltaTime * aimSpeed);
                }
            }
        }
    }
}
