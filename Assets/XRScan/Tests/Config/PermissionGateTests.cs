using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XRScan.Core;
using XRScan.Settings;

namespace XRScan.Tests.Config
{
    public class PermissionGateTests
    {
        private OverrideSandbox _sandbox;
        private GameObject _go;
        private FakeSpeechToText _inner;
        private RuntimePermissions _permissions;
        private FakePermissionService _service;
        private PermissionGatedSpeechToText _gate;
        private VoiceConfig _voice;
        private readonly List<string> _errors = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _sandbox = new OverrideSandbox();
            _go = new GameObject("stt-gate-test");
            _inner = _go.AddComponent<FakeSpeechToText>();
            _permissions = _go.AddComponent<RuntimePermissions>();
            _service = new FakePermissionService();
            _permissions.UseService(_service);
            _voice = ScriptableObject.CreateInstance<VoiceConfig>();
            _voice.selfHostedBaseUrl = "https://hub.example.lan";
            _gate = _go.AddComponent<PermissionGatedSpeechToText>();
            _gate.Configure(_inner, _permissions, _voice);
            _errors.Clear();
            _gate.OnError.AddListener(_errors.Add);
            _gate.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_voice);
            _sandbox.Dispose();
        }

        [Test]
        public void Initialize_DoesNotTouchTheRecognizerBeforeTheMicrophoneIsGranted()
        {
            Assert.AreEqual(0, _inner.InitializeCalls);
            Assert.IsEmpty(_service.Requests);
        }

        [Test]
        public void FirstPushToTalk_AsksForTheMicrophone_ThenStartsRecording()
        {
            _gate.StartListening();
            CollectionAssert.AreEqual(new[] { AppPermissions.Microphone }, _service.Requests);
            Assert.AreEqual(0, _inner.StartCalls);
            Assert.IsTrue(_gate.IsProcessing);

            _service.Answer(AppPermissions.Microphone, PermissionState.Granted);
            Assert.AreEqual(1, _inner.InitializeCalls);
            Assert.AreEqual(1, _inner.StartCalls);
            Assert.IsTrue(_gate.IsListening);
        }

        [Test]
        public void DeniedMicrophone_NeverStartsRecording_AndReportsWhy()
        {
            _gate.StartListening();
            _service.Answer(AppPermissions.Microphone, PermissionState.Denied);

            Assert.AreEqual(0, _inner.StartCalls);
            Assert.AreEqual(1, _errors.Count);
            StringAssert.Contains("Microphone", _errors[0]);

            _gate.StartListening(); // a plain denial is asked again on the next press
            Assert.AreEqual(2, _service.Requests.Count);
        }

        [Test]
        public void UnusableSpeechEndpoint_DoesNotRecordOrPrompt()
        {
            _voice.selfHostedBaseUrl = string.Empty;
            _gate.StartListening();

            Assert.IsEmpty(_service.Requests);
            Assert.AreEqual(0, _inner.StartCalls);
            Assert.AreEqual(1, _errors.Count);
            StringAssert.Contains("unavailable", _errors[0]);
        }

        [Test]
        public void CloudSpeechWithoutOptIn_DoesNotRecord()
        {
            _voice.provider = SpeechProvider.Cloud;
            _gate.StartListening();
            Assert.AreEqual(0, _inner.StartCalls);
            Assert.IsEmpty(_service.Requests);
        }
    }
}
