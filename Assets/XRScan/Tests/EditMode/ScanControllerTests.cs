using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XRScan.App;
using XRScan.Core;
using XRScan.Mapping;
using XRScan.Mocks;
using XRScan.Vision;

namespace XRScan.Tests.EditMode
{
    /// <summary>
    /// One scan pass against the vision server's contract: the frame carries its real size, the prompt names
    /// real objects, boxes are fractions with the origin at the image's top-left, and without a vision server
    /// the app says so instead of inventing detections. The camera looks down +z from the origin, so a pin's
    /// x and y show where its ray went.
    /// </summary>
    public class ScanControllerTests
    {
        private sealed class FakeCamera : ICameraProvider
        {
            public Vector2Int Size = new Vector2Int(1280, 960);
            public Matrix4x4 Intrinsics = Matrix4x4.zero;

            public bool IsAvailable => true;
            public bool SupportsRgbFrames => true;
            public Vector2Int FrameSize => Size;
            public void StartCapture() { }
            public void StopCapture() { }
            public byte[] GetCurrentFrameJpeg(int quality = 75) => new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
            public Matrix4x4 GetCameraIntrinsics() => Intrinsics;
            public Pose GetCameraPose() => new Pose(Vector3.zero, Quaternion.identity);
        }

        private sealed class FakeDetector : IObjectDetector
        {
            public readonly DetectionResult Result = new DetectionResult();
            public CaptureFrame Frame;
            public string Prompt;

            public bool IsReady => true;

            public FakeDetector Box(string label, params float[] box)
            {
                Result.bboxes.Add(box);
                Result.labels.Add(label);
                Result.scores.Add(0.9f);
                return this;
            }

            public Task<DetectionResult> DetectAsync(CaptureFrame frame, string prompt)
            {
                Frame = frame;
                Prompt = prompt;
                return Task.FromResult(Result);
            }

            public Task<SegmentationResult> SegmentAsync(CaptureFrame frame, Rect roi) => Task.FromResult(new SegmentationResult());
            public Task<FlorenceResult> CaptionAsync(CaptureFrame frame) => Task.FromResult(new FlorenceResult());
        }

        private GameObject _root;
        private SpatialPinManager _pins;
        private ScanController _scan;
        private readonly List<string> _notices = new List<string>();

        [SetUp]
        public void SetUp()
        {
            ConfigOverrides.Store = new MemorySettingsStore();
            ConfigOverrides.EnvironmentReader = _ => null;
            CleartextPolicy.ClearDevAllowlist();
            UserNotices.Posted += _notices.Add;

            _root = new GameObject("scan-rig");
            var cam = new GameObject("cam").AddComponent<Camera>();
            cam.transform.SetParent(_root.transform);
            cam.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            cam.fieldOfView = 60f;
            cam.aspect = 4f / 3f;
            cam.tag = "MainCamera";

            var raycaster = new GameObject("raycaster").AddComponent<ViewportRaycaster>();
            raycaster.transform.SetParent(_root.transform);
            SetPrivate(raycaster, "targetCamera", cam);

            _pins = new GameObject("pins").AddComponent<SpatialPinManager>();
            _pins.transform.SetParent(_root.transform);

            _scan = new GameObject("scan").AddComponent<ScanController>();
            _scan.transform.SetParent(_root.transform);
            SetPrivate(_scan, "raycaster", raycaster);
            SetPrivate(_scan, "pinManager", _pins);
        }

        [TearDown]
        public void TearDown()
        {
            UserNotices.Posted -= _notices.Add;
            _notices.Clear();
            Object.DestroyImmediate(_root);
            ConfigOverrides.ResetToDefaults();
        }

        private static void SetPrivate(Object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, "expected private field '" + field + "' on " + target.GetType().Name);
            f.SetValue(target, value);
        }

        private int ScanOnce() => _scan.ScanOnceAsync().GetAwaiter().GetResult();

        [Test]
        public void Scan_SendsTheFramesRealSizeAndAPromptThatNamesObjects()
        {
            var detector = new FakeDetector().Box("cup", 0.4f, 0.4f, 0.6f, 0.6f);
            _scan.Configure(new FakeCamera(), detector);

            Assert.AreEqual(1, ScanOnce());

            Assert.AreEqual(1280, detector.Frame.imageWidth);
            Assert.AreEqual(960, detector.Frame.imageHeight);
            Assert.AreEqual(ScanController.DefaultDetectionPrompt, detector.Prompt);
            Assert.AreNotEqual("object", detector.Prompt.Trim());
            Assert.GreaterOrEqual(detector.Prompt.Split('.').Length, 10, "the prompt lists the objects to look for");
            StringAssert.EndsWith(" .", detector.Prompt, "GroundingDINO's \"a . b .\" form");
            Assert.AreEqual("cup", _pins.Pins[0].label);
        }

        [Test]
        public void PinLabels_AreOnlyWordsThePromptAskedFor()
        {
            // A label is shown on its pin and spoken by the companion, so model text outside the prompt never gets there.
            var detector = new FakeDetector()
                .Box("Remote Control", 0.1f, 0.1f, 0.2f, 0.2f)
                .Box("cup mug", 0.3f, 0.1f, 0.4f, 0.2f)
                .Box("hate them all", 0.5f, 0.1f, 0.6f, 0.2f)
                .Box("cup <b>now</b>", 0.7f, 0.1f, 0.8f, 0.2f)
                .Box("", 0.1f, 0.5f, 0.2f, 0.6f);
            detector.Result.labels.RemoveAt(4); // and one box without a label at all
            _scan.Configure(new FakeCamera(), detector);

            Assert.AreEqual(5, ScanOnce());
            CollectionAssert.AreEqual(new[] { "remote control", "cup mug", "object", "object", "object" },
                _pins.Pins.Select(p => p.label).ToArray());
            CollectionAssert.AreEqual(_pins.Pins.Select(p => p.label).ToArray(), _pins.Pins.Select(p => p.semanticLabel).ToArray());
        }

        [TestCase("lamp", "lamp . cup .", "lamp")]
        [TestCase("  Picture   Frame ", ScanController.DefaultDetectionPrompt, "picture frame")]
        [TestCase("frame picture", ScanController.DefaultDetectionPrompt, "frame picture")]
        [TestCase("knife", ScanController.DefaultDetectionPrompt, ScanController.UncheckedLabel)]
        [TestCase("cup", "", ScanController.UncheckedLabel)]
        [TestCase("...", "cup .", ScanController.UncheckedLabel)]
        public void CheckedLabel(string label, string prompt, string expected)
        {
            Assert.AreEqual(expected, ScanController.CheckedLabel(label, prompt));
        }

        [Test]
        public void PixelBoxes_AreScaledByTheFrameSize_InsteadOfDropped()
        {
            var camera = new FakeCamera { Size = new Vector2Int(400, 400) };
            _scan.Configure(camera, new FakeDetector().Box("book", 100f, 100f, 300f, 300f));

            Assert.AreEqual(1, ScanOnce(), "a frame without a size made every pixel box fail to normalize");
            Assert.AreEqual(0.25f, _pins.Pins[0].bboxNormalized.x, 1e-4f);
            Assert.AreEqual(0.5f, _pins.Pins[0].bboxNormalized.width, 1e-4f);
        }

        [Test]
        public void ContractBoxNearTheTopOfTheImage_PinsAboveTheViewCentre()
        {
            // The server's boxes have their origin at the top-left: y = 0.05..0.25 is the top of the image.
            _scan.Configure(new FakeCamera(), new FakeDetector().Box("lamp", 0.45f, 0.05f, 0.55f, 0.25f));

            Assert.AreEqual(1, ScanOnce());
            var pin = _pins.Pins[0];
            Assert.Greater(pin.position.y, 0.3f, "a box at the top of the image must not land below the view centre");
            Assert.AreEqual(0f, pin.position.x, 0.05f);
            Assert.AreEqual(0.05f, pin.bboxNormalized.y, 1e-4f, "the pin keeps the box as the server sent it");
        }

        [Test]
        public void ContractBox_WithCameraIntrinsics_UsesTheCapturePixel()
        {
            var intrinsics = Matrix4x4.zero;
            intrinsics.m00 = 500f; intrinsics.m11 = 500f; intrinsics.m02 = 640f; intrinsics.m12 = 480f; intrinsics.m22 = 1f;
            var camera = new FakeCamera { Intrinsics = intrinsics };
            // Top-right of a 1280 x 960 image.
            _scan.Configure(camera, new FakeDetector().Box("clock", 0.8f, 0.1f, 0.9f, 0.2f));

            Assert.AreEqual(1, ScanOnce());
            var pin = _pins.Pins[0];
            Assert.Greater(pin.position.x, 0.3f, "right of the image is +x");
            Assert.Greater(pin.position.y, 0.3f, "top of the image is +y");
        }

        [Test]
        public void ServerContractExample_MapsToThePixelsTheReadmeGives()
        {
            // cv-server README "Boxes": [0.15, 0.30, 0.35, 0.70] on a 1280 x 1024 image spans x 192..448, y 307.2..716.8.
            Assert.IsTrue(BoundingBoxUtils.TryNormalize(new[] { 0.15f, 0.30f, 0.35f, 0.70f }, 1280, 1024, out var box));
            var pixels = BoundingBoxUtils.NormalizedToPixels(box, 1280, 1024);
            Assert.AreEqual(192f, pixels.xMin, 1e-3f);
            Assert.AreEqual(448f, pixels.xMax, 1e-3f);
            Assert.AreEqual(307.2f, pixels.yMin, 1e-2f);
            Assert.AreEqual(716.8f, pixels.yMax, 1e-2f);

            var centre = BoundingBoxUtils.CenterInPixels(box, 1280, 1024);
            Assert.AreEqual(320f, centre.x, 1e-3f);
            Assert.AreEqual(512f, centre.y, 1e-3f);
            var viewport = BoundingBoxUtils.CenterInViewport(new Rect(0.1f, 0.1f, 0.2f, 0.2f));
            Assert.AreEqual(0.2f, viewport.x, 1e-5f);
            Assert.AreEqual(0.8f, viewport.y, 1e-5f, "image y points down, viewport y points up");
        }

        [Test]
        public void WithoutAVisionServer_ScanSaysSo_AndPlacesNothing()
        {
            var server = ScriptableObject.CreateInstance<ServerConfig>();
            try
            {
                var detector = DetectorFactory.Select(server, new SentisYoloDetector(), null);
                Assert.IsInstanceOf<RemoteCvDetector>(detector, "no stand-in detector when the server is missing");
                Assert.IsFalse(detector.IsReady);

                _scan.Configure(new FakeCamera(), detector);
                Assert.AreEqual(0, ScanOnce());

                Assert.AreEqual(0, _pins.Pins.Count);
                CollectionAssert.AreEqual(new[] { RemoteCvDetector.NotConfiguredMessage }, _notices);
            }
            finally
            {
                Object.DestroyImmediate(server);
            }
        }

        [Test]
        public void ConfiguredVisionServer_IsReady()
        {
            var server = ScriptableObject.CreateInstance<ServerConfig>();
            try
            {
                server.baseUrl = "https://vision.example.lan";
                var detector = (RemoteCvDetector)DetectorFactory.Select(server, null, null);
                Assert.IsTrue(detector.IsReady);
                Assert.AreEqual(string.Empty, detector.UnavailableReason);
            }
            finally
            {
                Object.DestroyImmediate(server);
            }
        }

        [Test]
        public void DemoDetections_AreBehindADefaultOffDeveloperSwitch()
        {
            var go = new GameObject("bootstrap");
            try
            {
                var bootstrap = go.AddComponent<AppBootstrap>();
                using (var serialized = new SerializedObject(bootstrap))
                {
                    Assert.IsFalse(serialized.FindProperty("useMocks").boolValue, "a new AppBootstrap must not run the demo");
                }

                // Wired like XRScan_Main with no vision server set: the scan controller gets the server detector.
                var server = ScriptableObject.CreateInstance<ServerConfig>();
                try
                {
                    SetPrivate(bootstrap, "serverConfig", server);
                    SetPrivate(bootstrap, "scanController", _scan);
                    typeof(AppBootstrap).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(bootstrap, null);

                    var detector = typeof(ScanController).GetField("_detector", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_scan);
                    Assert.IsInstanceOf<RemoteCvDetector>(detector);
                    Assert.IsNotInstanceOf<MockObjectDetector>(detector);
                }
                finally
                {
                    Object.DestroyImmediate(server);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
