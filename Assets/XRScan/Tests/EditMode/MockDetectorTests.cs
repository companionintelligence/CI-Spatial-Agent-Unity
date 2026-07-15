using NUnit.Framework;
using XRScan.Core;
using XRScan.Mocks;

namespace XRScan.Tests.EditMode
{
    public class MockDetectorTests
    {
        [Test]
        public void DetectAsync_ReturnsSingleCenteredDetection()
        {
            var detector = new MockObjectDetector(new[] { "cup" });
            var frame = new CaptureFrame { imageWidth = 640, imageHeight = 480 };

            var result = detector.DetectAsync(frame, "object").GetAwaiter().GetResult();

            Assert.AreEqual(1, result.bboxes.Count);
            Assert.AreEqual(1, result.labels.Count);
            Assert.AreEqual("cup", result.labels[0]);
            Assert.Greater(result.scores[0], 0.5f);
        }

        [Test]
        public void DetectAsync_CyclesLabels()
        {
            var detector = new MockObjectDetector(new[] { "cup", "chair" });
            var frame = new CaptureFrame();

            var a = detector.DetectAsync(frame, "").GetAwaiter().GetResult().labels[0];
            var b = detector.DetectAsync(frame, "").GetAwaiter().GetResult().labels[0];

            Assert.AreEqual("cup", a);
            Assert.AreEqual("chair", b);
        }

        [Test]
        public void IsReady_IsTrue()
        {
            Assert.IsTrue(new MockObjectDetector().IsReady);
        }
    }
}
