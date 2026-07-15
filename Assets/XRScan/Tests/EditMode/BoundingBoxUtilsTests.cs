using NUnit.Framework;
using XRScan.Core;

namespace XRScan.Tests.EditMode
{
    public class BoundingBoxUtilsTests
    {
        [Test]
        public void Normalize_PixelBox_ConvertsToUnitRange()
        {
            bool ok = BoundingBoxUtils.TryNormalize(new float[] { 100, 100, 300, 300 }, 400, 400, out var r);
            Assert.IsTrue(ok);
            Assert.AreEqual(0.25f, r.x, 1e-4f);
            Assert.AreEqual(0.25f, r.y, 1e-4f);
            Assert.AreEqual(0.5f, r.width, 1e-4f);
            Assert.AreEqual(0.5f, r.height, 1e-4f);
        }

        [Test]
        public void Normalize_AlreadyNormalized_PassesThrough()
        {
            bool ok = BoundingBoxUtils.TryNormalize(new float[] { 0.4f, 0.4f, 0.6f, 0.6f }, 640, 480, out var r);
            Assert.IsTrue(ok);
            Assert.AreEqual(0.2f, r.width, 1e-4f);
            Assert.AreEqual(0.2f, r.height, 1e-4f);
        }

        [Test]
        public void Normalize_DegenerateBox_ReturnsFalse()
        {
            Assert.IsFalse(BoundingBoxUtils.TryNormalize(new float[] { 100, 100, 100, 100 }, 400, 400, out _));
        }

        [Test]
        public void Normalize_NullOrShort_ReturnsFalse()
        {
            Assert.IsFalse(BoundingBoxUtils.TryNormalize(null, 400, 400, out _));
            Assert.IsFalse(BoundingBoxUtils.TryNormalize(new float[] { 1, 2 }, 400, 400, out _));
        }
    }
}
