using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace XRScan.Tests.EditMode
{
    /// <summary>
    /// Keeps <c>THIRD_PARTY_NOTICES.md</c> in step with the packages the project resolves: every package
    /// in <c>Packages/packages-lock.json</c> (direct or indirect, built-in modules aside) must be listed
    /// there by its id, so adding a dependency without recording its licence there fails the tests.
    /// </summary>
    public class ThirdPartyNoticesTests
    {
        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [Test]
        public void EveryResolvedPackageIsListedInThirdPartyNotices()
        {
            var lockFile = File.ReadAllText(Path.Combine(ProjectRoot, "Packages", "packages-lock.json"));
            var notices = File.ReadAllText(Path.Combine(ProjectRoot, "THIRD_PARTY_NOTICES.md"));

            // Top-level entries of "dependencies" are the only keys indented by exactly four spaces.
            var packages = Regex.Matches(lockFile, "^ {4}\"([^\"]+)\"\\s*:\\s*\\{", RegexOptions.Multiline)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .Where(id => !id.StartsWith("com.unity.modules.", StringComparison.Ordinal))
                .ToArray();
            Assert.That(packages, Is.Not.Empty, "no packages parsed from packages-lock.json");

            var missing = packages.Where(id => !notices.Contains("`" + id + "`")).ToArray();
            CollectionAssert.IsEmpty(missing,
                "packages resolved by the project but not listed (as `id`) in THIRD_PARTY_NOTICES.md");
        }
    }
}
