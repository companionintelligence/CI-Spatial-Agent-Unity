using System.Collections.Generic;
using NUnit.Framework;
using XRScan.Core;

namespace XRScan.Tests.Config
{
    public class PermissionTrackerTests
    {
        private const string Mic = AppPermissions.Microphone;

        private FakePermissionService _service;
        private double _now;
        private PermissionTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _service = new FakePermissionService();
            _now = 0;
            _tracker = new PermissionTracker(_service, () => _now);
        }

        [Test]
        public void WithoutARuntimePermissionModel_EverythingIsGrantedWithoutAPrompt()
        {
            _service.UsesRuntimePermissions = false;
            PermissionState? result = null;
            _tracker.Ensure(Mic, s => result = s);
            Assert.AreEqual(PermissionState.Granted, result);
            Assert.IsEmpty(_service.Requests);
        }

        [Test]
        public void FirstUse_PromptsOnce_AndConcurrentCallersShareThePrompt()
        {
            var results = new List<PermissionState>();
            _tracker.Ensure(Mic, results.Add);
            _tracker.Ensure(Mic, results.Add);
            Assert.AreEqual(1, _service.Requests.Count);
            Assert.AreEqual(PermissionState.Requesting, _tracker.GetState(Mic));
            Assert.IsEmpty(results);

            _service.Answer(Mic, PermissionState.Granted);
            CollectionAssert.AreEqual(new[] { PermissionState.Granted, PermissionState.Granted }, results);
            Assert.IsTrue(_tracker.IsGranted(Mic));
        }

        [Test]
        public void PlainDenial_IsReported_AndAskedAgainOnNextUse()
        {
            PermissionState? result = null;
            _tracker.Ensure(Mic, s => result = s);
            _service.Answer(Mic, PermissionState.Denied);
            Assert.AreEqual(PermissionState.Denied, result);
            Assert.AreEqual(PermissionState.Denied, _tracker.GetState(Mic));

            _tracker.Ensure(Mic, null);
            Assert.AreEqual(2, _service.Requests.Count);
        }

        [Test]
        public void PermanentDenial_IsNeverPromptedAgain()
        {
            _tracker.Ensure(Mic, null);
            _service.Answer(Mic, PermissionState.DeniedPermanently);

            PermissionState? result = null;
            _tracker.Ensure(Mic, s => result = s);
            Assert.AreEqual(PermissionState.DeniedPermanently, result);
            Assert.AreEqual(1, _service.Requests.Count);
            StringAssert.Contains("Settings", PermissionTracker.Describe(Mic, PermissionState.DeniedPermanently));
        }

        [Test]
        public void PermissionGrantedLaterInSystemSettings_ResolvesWithoutAPrompt()
        {
            _tracker.Ensure(Mic, null);
            _service.Answer(Mic, PermissionState.DeniedPermanently);
            _service.Granted.Add(Mic);

            PermissionState? result = null;
            _tracker.Ensure(Mic, s => result = s);
            Assert.AreEqual(PermissionState.Granted, result);
            Assert.AreEqual(1, _service.Requests.Count);
        }

        [Test]
        public void DismissedPrompt_CountsAsAPlainDenial()
        {
            PermissionState? result = null;
            _tracker.Ensure(Mic, s => result = s);
            _service.Answer(Mic, PermissionState.NotRequested); // e.g. the dialog was dismissed
            Assert.AreEqual(PermissionState.Denied, result);
        }

        [Test]
        public void UnansweredPrompt_IsShownAgainOnlyAfterTheTimeout()
        {
            _tracker.Ensure(Mic, null);
            _now = PermissionTracker.RequestTimeoutSeconds / 2;
            _tracker.Ensure(Mic, null);
            Assert.AreEqual(1, _service.Requests.Count);

            _now = PermissionTracker.RequestTimeoutSeconds + 1;
            _tracker.Ensure(Mic, null);
            Assert.AreEqual(2, _service.Requests.Count);
        }
    }
}
