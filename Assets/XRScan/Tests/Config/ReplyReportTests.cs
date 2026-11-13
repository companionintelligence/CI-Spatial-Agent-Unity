using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using XRScan.Core;

namespace XRScan.Tests.Config
{
    /// <summary>
    /// Reporting an AI reply from inside the app (Google Play's AI-generated content policy; Meta VRC.Content.3 and 4):
    /// the Report row hides the reply at once, shows what a report sends and where, and sends it on a second press,
    /// by default as an email to the company's published support address, so no new server is needed.
    /// </summary>
    public class ReplyReportTests
    {
        private static readonly string[] NoHosts = new string[0];

        private OverrideSandbox _sandbox;
        private CompanionConfig _companion;
        private CompanionSettingsModel _model;
        private ReplyReport _sent;
        private AiReply _hidden;

        [SetUp]
        public void SetUp()
        {
            _sandbox = new OverrideSandbox();
            AiReplies.Clear();
            AiReplies.HideRequested += OnHide;
            _companion = ScriptableObject.CreateInstance<CompanionConfig>();
            _model = new CompanionSettingsModel(_companion, false, NoHosts)
            {
                AppVersion = "0.1.0",
                SendReport = report =>
                {
                    _sent = report;
                    return "sent";
                }
            };
        }

        [TearDown]
        public void TearDown()
        {
            AiReplies.HideRequested -= OnHide;
            AiReplies.Clear();
            UnityEngine.Object.DestroyImmediate(_companion);
            _sandbox.Dispose();
        }

        private void OnHide(AiReply reply) => _hidden = reply;

        private SettingRow ReportRow() => _model.BuildRows().Single(r => r.Id == SettingId.ReportReply);

        private static AiReply Reply() => new AiReply("What is on the table?", "A red mug and a book.", "llava:7b");

        [Test]
        public void TheDefaultDestination_IsThePublishedSupportAddress()
        {
            Assert.AreEqual("mailto:support@companionintelligence.com", _companion.ResolveReplyReportDestination());
            StringAssert.Contains("replyReportDestination: mailto:support@companionintelligence.com",
                File.ReadAllText("Assets/XRScan/Config/CompanionConfig.asset"));

            _sandbox.Environment[ReplyReporting.EnvDestinationKey] = "https://reports.example.lan/v1/replies";
            Assert.AreEqual("https://reports.example.lan/v1/replies", _companion.ResolveReplyReportDestination());
        }

        [Test]
        public void TheReportRow_IsFirst_AndSaysWhenThereIsNothingToReport()
        {
            Assert.AreEqual(SettingId.ReportReply, _model.BuildRows()[0].Id);
            Assert.AreEqual("no reply yet", ReportRow().Value);
            StringAssert.Contains("no reply", _model.Activate(SettingId.ReportReply));
            Assert.IsNull(_sent);
        }

        [Test]
        public void FirstPress_HidesTheReplyAtOnce_AndShowsWhatIsSentAndWhere()
        {
            var reply = Reply();
            AiReplies.Record(reply);
            StringAssert.Contains("A red mug", ReportRow().Value);

            _model.Activate(SettingId.ReportReply);
            Assert.AreSame(reply, _hidden, "whoever speaks the reply is told to stop");
            Assert.IsTrue(reply.Hidden);
            Assert.IsTrue(_model.ReportPending);
            Assert.IsNull(_sent, "nothing is sent before the user confirms");

            var row = ReportRow();
            Assert.AreEqual(SettingKind.Choice, row.Kind, "left / right picks the reason");
            StringAssert.Contains(ReplyReporting.Contents, row.Detail);
            StringAssert.Contains("support@companionintelligence.com", row.Detail);
            StringAssert.Contains("llava:7b", row.Detail);
            StringAssert.Contains("0.1.0", row.Detail);
            StringAssert.DoesNotContain("A red mug", row.Value + row.Detail, "a hidden reply is not shown again");
        }

        [Test]
        public void SecondPress_SendsAPrefilledEmail_WithTheReason()
        {
            AiReplies.Record(Reply());
            _model.Activate(SettingId.ReportReply);
            Assert.AreEqual("Reason: Sexual content", _model.Cycle(SettingId.ReportReply, 1));

            Assert.AreEqual("sent", _model.Activate(SettingId.ReportReply));
            Assert.IsFalse(_model.ReportPending);
            Assert.AreEqual(ReplyReportChannel.Email, _sent.Channel);
            Assert.AreEqual("support@companionintelligence.com", _sent.Recipient);
            StringAssert.StartsWith("mailto:support@companionintelligence.com?subject=", _sent.MailtoUri);

            string body = Uri.UnescapeDataString(_sent.MailtoUri.Substring(_sent.MailtoUri.IndexOf("&body=", StringComparison.Ordinal) + 6));
            StringAssert.Contains("Reason: Sexual content", body);
            StringAssert.Contains("Model: llava:7b", body);
            StringAssert.Contains("App version: 0.1.0", body);
            StringAssert.Contains("What is on the table?", body);
            StringAssert.Contains("A red mug and a book.", body);
            StringAssert.DoesNotContain(" ", _sent.MailtoUri, "everything after mailto: is percent-encoded");
        }

        [Test]
        public void MovingAway_CancelsTheReport_ButTheReplyStaysHidden()
        {
            var reply = Reply();
            AiReplies.Record(reply);
            _model.Activate(SettingId.ReportReply);
            _model.CancelPending();

            Assert.IsFalse(_model.ReportPending);
            Assert.IsTrue(reply.Hidden);
            Assert.AreEqual("hidden", ReportRow().Value);
            Assert.IsNull(_sent);
        }

        [Test]
        public void AnHttpsDestination_GetsAJsonPost_AndPlainHttpIsRefusedInRelease()
        {
            var reply = Reply();
            var https = ReplyReporting.Build(reply, "Dangerous or harmful", "0.1.0", "https://reports.example.lan/v1/replies", false, NoHosts);
            Assert.AreEqual(ReplyReportChannel.Https, https.Channel);
            Assert.AreEqual("https://reports.example.lan/v1/replies", https.CheckedUrl);
            StringAssert.Contains("\"reason\":\"Dangerous or harmful\"", https.Json);
            StringAssert.Contains("\"model\":\"llava:7b\"", https.Json);
            StringAssert.Contains("\"app_version\":\"0.1.0\"", https.Json);
            StringAssert.Contains("\"question\":\"What is on the table?\"", https.Json);
            StringAssert.Contains("\"reply\":\"A red mug and a book.\"", https.Json);

            var http = ReplyReporting.Build(reply, "Something else", "0.1.0", "http://reports.example.lan", false, NoHosts);
            Assert.AreEqual(ReplyReportChannel.Unavailable, http.Channel);
            Assert.IsEmpty(http.CheckedUrl);
        }

        [Test]
        public void LongReplies_AreCut_AndBadAddressesRefused()
        {
            var longReply = new AiReply("q", new string('x', 5000), "m");
            var report = ReplyReporting.Build(longReply, "Something else", "0.1.0", ReplyReporting.DefaultDestination, false, NoHosts);
            Assert.Less(Uri.UnescapeDataString(report.MailtoUri).Length, 2 * ReplyReporting.MaxTextLength);

            Assert.AreEqual(ReplyReportChannel.Unavailable,
                ReplyReporting.Build(longReply, "x", "0.1.0", "mailto:not an address", false, NoHosts).Channel);
        }
    }
}
