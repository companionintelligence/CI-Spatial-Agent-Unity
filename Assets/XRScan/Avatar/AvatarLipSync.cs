using UnityEngine;

namespace XRScan.Avatar
{
    /// <summary>
    /// Phoneme-accurate lip-sync via uLipSync: analyzes the TTS AudioSource in real time and drives
    /// the Oculus-style visemes on the face (and, if the model has one, teeth) mesh — web-parity mouth
    /// shapes, replacing the amplitude-only VisemeController. Self-wires uLipSync + uLipSyncBlendShape
    /// at Start, mapping the profile's phonemes (A/I/U/E/O/S) onto the <c>viseme_*</c> blendshapes
    /// (see <see cref="VisemeBlendShapes"/>; the CI Companion avatar carries all 15).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AvatarLipSync : MonoBehaviour
    {
        [SerializeField] private uLipSync.Profile profile;
        [SerializeField] private SkinnedMeshRenderer faceRenderer;
        [SerializeField] private SkinnedMeshRenderer teethRenderer;
        // Oculus-style viseme blendshapes are authored for 0–100%. uLipSync applies
        // weight = ratio * maxWeight * volume * 100, so anything above 1.0 over-extrudes the mesh
        // and it distorts — keep this at (or below) 1.0.
        [SerializeField, Range(0f, 1f)] private float maxWeight = 1.0f;
        // Softens transitions: blend the visemes by their phoneme ratios rather than snapping to the
        // single loudest one (which looks robotic), and damp the per-frame weight changes.
        [SerializeField] private bool blendVisemes = true;
        [SerializeField, Range(0f, 0.3f)] private float smoothness = 0.08f;

        // uLipSync profile phoneme -> Oculus-style viseme blendshape name.
        private static readonly (string phoneme, string viseme)[] VisemeMap =
        {
            ("A", "viseme_aa"),
            ("I", "viseme_I"),
            ("U", "viseme_U"),
            ("E", "viseme_E"),
            ("O", "viseme_O"),
            ("S", "viseme_SS"),
        };

        /// <summary>The blendshape names lip-sync binds to; an avatar's face mesh should carry all of them.</summary>
        public static System.Collections.Generic.IEnumerable<string> VisemeBlendShapes
        {
            get { foreach (var (_, viseme) in VisemeMap) yield return viseme; }
        }

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

            maxWeight = Mathf.Clamp01(maxWeight); // guard against a stale >1 serialized value

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
            bs.usePhonemeBlend = blendVisemes;
            bs.smoothness = smoothness;
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
