using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using XRScan.Agent;
using XRScan.Core;

namespace XRScan.Tests.EditMode
{
    /// <summary>
    /// The fail-closed check of every reply (Google Play gate 11, Meta gate 12): a reply is spoken only when the check
    /// says it is safe; a flagged reply, and one the check cannot judge (error, exception, timeout, no answer, no
    /// checker), is withheld and replaced by a fixed line. The check gets the user's question and the reply, nothing else.
    /// </summary>
    public class ReplyCheckTests
    {
        private GameObject _go;
        private ConversationOrchestrator _conversation;
        private FakeStt _stt;
        private FakeLlm _llm;
        private FakeTts _tts;
        private FakeChecker _checker;
        private double _now;
        private readonly List<string> _notices = new List<string>();

        [SetUp]
        public void SetUp()
        {
            AiReplies.Clear();
            ReplySafety.Clear();
            UserNotices.Posted += _notices.Add;
            _now = 100.0;
            _go = new GameObject("conversation");
            _stt = new FakeStt();
            _llm = new FakeLlm();
            _tts = new FakeTts();
            _checker = new FakeChecker();
            _conversation = _go.AddComponent<ConversationOrchestrator>();
            _conversation.Clock = () => _now;
            _conversation.Configure(_stt, _llm, _tts, null, _checker);
        }

        [TearDown]
        public void TearDown()
        {
            _conversation.Configure(null, null, null, null);
            Object.DestroyImmediate(_go);
            UserNotices.Posted -= _notices.Add;
            _notices.Clear();
            AiReplies.Clear();
            ReplySafety.Clear();
        }

        private void Ask(string question, string reply)
        {
            _stt.OnTranscriptionComplete.Invoke(question);
            _llm.OnResponseReceived.Invoke(reply);
        }

        private void AssertWithheld(string reply, string line)
        {
            CollectionAssert.AreEqual(new[] { line }, _tts.Lines, "only the fixed line is spoken");
            Assert.IsNull(AiReplies.Last, "a withheld reply is not kept, so the panel never shows it");
            foreach (var notice in _notices) StringAssert.DoesNotContain(reply, notice);
            Assert.IsFalse(_conversation.IsCheckingReply);
        }

        [Test]
        public void ASafeReply_IsSpokenAndRecorded()
        {
            Ask("what is on the table?", "A cup and a lamp.");

            CollectionAssert.AreEqual(new[] { "A cup and a lamp." }, _tts.Lines);
            Assert.AreEqual("A cup and a lamp.", AiReplies.Last.Reply);
            Assert.AreEqual(ReplyVerdict.Safe, ReplySafety.Last.Verdict);
            CollectionAssert.IsEmpty(_notices);
        }

        [Test]
        public void AnUnsafeReply_IsWithheld_AndTheBlockedLineIsSpoken()
        {
            _checker.Answer = ReplyCheckResult.Blocked("violence");
            Ask("my neighbour is loud", "Smash his windows.");

            AssertWithheld("Smash his windows.", ReplySafety.BlockedLine);
            Assert.AreEqual(ReplyVerdict.Blocked, ReplySafety.Last.Verdict);
            Assert.AreEqual("violence", ReplySafety.Last.Flag);
        }

        [Test]
        public void ACheckThatFails_WithholdsTheReply_AndPointsToSettings()
        {
            _checker.Answer = ReplyCheckResult.Unavailable("granite3-guardian:2b is not installed on the LLM server.");
            Ask("what is on the shelf?", "Two books.");

            AssertWithheld("Two books.", ReplySafety.UnavailableLine);
            StringAssert.Contains("Settings", ReplySafety.UnavailableLine);
            Assert.AreEqual(1, _notices.Count);
            StringAssert.Contains("granite3-guardian:2b is not installed", _notices[0]);
            StringAssert.Contains("Settings", _notices[0]);
        }

        [Test]
        public void ACheckerThatThrows_WithholdsTheReply()
        {
            _checker.Throw = true;
            Ask("what is on the shelf?", "Two books.");
            AssertWithheld("Two books.", ReplySafety.UnavailableLine);
        }

        [Test]
        public void ACheckerThatAnswersNothing_WithholdsTheReply()
        {
            _checker.Answer = null;
            Ask("what is on the shelf?", "Two books.");
            AssertWithheld("Two books.", ReplySafety.UnavailableLine);
        }

        [Test]
        public void ACheckThatTimesOut_WithholdsTheReply_AndALateAnswerIsIgnored()
        {
            _checker.Hold = true;
            _checker.TimeoutSeconds = 5f;
            Ask("what is on the shelf?", "Two books.");

            _now += 4.9;
            _conversation.CheckReplyDeadline();
            CollectionAssert.IsEmpty(_tts.Lines, "nothing is said while the check runs");
            Assert.IsTrue(_conversation.IsCheckingReply);

            _now += 0.2;
            _conversation.CheckReplyDeadline();
            AssertWithheld("Two books.", ReplySafety.UnavailableLine);
            StringAssert.Contains("5 seconds", ReplySafety.Last.Reason);

            _checker.Pending(ReplyCheckResult.Safe());
            CollectionAssert.AreEqual(new[] { ReplySafety.UnavailableLine }, _tts.Lines, "a late Safe releases nothing");
        }

        [Test]
        public void ACheckThatAnswersTwice_CountsOnlyTheFirstAnswer()
        {
            _checker.Hold = true;
            Ask("what is on the shelf?", "Two books.");
            _checker.Pending(ReplyCheckResult.Blocked("harm"));
            _checker.Pending(ReplyCheckResult.Safe());

            AssertWithheld("Two books.", ReplySafety.BlockedLine);
        }

        [Test]
        public void ANewerReply_ReplacesOneStillBeingChecked()
        {
            _checker.Hold = true;
            Ask("first question", "First reply.");
            var first = _checker.Pending;
            Ask("second question", "Second reply.");
            var second = _checker.Pending;

            first(ReplyCheckResult.Safe());
            CollectionAssert.IsEmpty(_tts.Lines, "the superseded reply is never released");
            second(ReplyCheckResult.Safe());
            CollectionAssert.AreEqual(new[] { "Second reply." }, _tts.Lines);
        }

        [Test]
        public void WithoutAChecker_EveryReplyIsWithheld()
        {
            _conversation.Configure(_stt, _llm, _tts, null); // FakeLlm brings no reply check of its own
            Ask("what is on the shelf?", "Two books.");

            AssertWithheld("Two books.", ReplySafety.UnavailableLine);
            Assert.AreEqual(ReplyGate.NoCheckerReason, ReplySafety.Last.Reason);
        }

        [Test]
        public void TheLlmBackendsOwnCheck_IsUsedWhenNoneIsGiven()
        {
            var llm = new CheckingLlm();
            llm.Checker.Answer = ReplyCheckResult.Blocked("sexual_content");
            _conversation.Configure(_stt, llm, _tts, null);
            _stt.OnTranscriptionComplete.Invoke("hello");
            llm.OnResponseReceived.Invoke("Something unsafe.");

            Assert.AreEqual(1, llm.Checker.Calls.Count);
            AssertWithheld("Something unsafe.", ReplySafety.BlockedLine);
        }

        [Test]
        public void TheCheck_GetsOnlyTheQuestionAndTheReply()
        {
            var knowledge = new GameObject("knowledge").AddComponent<SceneKnowledgeBase>();
            try
            {
                _conversation.Configure(_stt, _llm, _tts, knowledge, _checker);
                Ask("what is on the table?", "Nothing yet.");

                StringAssert.Contains(knowledge.BuildContext(), _llm.LastPrompt, "the model gets the scene context");
                Assert.AreEqual(1, _checker.Calls.Count);
                Assert.AreEqual("what is on the table?", _checker.Calls[0].Key, "the user's words, without the scene context");
                Assert.AreEqual("Nothing yet.", _checker.Calls[0].Value);
            }
            finally
            {
                Object.DestroyImmediate(knowledge.gameObject);
            }
        }

        [Test]
        public void AnyReplyOfTheBackend_IsChecked_EvenWithoutAQuestion()
        {
            // A vision request's reply arrives on the same event as a text request's.
            _checker.Answer = ReplyCheckResult.Blocked("harm");
            _llm.OnResponseReceived.Invoke("Unprompted text.");

            Assert.AreEqual(1, _checker.Calls.Count);
            AssertWithheld("Unprompted text.", ReplySafety.BlockedLine);
        }

        [Test]
        public void TheOnDeviceRules_BlockBeforeTheChecker()
        {
            Ask("are you a real person?", "Yes, I'm a real person.");

            CollectionAssert.IsEmpty(_checker.Calls);
            AssertWithheld("Yes, I'm a real person.", ReplySafety.BlockedLine);
            Assert.AreEqual(LocalReplyRules.HumanClaimRule, ReplySafety.Last.Flag);
        }

        [Test]
        public void TheLastSpokenReply_StaysReportable_AfterAWithheldOne()
        {
            Ask("what is on the table?", "A cup.");
            _checker.Answer = ReplyCheckResult.Blocked("harm");
            Ask("and now?", "Something unsafe.");

            Assert.AreEqual("A cup.", AiReplies.Last.Reply);
            CollectionAssert.AreEqual(new[] { "A cup.", ReplySafety.BlockedLine }, _tts.Lines);
        }

        private sealed class CheckingLlm : FakeLlm, IReplyChecker
        {
            public readonly FakeChecker Checker = new FakeChecker();
            public string CheckerName => Checker.CheckerName;
            public float TimeoutSeconds => Checker.TimeoutSeconds;
            public void Check(string question, string reply, System.Action<ReplyCheckResult> done) => Checker.Check(question, reply, done);
        }

        // ---- every path that speaks or shows model text ----

        private static string RuntimeRoot => Path.Combine(Application.dataPath, "XRScan");

        private static IEnumerable<KeyValuePair<string, string>> RuntimeSources()
        {
            foreach (var file in Directory.GetFiles(RuntimeRoot, "*.cs", SearchOption.AllDirectories))
            {
                string relative = file.Substring(RuntimeRoot.Length + 1).Replace('\\', '/');
                var segments = relative.Split('/');
                if (segments.Contains("Editor") || segments.Contains("Tests")) continue; // not in the player
                yield return new KeyValuePair<string, string>(relative, File.ReadAllText(file));
            }
        }

        private static SortedSet<string> FilesMatching(string pattern) =>
            new SortedSet<string>(RuntimeSources().Where(s => Regex.IsMatch(s.Value, pattern)).Select(s => s.Key));

        [Test]
        public void EverySpeechPath_GoesThroughTheCheck()
        {
            // Model replies reach the app only through the backend's OnResponseReceived, which only the conversation
            // loop listens to; it speaks a reply only from the Safe branch of its check, or a fixed line.
            CollectionAssert.AreEquivalent(new[] { "Agent/ConversationOrchestrator.cs" },
                FilesMatching(@"OnResponseReceived\s*\.\s*AddListener"));
            CollectionAssert.AreEquivalent(new[] { "Agent/AvatarPerceptionBridge.cs", "Agent/ConversationOrchestrator.cs" },
                FilesMatching(@"\.SpeakText\("), "a new speech path must go through the reply check");

            string orchestrator = File.ReadAllText(Path.Combine(RuntimeRoot, "Agent/ConversationOrchestrator.cs"));
            var spoken = new SortedSet<string>(Regex.Matches(orchestrator, @"\.SpeakText\((?<arg>[^;]*)\);")
                .Cast<Match>().Select(m => m.Groups["arg"].Value.Trim()));
            CollectionAssert.AreEquivalent(new[] { "ReplySafety.BlockedLine", "ReplySafety.UnavailableLine", "reply" }, spoken);
            StringAssert.Contains("_gate.Submit(", orchestrator);

            // The other path speaks and shows pin labels, which come only from ScanController, through its label check.
            CollectionAssert.AreEquivalent(new[] { "App/ScanController.cs" }, FilesMatching(@"\.AddPin\("));
            StringAssert.Contains("string label = CheckedLabel(", File.ReadAllText(Path.Combine(RuntimeRoot, "App/ScanController.cs")));
            StringAssert.Contains("pin.label", File.ReadAllText(Path.Combine(RuntimeRoot, "Agent/AvatarPerceptionBridge.cs")));

            // The report row shows the last recorded reply, and only a reply that passed is recorded.
            CollectionAssert.AreEquivalent(new[] { "Agent/ConversationOrchestrator.cs" }, FilesMatching(@"AiReplies\.Record\("));
        }
    }
}
