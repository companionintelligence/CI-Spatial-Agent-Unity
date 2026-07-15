using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XRScan.Core;
using XRScan.Mapping;

namespace XRScan.Agent
{
    /// <summary>Thin query facade over the pin store; grows into the LLM's grounding context in Phase 2.</summary>
    public class SceneKnowledgeBase : MonoBehaviour
    {
        [SerializeField] private SpatialPinManager pinManager;

        private static readonly IReadOnlyList<SpatialPin> Empty = new List<SpatialPin>();

        public IReadOnlyList<SpatialPin> Pins => pinManager != null ? pinManager.Pins : Empty;

        public SpatialPin FindByLabel(string label)
            => pinManager == null ? null : pinManager.Pins.FirstOrDefault(p =>
                string.Equals(p.label, label, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.semanticLabel, label, System.StringComparison.OrdinalIgnoreCase));

        public SpatialPin GetNearest(Vector3 worldPoint)
        {
            if (pinManager == null) return null;
            SpatialPin nearest = null;
            float best = float.MaxValue;
            foreach (var p in pinManager.Pins)
            {
                float d = (p.position - worldPoint).sqrMagnitude;
                if (d < best) { best = d; nearest = p; }
            }
            return nearest;
        }

        /// <summary>Grounding context string handed to the LLM in Phase 2.</summary>
        public string BuildContext()
        {
            if (pinManager == null || pinManager.Pins.Count == 0) return "The room has no scanned objects yet.";
            var items = pinManager.Pins.Select(p => $"- {p.label} at {p.position.ToString("F1")}");
            return "Scanned objects:\n" + string.Join("\n", items);
        }
    }
}
