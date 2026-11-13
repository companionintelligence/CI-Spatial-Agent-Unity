using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using XRScan.Agent;
using XRScan.Core;

namespace XRScan.Tests.EditMode
{
    /// <summary>
    /// The conversation loop's part in reporting an AI reply: it records each reply with the question and the model
    /// that wrote it, and stops speaking a reply the user hides.
    /// </summary>
    public class ConversationReportTests
    {
        private sealed class FakeStt : ISpeechToText
        {
            public bool IsInitialized => true;
            public bool IsListening => false;
            public bool IsProcessing => false;
            public UnityEvent OnListeningStarted { get; } = new UnityEvent();
            public UnityEvent OnListeningStopped { get; } = new UnityEvent();
            public UnityEvent<string> OnTranscriptionComplete { get; } = new StringUnityEvent();
            public UnityEvent<string> OnError { get; } = new StringUnityEvent();
            public void Initialize() { }
            public void StartListening() { }
            public void StopListening() { }
            public void ToggleListening() { }
        }

        private sealed class FakeLlm : ILlmBackend
        {
            public string LastPrompt;
            public bool IsInitialized => true;
            public bool IsConnected => true;
            public bool IsProcessing => false;
            public string ServerAddress => "test";
            public string ModelName => "test-model:1b";
            public UnityEvent OnProcessingStarted { get; } = new UnityEvent();
            public UnityEvent<string> OnResponseReceived { get; } = new StringUnityEvent();
            public UnityEvent<string> OnError { get; } = new StringUnityEvent();
            public void Initialize() { }
            public void SendTextRequest(string prompt) => LastPrompt = prompt;
            public void SendVisionRequest(Texture2D image, string prompt) => LastPrompt = prompt;
        }

        private sealed class FakeTts : ITextToSpeech
        {
            public string Spoken;
            public int Stops;
            public bool IsInitialized => true;
            public bool IsPlaying => false;
            public AudioSource OutputSource => null;
            public UnityEvent OnSpeechStarted { get; } = new UnityEvent();
            public UnityEvent OnSpeechCompleted { get; } = new UnityEvent();
            public UnityEvent<string> OnError { get; } = new StringUnityEvent();
            public void Initialize() { }
            public void SpeakText(string text) => Spoken = text;
            public void StopSpeaking() => Stops++;
            public void SetVoiceSettings(string voice, float speed, float pitch) { }
        }

        private GameObject _go;
        private FakeStt _stt;
        private FakeLlm _llm;
        private FakeTts _tts;

        [SetUp]
        public void SetUp()
        {
            AiReplies.Clear();
            _go = new GameObject("conversation");
            _stt = new FakeStt();
            _llm = new FakeLlm();
            _tts = new FakeTts();
            _go.AddComponent<ConversationOrchestrator>().Configure(_stt, _llm, _tts, null);
        }

        [TearDown]
        public void TearDown()
        {
            _go.GetComponent<ConversationOrchestrator>().Configure(null, null, null, null); // drops the hide subscription
            Object.DestroyImmediate(_go);
            AiReplies.Clear();
        }

        [Test]
        public void EachReply_IsRecordedWithItsQuestionAndModel()
        {
            _stt.OnTranscriptionComplete.Invoke("what is on the shelf?");
            StringAssert.Contains("what is on the shelf?", _llm.LastPrompt);
            _llm.OnResponseReceived.Invoke("Two books and a plant.");

            var reply = AiReplies.Last;
            Assert.IsNotNull(reply);
            Assert.AreEqual("what is on the shelf?", reply.Question, "the user's question, not the scene context");
            Assert.AreEqual("Two books and a plant.", reply.Reply);
            Assert.AreEqual("test-model:1b", reply.Model);
            Assert.AreEqual("Two books and a plant.", _tts.Spoken);
        }

        [Test]
        public void HidingAReply_StopsSpeakingIt()
        {
            _stt.OnTranscriptionComplete.Invoke("tell me a joke");
            _llm.OnResponseReceived.Invoke("An offensive joke.");

            AiReplies.Hide(AiReplies.Last);
            Assert.AreEqual(1, _tts.Stops);
            Assert.IsTrue(AiReplies.Last.Hidden);

            AiReplies.Hide(AiReplies.Last);
            Assert.AreEqual(1, _tts.Stops, "hiding twice does nothing more");
        }
    }
}
