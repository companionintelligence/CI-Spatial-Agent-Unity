using NUnit.Framework;
using UnityEngine;
using XRScan.Core;
using XRScan.Mapping;

namespace XRScan.Tests.EditMode
{
    public class SpatialPinManagerTests
    {
        private GameObject _go;
        private SpatialPinManager _spm;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("spm");
            _spm = _go.AddComponent<SpatialPinManager>();
        }

        [TearDown]
        public void Teardown() => Object.DestroyImmediate(_go);

        private static SpatialPin Pin(string label, Vector3 pos) => new SpatialPin
        {
            label = label,
            semanticLabel = label,
            position = pos,
            source = PinSource.CvServer,
            score = 0.9f
        };

        [Test]
        public void AddPin_AddsToStoreAndFiresOnPinAdded()
        {
            SpatialPin fired = null;
            _spm.OnPinAdded += p => fired = p;

            _spm.AddPin(Pin("cup", new Vector3(1, 0, 1)));

            Assert.AreEqual(1, _spm.Pins.Count);
            Assert.IsNotNull(fired);
            Assert.AreEqual("cup", fired.label);
        }

        [Test]
        public void AddPin_MergesSameLabelNearby()
        {
            _spm.AddPin(Pin("cup", new Vector3(1f, 0, 1f)));
            _spm.AddPin(Pin("cup", new Vector3(1.05f, 0, 1f))); // within default dedupDistance (0.15)

            Assert.AreEqual(1, _spm.Pins.Count, "nearby same-label pins should dedup-merge");
        }

        [Test]
        public void AddPin_KeepsDistinctLabels()
        {
            _spm.AddPin(Pin("cup", new Vector3(1, 0, 1)));
            _spm.AddPin(Pin("chair", new Vector3(1, 0, 1)));

            Assert.AreEqual(2, _spm.Pins.Count);
        }
    }
}
