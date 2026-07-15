using System;
using System.Collections.Generic;

namespace XRScan.Core
{
    /// <summary>Run-length-encoded segmentation mask as returned by the CV server (COCO RLE).</summary>
    [Serializable]
    public class RleMask
    {
        public List<int> size = new List<int>();
        public string counts;
    }

    [Serializable]
    public class SegmentAnnotation
    {
        public string label;
        public float score;
        public float[] bbox;
        public RleMask mask;
    }

    /// <summary>Response shape of the CV server /v1/segment (Grounded-SAM 2) endpoint.</summary>
    [Serializable]
    public class SegmentationResult
    {
        public List<SegmentAnnotation> annotations = new List<SegmentAnnotation>();
        public List<int> image_size = new List<int>();
    }
}
