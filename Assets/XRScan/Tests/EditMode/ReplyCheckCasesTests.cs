using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using XRScan.Core;
using XRScan.Llm;

namespace XRScan.Tests.EditMode
{
    /// <summary>
    /// The per-category test set of the reply check, tools/reply-check/cases.json, which tools/reply-check/run_reply_check.py
    /// runs against a live Ollama server. Offline, this keeps it in step with the app: the same model and risks, every
    /// category the store sheets name, short replies, and true "local" marks (the replies the on-device rules block, and
    /// no safe reply blocked by them).
    /// </summary>
    public class ReplyCheckCasesTests
    {
        [Serializable]
        private class Case
        {
            public string question;
            public string reply;
            public bool local;
        }

        [Serializable]
        private class Category
        {
            public string id;
            public string name;
            public string sheets;
            public Case[] block;
            public Case[] pass;
        }

        [Serializable]
        private class CaseFile
        {
            public string model;
            public string[] risks;
            public Category[] categories;
        }

        private static readonly string[] SheetCategories =
            { "sexual", "minors", "hate", "harassment", "violence", "self-harm", "crime", "deception" };

        private static CaseFile Load()
        {
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "tools", "reply-check", "cases.json");
            Assert.IsTrue(File.Exists(path), path + " is missing");
            var file = JsonUtility.FromJson<CaseFile>(File.ReadAllText(path));
            Assert.IsNotNull(file?.categories);
            return file;
        }

        [Test]
        public void TheTestSet_AsksWhatTheAppAsks()
        {
            var file = Load();
            Assert.AreEqual(LlmConfig.DefaultSafetyModel, file.model);
            CollectionAssert.AreEqual(GraniteGuardian.Risks, file.risks);
        }

        [Test]
        public void EveryCategoryTheSheetsName_HasRepliesToBlockAndToPass()
        {
            var file = Load();
            CollectionAssert.AreEquivalent(SheetCategories, file.categories.Select(c => c.id));
            foreach (var category in file.categories)
            {
                Assert.IsNotEmpty(category.name, category.id);
                Assert.IsNotEmpty(category.sheets, category.id + ": which sheet names it");
                Assert.GreaterOrEqual(category.block?.Length ?? 0, 3, category.id + ": replies that must be blocked");
                Assert.GreaterOrEqual(category.pass?.Length ?? 0, 3, category.id + ": replies that must pass");
                foreach (var c in category.block.Concat(category.pass))
                {
                    Assert.IsNotEmpty(c.question, category.id);
                    Assert.IsNotEmpty(c.reply, category.id);
                    Assert.LessOrEqual(c.reply.Length, 120, category.id + ": keep the replies short: " + c.reply);
                }
            }
        }

        [Test]
        public void TheLocalMarks_AreWhatTheOnDeviceRulesDo()
        {
            foreach (var category in Load().categories)
            {
                foreach (var c in category.block)
                {
                    Assert.AreEqual(c.local, LocalReplyRules.Blocks(c.reply, out _),
                        category.id + ": 'local' must say whether the on-device rules block: " + c.reply);
                }
                foreach (var c in category.pass)
                {
                    Assert.IsFalse(c.local, category.id + ": a reply that must pass cannot be marked local");
                    Assert.IsFalse(LocalReplyRules.Blocks(c.reply, out string rule),
                        category.id + ": the on-device rules (" + rule + ") block a reply that must pass: " + c.reply);
                }
            }
        }

        [TestCase("I’m a real person, trust me.", LocalReplyRules.HumanClaimRule)]
        [TestCase("I am human.", LocalReplyRules.HumanClaimRule)]
        [TestCase("I'm not a bot.", LocalReplyRules.HumanClaimRule)]
        [TestCase("Teens and sex: let me explain.", LocalReplyRules.MinorsRule)]
        [TestCase("A naked 9 year old.", LocalReplyRules.MinorsRule)]
        [TestCase("I'm not a human; I'm an AI companion.", null)]
        [TestCase("I am not a person, I am an AI.", null)]
        [TestCase("The kids' books are on the shelf.", null)]
        [TestCase("An 18-year-old bottle of wine and a nude-coloured lamp.", null)]
        [TestCase("", null)]
        public void TheOnDeviceRules(string reply, string expectedRule)
        {
            bool blocked = LocalReplyRules.Blocks(reply, out string rule);
            Assert.AreEqual(expectedRule != null, blocked, reply);
            if (blocked) Assert.AreEqual(expectedRule, rule);
        }
    }
}
