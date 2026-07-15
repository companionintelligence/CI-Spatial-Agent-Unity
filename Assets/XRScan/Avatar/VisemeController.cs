using UnityEngine;

namespace XRScan.Avatar
{
    /// <summary>
    /// Drives a mouth-open blendshape from the live audio amplitude of an AudioSource, giving a
    /// basic lip-flap while the avatar speaks. This is the slice-level placeholder; the full
    /// uLipSync viseme mapping (Ready Player Me Oculus visemes) replaces it in a later phase.
    /// </summary>
    public class VisemeController : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private SkinnedMeshRenderer faceRenderer;
        [SerializeField] private string mouthOpenBlendShape = "mouthOpen";
        [SerializeField] private string jawOpenBlendShape = "jawOpen";
        [SerializeField] private float gain = 300f;
        [SerializeField] private float smoothing = 12f;

        private int _blendIndex = -1;
        private float _current;
        private readonly float[] _samples = new float[256];

        private void Start() => ResolveBlendShape();

        public void Bind(AudioSource source, SkinnedMeshRenderer renderer)
        {
            audioSource = source;
            faceRenderer = renderer;
            ResolveBlendShape();
        }

        private void ResolveBlendShape()
        {
            _blendIndex = -1;
            if (faceRenderer == null || faceRenderer.sharedMesh == null) return;

            var mesh = faceRenderer.sharedMesh;
            _blendIndex = FindBlendShape(mesh, mouthOpenBlendShape);
            if (_blendIndex < 0) _blendIndex = FindBlendShape(mesh, jawOpenBlendShape);
            if (_blendIndex < 0 && mesh.blendShapeCount > 0)
            {
                Debug.Log($"[VisemeController] No '{mouthOpenBlendShape}'/'{jawOpenBlendShape}' blendshape on {faceRenderer.name}; lip-flap disabled.");
            }
        }

        private static int FindBlendShape(Mesh mesh, string name)
        {
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                if (mesh.GetBlendShapeName(i).IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0) return i;
            }
            return -1;
        }

        private void Update()
        {
            if (faceRenderer == null || _blendIndex < 0) return;

            float target = 0f;
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.GetOutputData(_samples, 0);
                float sum = 0f;
                for (int i = 0; i < _samples.Length; i++) sum += _samples[i] * _samples[i];
                float rms = Mathf.Sqrt(sum / _samples.Length);
                target = Mathf.Clamp(rms * gain, 0f, 100f);
            }

            _current = Mathf.Lerp(_current, target, Time.deltaTime * smoothing);
            faceRenderer.SetBlendShapeWeight(_blendIndex, _current);
        }
    }
}
