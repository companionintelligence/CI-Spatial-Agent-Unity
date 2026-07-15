using UnityEngine;

namespace XRScan.Avatar
{
    /// <summary>
    /// Owns the avatar instance for the slice. Either uses an avatar already placed in the scene
    /// (assign avatarRoot) or instantiates a baked prefab, then exposes the head transform and the
    /// face SkinnedMeshRenderer so gaze + viseme controllers can bind. Runtime Ready Player Me URL
    /// loading is added in a later phase; the slice uses a baked prefab / placeholder.
    /// </summary>
    public class AvatarLoadController : MonoBehaviour
    {
        [SerializeField] private GameObject avatarPrefab;
        [SerializeField] private Transform avatarRoot;
        [SerializeField] private Transform headOverride;

        public Transform AvatarRoot => avatarRoot;
        public Transform Head { get; private set; }
        public SkinnedMeshRenderer FaceRenderer { get; private set; }
        public bool IsLoaded => avatarRoot != null;

        private void Awake() => EnsureAvatar();

        public void EnsureAvatar()
        {
            if (avatarRoot == null && avatarPrefab != null)
            {
                var instance = Instantiate(avatarPrefab, transform);
                avatarRoot = instance.transform;
            }
            if (avatarRoot == null) return;

            Head = headOverride != null ? headOverride : FindHead(avatarRoot);
            FaceRenderer = FindFaceRenderer(avatarRoot);
        }

        private static Transform FindHead(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                if (t.name.ToLowerInvariant().Contains("head")) return t;
            }
            return root;
        }

        private static SkinnedMeshRenderer FindFaceRenderer(Transform root)
        {
            // Prefer the head/face mesh (RPM: "Wolf3D_Head"), since eyes/teeth carry the same
            // blendshape set — driving eyes wouldn't move the mouth. Fall back to max-blendshapes.
            SkinnedMeshRenderer best = null;
            int bestBlendshapes = 0;
            SkinnedMeshRenderer head = null;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int bs = smr.sharedMesh != null ? smr.sharedMesh.blendShapeCount : 0;
                if (bs <= 0) continue;
                string n = smr.name.ToLowerInvariant();
                if (head == null && n.Contains("head") && !n.Contains("eye")) head = smr;
                if (bs > bestBlendshapes) { bestBlendshapes = bs; best = smr; }
            }
            return head != null ? head : best;
        }
    }
}
