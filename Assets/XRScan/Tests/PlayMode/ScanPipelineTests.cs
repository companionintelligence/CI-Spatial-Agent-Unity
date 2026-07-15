using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using XRScan.App;
using XRScan.Core;
using XRScan.Mapping;
using XRScan.Mocks;

namespace XRScan.Tests.PlayMode
{
    /// <summary>
    /// Automated scanning regression: exercises the full pipeline end-to-end against deterministic
    /// physics ground truth — a "wall" collider at a known depth stands in for a scanned surface,
    /// so a detection at a known screen position must land a pin at a known world position.
    ///
    /// Extend the system by adding cases to <see cref="Scenarios"/> (a detection UV + expected
    /// depth) or by adding a new [UnityTest]; the rig helper below builds a fresh scene each time.
    /// </summary>
    public class ScanPipelineTests
    {
        private GameObject _root;
        private Camera _cam;
        private ViewportRaycaster _raycaster;
        private SpatialPinManager _pins;

        private const float WallZ = 3f;      // wall centre
        private const float WallFrontZ = 2.9f; // front face (half-depth 0.1)

        [SetUp]
        public void Setup()
        {
            _root = new GameObject("scan-rig");

            _cam = new GameObject("cam").AddComponent<Camera>();
            _cam.transform.SetParent(_root.transform);
            _cam.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _cam.tag = "MainCamera";

            // ground-truth surface the detection should land on
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(_root.transform);
            wall.transform.position = new Vector3(0f, 0f, WallZ);
            wall.transform.localScale = new Vector3(8f, 8f, 0.2f);

            _raycaster = new GameObject("raycaster").AddComponent<ViewportRaycaster>();
            _raycaster.transform.SetParent(_root.transform);
            // targetCamera is [SerializeField] private and resolves Camera.main when null -> our tagged cam.

            _pins = new GameObject("pins").AddComponent<SpatialPinManager>();
            _pins.transform.SetParent(_root.transform);
        }

        [TearDown]
        public void Teardown() => Object.DestroyImmediate(_root);

        private static void SetPrivate(Component c, string field, Object value)
        {
            var f = c.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"expected private field '{field}' on {c.GetType().Name}");
            f.SetValue(c, value);
        }

        [UnityTest]
        public IEnumerator ViewportCentre_RaycastsToWall_AtExpectedDepth()
        {
            yield return null; // let the collider register with the physics scene

            bool hit = _raycaster.TryRaycastViewport(new Vector2(0.5f, 0.5f), out var point, out _, out var hitType);

            Assert.IsTrue(hit, "the centre ray should hit the wall");
            Assert.AreEqual(RaycastHitType.Physics, hitType);
            Assert.AreEqual(WallFrontZ, point.z, 0.15f);
        }

        [UnityTest]
        public IEnumerator FullPipeline_PlacesLabelledPinOnScannedObject()
        {
            var cam = new GameObject("mockcam").AddComponent<MockCameraProvider>();
            cam.transform.SetParent(_root.transform);
            var detector = new MockObjectDetector(new[] { "cup" });

            var scan = new GameObject("scan").AddComponent<ScanController>();
            scan.transform.SetParent(_root.transform);
            SetPrivate(scan, "raycaster", _raycaster);
            SetPrivate(scan, "pinManager", _pins);
            scan.Configure(cam, detector);

            yield return null;
            scan.Scan();
            yield return null;
            yield return null; // async scan + placement

            Assert.AreEqual(1, _pins.Pins.Count, "one detection should produce one pin");
            Assert.AreEqual("cup", _pins.Pins[0].label);
            Assert.AreEqual(WallFrontZ, _pins.Pins[0].position.z, 0.2f, "pin should sit on the scanned wall");
        }

        [UnityTest]
        public IEnumerator RepeatedScanOfSameObject_DedupsToOnePin()
        {
            var cam = new GameObject("mockcam").AddComponent<MockCameraProvider>();
            cam.transform.SetParent(_root.transform);
            // single-label detector -> every scan reports "mug" at the same place
            var detector = new MockObjectDetector(new[] { "mug" });

            var scan = new GameObject("scan").AddComponent<ScanController>();
            scan.transform.SetParent(_root.transform);
            SetPrivate(scan, "raycaster", _raycaster);
            SetPrivate(scan, "pinManager", _pins);
            scan.Configure(cam, detector);

            yield return null;
            scan.Scan();
            yield return null; yield return null;
            scan.Scan();
            yield return null; yield return null;

            Assert.AreEqual(1, _pins.Pins.Count, "scanning the same object twice should dedup to one pin");
        }
    }
}
