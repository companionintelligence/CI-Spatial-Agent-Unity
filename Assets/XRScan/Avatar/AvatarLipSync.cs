using UnityEngine;

namespace XRScan.Avatar
{
    /// <summary>
    /// Phoneme-accurate lip-sync via uLipSync: analyzes the TTS AudioSource in real time and drives
    /// the RPM / Oculus visemes on the face (and teeth) mesh — web-parity mouth shapes, replacing the
    /// amplitude-only VisemeController. Self-wires uLipSync + uLipSyncBlendShape at Start, mapping the
    /// profile's phonemes (A/I/U/E/O/S) onto the RPM viseme_* blendshapes.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AvatarLipSync : MonoBehaviour
    {
        [SerializeField] private uLipSync.Profile profile;
        [SerializeField] private SkinnedMeshRenderer faceRenderer;
        [SerializeField] private SkinnedMeshRenderer teethRenderer;
        [SerializeField, Range(0f, 2f)] private float maxWeight = 1.2f;

        // uLipSync profile phoneme -> RPM Oculus viseme blendshape name.
        private static readonly (string phoneme, string viseme)[] VisemeMap =
        {
            ("A", "viseme_aa"),
            ("I", "viseme_I"),
            ("U", "viseme_U"),
            ("E", "viseme_E"),
            ("O", "viseme_O"),
            ("S", "viseme_SS"),
        };

        private uLipSync.uLipSync _lipSync;

        private void Start() => Configure();

        public void Configure(uLipSync.Profile overrideProfile = null, SkinnedMeshRenderer face = null, SkinnedMeshRenderer teeth = null)
        {
            if (overrideProfile != null) profile = overrideProfile;
            if (face != null) faceRenderer = face;
            if (teeth != null) teethRenderer = teeth;

            if (faceRenderer == null)
            {
                Debug.LogWarning("[AvatarLipSync] No face renderer; lip-sync disabled.");
                return;
            }

            _lipSync = GetComponent<uLipSync.uLipSync>();
            if (_lipSync == null) _lipSync = gameObject.AddComponent<uLipSync.uLipSync>();
            _lipSync.profile = profile;

            BindMesh(faceRenderer);
            if (teethRenderer != null) BindMesh(teethRenderer);

            if (profile == null)
                Debug.LogWarning("[AvatarLipSync] No uLipSync profile assigned; phoneme detection will be weak.");
        }

        private void BindMesh(SkinnedMeshRenderer smr)
        {
            if (smr.sharedMesh == null) return;

            var bs = smr.gameObject.GetComponent<uLipSync.uLipSyncBlendShape>();
            if (bs == null) bs = smr.gameObject.AddComponent<uLipSync.uLipSyncBlendShape>();
            bs.skinnedMeshRenderer = smr;
            bs.maxBlendShapeValue = 100f;
            bs.blendShapes.Clear();

            foreach (var (phoneme, viseme) in VisemeMap)
            {
                int index = smr.sharedMesh.GetBlendShapeIndex(viseme);
                if (index < 0) continue;
                bs.blendShapes.Add(new uLipSync.uLipSyncBlendShape.BlendShapeInfo
                {
                    phoneme = phoneme,
                    index = index,
                    maxWeight = maxWeight
                });
            }

            _lipSync.onLipSyncUpdate.RemoveListener(bs.OnLipSyncUpdate);
            _lipSync.onLipSyncUpdate.AddListener(bs.OnLipSyncUpdate);
        }
    }
}
