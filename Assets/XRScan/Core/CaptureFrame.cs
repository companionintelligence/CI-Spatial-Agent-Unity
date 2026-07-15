using System;
using UnityEngine;

namespace XRScan.Core
{
    /// <summary>
    /// An immutable snapshot of a single camera frame: encoded pixels plus the
    /// pose + intrinsics needed to unproject 2D detections into world space.
    /// </summary>
    [Serializable]
    public class CaptureFrame
    {
        public string id;
        public string sessionId;
        public int frameIndex;
        public double timestamp;
        public byte[] jpegBytes;
        public int imageWidth;
        public int imageHeight;
        public Pose cameraPose;
        public Matrix4x4 cameraIntrinsics;

        public bool HasIntrinsics => cameraIntrinsics.m00 > 0f && cameraIntrinsics.m11 > 0f;

        public static CaptureFrame Create(byte[] bytes, int width, int height, Pose pose, Matrix4x4 intrinsics)
        {
            return new CaptureFrame
            {
                timestamp = Time.timeAsDouble,
                jpegBytes = bytes,
                imageWidth = width,
                imageHeight = height,
                cameraPose = pose,
                cameraIntrinsics = intrinsics
            };
        }
    }
}
