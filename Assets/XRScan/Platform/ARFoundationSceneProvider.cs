using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using XRScan.Core;

namespace XRScan.Platform
{
    /// <summary>
    /// ISceneProvider backed by AR Foundation's ARPlaneManager — the shared cross-platform scene
    /// source (works on Quest via Meta-OpenXR and on Pico via PICO OpenXR). Replaces CompanionScan's
    /// fully Meta-coupled MetaSceneCollector; vendor semantic enrichment binds separately.
    /// </summary>
    public class ARFoundationSceneProvider : MonoBehaviour, ISceneProvider
    {
        [SerializeField] private ARPlaneManager planeManager;

        public event Action OnSceneUpdated;

        public bool IsAvailable => planeManager != null && planeManager.enabled;

        private void Awake()
        {
            if (planeManager == null) planeManager = FindAnyObjectByType<ARPlaneManager>();
        }

        private void OnEnable()
        {
            if (planeManager != null) planeManager.trackablesChanged.AddListener(HandlePlanesChanged);
        }

        private void OnDisable()
        {
            if (planeManager != null) planeManager.trackablesChanged.RemoveListener(HandlePlanesChanged);
        }

        private void HandlePlanesChanged(ARTrackablesChangedEventArgs<ARPlane> _) => OnSceneUpdated?.Invoke();

        public IReadOnlyList<ScenePlane> GetPlanes()
        {
            var list = new List<ScenePlane>();
            if (planeManager == null) return list;

            foreach (var p in planeManager.trackables)
            {
                list.Add(new ScenePlane
                {
                    center = p.transform.position,
                    normal = p.normal,
                    size = p.size,
                    label = p.classifications.ToString()
                });
            }
            return list;
        }

        public bool TryGetSemanticLabel(Vector3 worldPoint, out string label)
        {
            label = "unknown";
            if (planeManager == null) return false;

            float best = float.MaxValue;
            bool found = false;
            foreach (var p in planeManager.trackables)
            {
                float d = (p.transform.position - worldPoint).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    label = p.classifications.ToString();
                    found = true;
                }
            }
            return found;
        }
    }
}
