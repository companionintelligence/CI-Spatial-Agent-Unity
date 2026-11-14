using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using NUnit.Framework;
using XRScan.EditorTools;

namespace XRScan.Tests.Config
{
    /// <summary>
    /// The manifest values the Meta Horizon Store checks at upload: installLocation auto, supportedDevices
    /// quest3|quest3s (scanning needs the Passthrough Camera API, which only Quest 3 and 3S have), and no BLUETOOTH
    /// or MODIFY_AUDIO_SETTINGS (VRC.Quest.Security.2). The built APK is checked with aapt2 (docs/BUILD.md,
    /// "Verifying a build"); these tests cover the rules and the project settings behind them.
    /// </summary>
    public class QuestManifestTests
    {
        private const string AndroidNs = QuestManifestRules.AndroidNs;
        private const string ToolsNs = QuestManifestRules.ToolsNs;

        // The shape of the manifests Unity 6000.6 generates for the launcher and unityLibrary modules.
        private const string Launcher =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" android:installLocation=\"preferExternal\">\n" +
            "  <supports-screens android:smallScreens=\"true\" />\n" +
            "  <application android:label=\"@string/app_name\" android:icon=\"@mipmap/app_icon\" />\n" +
            "</manifest>";

        private const string Library =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" xmlns:tools=\"http://schemas.android.com/tools\">\n" +
            "  <uses-permission android:name=\"android.permission.INTERNET\" />\n" +
            "  <uses-permission android:name=\"android.permission.RECORD_AUDIO\" />\n" +
            "  <uses-permission android:name=\"android.permission.MODIFY_AUDIO_SETTINGS\" />\n" +
            "  <uses-permission android:name=\"android.permission.BLUETOOTH\" />\n" +
            "  <application><activity android:name=\"com.unity3d.player.UnityPlayerGameActivity\" /></application>\n" +
            "</manifest>";

        private static XmlDocument Load(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            return doc;
        }

        private static List<XmlElement> Permissions(XmlDocument doc, string name) =>
            doc.DocumentElement.ChildNodes.OfType<XmlElement>()
                .Where(e => e.Name == "uses-permission" && e.GetAttribute("name", AndroidNs) == name).ToList();

        private static XmlElement SupportedDevices(XmlDocument doc) =>
            doc.SelectNodes("/manifest/application/meta-data").OfType<XmlElement>()
                .SingleOrDefault(e => e.GetAttribute("name", AndroidNs) == QuestManifestRules.SupportedDevicesKey);

        [Test]
        public void Launcher_IsAutoInstalled_ForQuest3And3S_AndDropsTheUnusedPermissions()
        {
            var doc = Load(Launcher);
            QuestManifestRules.ApplyToLauncher(doc, true);
            QuestManifestRules.ApplyToLauncher(doc, true); // idempotent

            Assert.AreEqual("auto", doc.DocumentElement.GetAttribute("installLocation", AndroidNs));

            var devices = SupportedDevices(doc);
            Assert.IsNotNull(devices);
            Assert.AreEqual("quest3|quest3s", devices.GetAttribute("value", AndroidNs));
            Assert.AreEqual("android:value", devices.GetAttribute("replace", ToolsNs),
                "the launcher's value must replace the one Unity's Meta Quest feature merges in");

            foreach (var permission in new[] { "android.permission.BLUETOOTH", "android.permission.MODIFY_AUDIO_SETTINGS" })
            {
                var markers = Permissions(doc, permission);
                Assert.AreEqual(1, markers.Count, permission);
                Assert.AreEqual("remove", markers[0].GetAttribute("node", ToolsNs), permission);
            }

            string xml = doc.OuterXml;
            StringAssert.Contains("android:installLocation=\"auto\"", xml);
            StringAssert.Contains("tools:replace=\"android:value\"", xml);
            StringAssert.Contains("tools:node=\"remove\"", xml);
            StringAssert.Contains("xmlns:tools=\"http://schemas.android.com/tools\"", xml);
        }

        [Test]
        public void Launcher_OfAPicoBuild_ListsNoQuestDevices()
        {
            var doc = Load(Launcher);
            QuestManifestRules.ApplyToLauncher(doc, false);
            Assert.IsNull(SupportedDevices(doc));
            Assert.AreEqual("auto", doc.DocumentElement.GetAttribute("installLocation", AndroidNs));
        }

        [Test]
        public void Library_LosesBluetoothAndAudioSettings_ButKeepsTheMicrophone()
        {
            var doc = Load(Library);
            QuestManifestRules.ApplyToLibrary(doc);

            CollectionAssert.IsEmpty(Permissions(doc, "android.permission.BLUETOOTH"));
            CollectionAssert.IsEmpty(Permissions(doc, "android.permission.MODIFY_AUDIO_SETTINGS"));
            Assert.AreEqual(1, Permissions(doc, "android.permission.RECORD_AUDIO").Count);
            Assert.AreEqual(1, Permissions(doc, "android.permission.INTERNET").Count);
        }

        [Test]
        public void PlayerSettings_PreferAutoInstallLocation()
        {
            // PlayerSettings.Android.preferredInstallLocation: 0 = Auto, 1 = PreferExternal, 2 = ForceInternal.
            StringAssert.Contains("\n  AndroidPreferredInstallLocation: 0\n",
                File.ReadAllText("ProjectSettings/ProjectSettings.asset").Replace("\r\n", "\n"));
        }

        [Test]
        public void OpenXRMetaQuestFeature_TargetsOnlyQuest3And3S()
        {
            // Unity's Meta Quest feature uses Meta's codename "eureka" for Quest 3.
            CollectionAssert.AreEquivalent(new[] { "eureka", "quest3s" }, EnabledOpenXRTargets("MetaQuestFeature Android"));
        }

        private static List<string> EnabledOpenXRTargets(string featureName)
        {
            var enabled = new List<string>();
            bool inFeature = false;
            string manifestName = null;
            foreach (var line in File.ReadAllLines("Assets/XR/Settings/OpenXRPackageSettings.asset"))
            {
                if (line.StartsWith("  m_Name: ", StringComparison.Ordinal))
                {
                    inFeature = line.Trim() == "m_Name: " + featureName;
                    continue;
                }
                if (!inFeature) continue;

                var name = Regex.Match(line, @"^    manifestName: (\S+)$");
                if (name.Success)
                {
                    manifestName = name.Groups[1].Value;
                    continue;
                }

                var state = Regex.Match(line, @"^    enabled: ([01])$");
                if (state.Success && manifestName != null)
                {
                    if (state.Groups[1].Value == "1") enabled.Add(manifestName);
                    manifestName = null;
                }
            }

            Assert.IsNotEmpty(enabled, featureName + " has no enabled target devices");
            return enabled;
        }
    }
}
