using System;
using System.Collections.Generic;

namespace XRScan.Core
{
    /// <summary>
    /// Response shape of the CV server /v1/detect endpoint: parallel arrays of
    /// bounding boxes, labels, and confidence scores. Each bbox is [x1,y1,x2,y2],
    /// either pixel or normalized (see <see cref="BoundingBoxUtils"/>).
    /// </summary>
    [Serializable]
    public class DetectionResult
    {
        public List<float[]> bboxes = new List<float[]>();
        public List<string> labels = new List<string>();
        public List<float> scores = new List<float>();
    }
}
