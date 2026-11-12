using NUnit.Framework;
using UnityEngine;
using XRScan.Core;
using XRScan.Mapping;

namespace XRScan.Tests.EditMode
{
    /// <summary>
    /// Verifies the camera-intrinsics unprojection at the heart of scanning: a 2D detection pixel
    /// becomes a correctly-oriented world ray. Pure math, no scene required.
    /// </summary>
    public class ScanMathTests
    {
        private static Matrix4x4 Intrinsics(float fx, float fy, float cx, float cy)
        {
            var m = Matrix4x4.zero;
            m.m00 = fx; m.m11 = fy; m.m02 = cx; m.m12 = cy; m.m22 = 1f;
            return m;
        }

        private static CaptureFrame Frame(Matrix4x4 intr, Quaternion rot)
            => new CaptureFrame
            {
                imageWidth = 640,
                imageHeight = 480,
                cameraPose = new Pose(Vector3.zero, rot),
                cameraIntrinsics = intr
            };

        [Test]
        public void RayFromIntrinsics_CenterPixel_PointsForward()
        {
            var ray = ViewportRaycaster.BuildRayFromIntrinsics(
                Frame(Intrinsics(500, 500, 320, 240), Quaternion.identity), new Vector2(320, 240));

            Assert.AreEqual(0f, ray.direction.x, 1e-3f);
            Assert.AreEqual(0f, ray.direction.y, 1e-3f);
            Assert.Greater(ray.direction.z, 0.99f);
        }

        [Test]
        public void RayFromIntrinsics_RightOfCenter_TiltsPositiveX()
        {
            var ray = ViewportRaycaster.BuildRayFromIntrinsics(
                Frame(Intrinsics(500, 500, 320, 240), Quaternion.identity), new Vector2(440, 240));

            Assert.Greater(ray.direction.x, 0.1f, "a detection right of centre should aim +x");
        }

        [Test]
        public void RayFromIntrinsics_NoIntrinsics_FallsBackToPoseForward()
        {
            // No usable intrinsics -> ray follows the camera pose. Facing +x (yaw 90).
            var frame = new CaptureFrame { cameraPose = new Pose(Vector3.zero, Quaternion.Euler(0, 90, 0)) };
            var ray = ViewportRaycaster.BuildRayFromIntrinsics(frame, Vector2.zero);

            Assert.Greater(ray.direction.x, 0.9f);
        }
    }
}
