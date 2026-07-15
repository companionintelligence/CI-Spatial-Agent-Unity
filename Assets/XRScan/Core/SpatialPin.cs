using System;
using UnityEngine;

namespace XRScan.Core
{
    /// <summary>Identifies which capture system produced a pin.</summary>
    public enum PinSource
    {
        /// <summary>Produced by the remote CV server pipeline (GroundingDINO / SAM2 / Florence2).</summary>
        CvServer,
        /// <summary>Produced by an on-device detector (Sentis YOLO).</summary>
        OnDeviceDetector,
        /// <summary>Produced by XR scene understanding (AR Foundation planes / semantic labels).</summary>
        SceneUnderstanding
    }

    /// <summary>Records which raycast strategy produced the 3D placement.</summary>
    public enum RaycastHitType
    {
        ArPlane,
        Environment,
        ScenePlane,
        Physics,
        Fallback
    }

    [Serializable]
    public struct SerializableRect
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public SerializableRect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
    }

    /// <summary>
    /// A unified spatial observation. Every capture source (CV server, on-device detector,
    /// scene understanding) writes this same type into the shared pin store, so downstream
    /// rendering / avatar / export code stays source-agnostic.
    /// </summary>
    [Serializable]
    public class SpatialPin
    {
        // Identity
        public string label;
        public string semanticLabel;
        public float score;
        public PinSource source;

        // Spatial
        public Vector3 position;
        public Vector3 normal;
        public Quaternion rotation = Quaternion.identity;

        // 2D (detector sources only)
        public SerializableRect bboxPixels;
        public SerializableRect bboxNormalized;

        // Geometry (scene-understanding sources only)
        public Vector3 planeSize;
        public Vector3 volumeSize;
        public string anchorUuid;

        // Metadata
        public string sourcePrompt;
        public double timestamp;
        public string captureId;
        public string captureSessionId;
        public int captureFrameIndex;
        public float placementConfidence;
        public RaycastHitType hitType;
        public int trackId;
    }
}
