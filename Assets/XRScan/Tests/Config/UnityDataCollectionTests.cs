using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using XRScan.Core;

namespace XRScan.Tests.Config
{
    /// <summary>
    /// Privacy launch condition S1: the app sends Unity no device data. The published privacy policy no longer
    /// mentions Unity, so hardware statistics and every Unity service must stay off.
    /// </summary>
    public class UnityDataCollectionTests
    {
        private static string Read(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

        [Test]
        public void HardwareStatistics_AreOff()
        {
            StringAssert.Contains("\n  submitAnalytics: 0\n", Read("ProjectSettings/ProjectSettings.asset"));
        }

        [Test]
        public void CrashReporting_IsOff()
        {
            StringAssert.Contains("\n  enableCrashReportAPI: 0\n", Read("ProjectSettings/ProjectSettings.asset"));
        }

        [Test]
        public void EveryUnityService_IsOff()
        {
            string connect = Read("ProjectSettings/UnityConnectSettings.asset");
            var enabled = Regex.Matches(connect, @"^\s*(m_Enabled|m_EnableCloudDiagnosticsReporting|m_EngineDiagnosticsEnabled): (\d)$",
                    RegexOptions.Multiline)
                .Cast<Match>().ToList();
            Assert.GreaterOrEqual(enabled.Count, 7, "UnityConnectSettings changed shape; check every service is still off");
            foreach (var match in enabled)
            {
                Assert.AreEqual("0", match.Groups[2].Value, match.Value.Trim());
            }
        }

        [Test]
        public void TheRuntimeSwitch_RunsBeforeTheFirstScene()
        {
            var method = typeof(UnityDataCollection).GetMethod("TurnOffAtStartup", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            var attribute = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.IsNotNull(attribute);
            Assert.AreEqual(RuntimeInitializeLoadType.SubsystemRegistration, attribute.loadType);
        }
    }
}
