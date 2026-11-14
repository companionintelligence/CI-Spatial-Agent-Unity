using System.Linq;
using NUnit.Framework;
using XRScan.Core;
using XRScan.Llm;

namespace XRScan.Tests.EditMode
{
    /// <summary>
    /// The default reply check: IBM Granite Guardian on the language model's own Ollama server. Each request names one
    /// risk and carries only the user's question and the reply; Yes blocks, No passes, and any other answer, an error or
    /// a missing model withholds the reply.
    /// </summary>
    public class GraniteGuardianTests
    {
        private const string Model = LlmConfig.DefaultSafetyModel;

        private static ReplyCheckResult Read(string content) =>
            GraniteGuardian.ReadAnswer(Model, "harm", 200,
                "{\"model\":\"" + Model + "\",\"message\":{\"role\":\"assistant\",\"content\":" + Quote(content) + "},\"done\":true}", null);

        private static string Quote(string text) =>
            "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";

        [Test]
        public void TheDefault_IsTheSmallestGraniteGuardian()
        {
            Assert.AreEqual("granite3-guardian:2b", LlmConfig.DefaultSafetyModel);
            CollectionAssert.AreEqual(
                new[] { "harm", "sexual_content", "violence", "social_bias", "unethical_behavior", "profanity" },
                GraniteGuardian.Risks, "every harm category of the model card that judges a reply");
        }

        [Test]
        public void TheRequest_CarriesOnlyTheRiskTheQuestionAndTheReply()
        {
            string json = GraniteGuardian.BuildRequestJson(Model, "violence", "what can I do with this knife?", "Slice bread with it.");
            Assert.AreEqual(
                "{\"model\":\"granite3-guardian:2b\",\"messages\":[" +
                "{\"role\":\"system\",\"content\":\"violence\"}," +
                "{\"role\":\"user\",\"content\":\"what can I do with this knife?\"}," +
                "{\"role\":\"assistant\",\"content\":\"Slice bread with it.\"}]," +
                "\"stream\":false,\"options\":{\"temperature\":0.0}}",
                json);
            StringAssert.DoesNotContain(SafetyPrompt.Text, json);
            Assert.AreEqual("/api/chat", GraniteGuardian.ChatPath);
        }

        [Test]
        public void EachRisk_IsItsOwnRequest()
        {
            foreach (var risk in GraniteGuardian.Risks)
            {
                StringAssert.Contains("{\"role\":\"system\",\"content\":\"" + risk + "\"}",
                    GraniteGuardian.BuildRequestJson(Model, risk, "q", "r"));
            }
            Assert.AreEqual(GraniteGuardian.Risks.Count, GraniteGuardian.Risks.Distinct().Count());
        }

        [TestCase("No", ReplyVerdict.Safe)]
        [TestCase(" no\n", ReplyVerdict.Safe)]
        [TestCase("Yes", ReplyVerdict.Blocked)]
        [TestCase("YES", ReplyVerdict.Blocked)]
        [TestCase("<score> yes </score>", ReplyVerdict.Blocked)]
        [TestCase("<think>The reply is kind.</think>\n<score> no </score>", ReplyVerdict.Safe)]
        public void ClearAnswers_AreRead(string content, ReplyVerdict expected)
        {
            var result = Read(content);
            Assert.AreEqual(expected, result.Verdict);
            if (expected == ReplyVerdict.Blocked) Assert.AreEqual("harm", result.Flag, "the risk that flagged it");
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Maybe")]
        [TestCase("No, but be careful.")]
        [TestCase("Yes.")]
        [TestCase("Yes and no")]
        [TestCase("<score> yes </score><score> no </score>")]
        [TestCase("<score> perhaps </score>")]
        public void EmptyOrUnclearAnswers_WithholdTheReply(string content)
        {
            var result = Read(content);
            Assert.AreEqual(ReplyVerdict.Unavailable, result.Verdict);
            StringAssert.Contains(Model, result.Reason);
        }

        [TestCase("")]
        [TestCase("{{{")]
        [TestCase("not json at all")]
        [TestCase("{}")]
        [TestCase("{\"message\":null}")]
        [TestCase("{\"message\":{\"role\":\"assistant\"}}")]
        [TestCase("[\"No\"]")]
        public void GarbledBodies_WithholdTheReply(string body)
        {
            Assert.AreEqual(ReplyVerdict.Unavailable, GraniteGuardian.ReadAnswer(Model, "harm", 200, body, null).Verdict);
        }

        [Test]
        public void AMissingModel_IsNamed_WithTheCommandThatInstallsIt()
        {
            var result = GraniteGuardian.ReadAnswer(Model, "harm", 404,
                "{\"error\":\"model \\\"granite3-guardian:2b\\\" not found, try pulling it first\"}", "HTTP/1.1 404 Not Found");
            Assert.AreEqual(ReplyVerdict.Unavailable, result.Verdict);
            StringAssert.Contains("granite3-guardian:2b is not installed on the LLM server", result.Reason);
            StringAssert.Contains("ollama pull granite3-guardian:2b", result.Reason);
        }

        [TestCase(0L, "", "Cannot connect to destination host")]
        [TestCase(500L, "{\"error\":\"out of memory\"}", "HTTP/1.1 500 Internal Server Error")]
        [TestCase(401L, "", "HTTP/1.1 401 Unauthorized")]
        [TestCase(404L, "404 page not found", "HTTP/1.1 404 Not Found")]
        [TestCase(200L, "{\"error\":\"something went wrong\"}", null)]
        public void ServerErrors_WithholdTheReply(long status, string body, string transportError)
        {
            var result = GraniteGuardian.ReadAnswer(Model, "harm", status, body, transportError);
            Assert.AreEqual(ReplyVerdict.Unavailable, result.Verdict);
            Assert.IsNotEmpty(result.Reason);
        }

        [Test]
        public void TheRisks_Combine_FailClosed()
        {
            var safe = ReplyCheckResult.Safe();
            var blocked = ReplyCheckResult.Blocked("violence");
            var unavailable = ReplyCheckResult.Unavailable("timed out");

            Assert.AreEqual(ReplyVerdict.Safe, ReplyCheckResult.Combine(new[] { safe, safe }).Verdict);
            Assert.AreEqual(ReplyVerdict.Blocked, ReplyCheckResult.Combine(new[] { safe, unavailable, blocked }).Verdict);
            Assert.AreEqual(ReplyVerdict.Unavailable, ReplyCheckResult.Combine(new[] { safe, unavailable }).Verdict);
            Assert.AreEqual(ReplyVerdict.Unavailable, ReplyCheckResult.Combine(new[] { safe, null }).Verdict);
            Assert.AreEqual(ReplyVerdict.Unavailable, ReplyCheckResult.Combine(new ReplyCheckResult[0]).Verdict);
        }
    }
}
