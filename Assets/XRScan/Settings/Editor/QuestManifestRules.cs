using System.Collections.Generic;
using System.Xml;

namespace XRScan.EditorTools
{
    /// <summary>
    /// Manifest values the Meta Horizon Store checks at upload, applied to the Gradle project Unity generates (by
    /// <c>XRScanManifestPostProcessor</c>). The launcher (application) manifest has the highest merge priority, so
    /// what is set there wins over anything Unity, a package or a library contributes:
    /// <list type="bullet">
    /// <item><c>android:installLocation="auto"</c>, which Meta requires (Player Settings also say Auto).</item>
    /// <item><c>com.oculus.supportedDevices</c> = <c>quest3|quest3s</c> on Quest builds: scanning needs the
    /// Passthrough Camera API, which only Quest 3 and Quest 3S have. Unity's Meta Quest feature writes Meta's
    /// codenames from its target list; <c>tools:replace</c> puts the documented names in their place.</item>
    /// <item>No BLUETOOTH or MODIFY_AUDIO_SETTINGS (VRC.Quest.Security.2, minimum permissions). Unity adds them
    /// with the microphone; the app uses neither, and BLUETOOTH also makes Bluetooth hardware a required feature.</item>
    /// </list>
    /// </summary>
    public static class QuestManifestRules
    {
        public const string AndroidNs = "http://schemas.android.com/apk/res/android";
        public const string ToolsNs = "http://schemas.android.com/tools";

        public const string InstallLocation = "auto";
        public const string SupportedDevicesKey = "com.oculus.supportedDevices";
        public const string SupportedDevices = "quest3|quest3s";

        public static readonly IReadOnlyList<string> UnusedPermissions = new[]
        {
            "android.permission.BLUETOOTH",
            "android.permission.MODIFY_AUDIO_SETTINGS",
        };

        /// <summary>The launcher module's manifest: install location, merge markers that drop the unused
        /// permissions from every merged manifest, and on Quest builds the supported devices.</summary>
        public static void ApplyToLauncher(XmlDocument doc, bool questBuild)
        {
            var manifest = doc?.DocumentElement;
            if (manifest == null) return;

            SetAttribute(doc, manifest, "android", "installLocation", AndroidNs, InstallLocation);
            foreach (var permission in UnusedPermissions)
            {
                RemoveDeclarations(manifest, permission);
                AddRemoveMarker(doc, manifest, permission);
            }

            if (questBuild) SetSupportedDevices(doc, manifest);
        }

        /// <summary>The unityLibrary module's manifest: drops the unused permissions Unity wrote there, and keeps its
        /// install location (if it has one) in line with the launcher's.</summary>
        public static void ApplyToLibrary(XmlDocument doc)
        {
            var manifest = doc?.DocumentElement;
            if (manifest == null) return;

            if (manifest.HasAttribute("installLocation", AndroidNs))
                SetAttribute(doc, manifest, "android", "installLocation", AndroidNs, InstallLocation);
            foreach (var permission in UnusedPermissions) RemoveDeclarations(manifest, permission);
        }

        private static void SetSupportedDevices(XmlDocument doc, XmlElement manifest)
        {
            var application = FirstChild(manifest, "application");
            if (application == null)
            {
                application = doc.CreateElement("application");
                manifest.AppendChild(application);
            }

            XmlElement meta = null;
            foreach (XmlNode node in application.ChildNodes)
            {
                if (node is XmlElement e && e.Name == "meta-data" && e.GetAttribute("name", AndroidNs) == SupportedDevicesKey)
                {
                    meta = e;
                    break;
                }
            }

            if (meta == null)
            {
                meta = doc.CreateElement("meta-data");
                SetAttribute(doc, meta, "android", "name", AndroidNs, SupportedDevicesKey);
                application.AppendChild(meta);
            }

            SetAttribute(doc, meta, "android", "value", AndroidNs, SupportedDevices);
            EnsureToolsNamespace(doc, manifest);
            SetAttribute(doc, meta, "tools", "replace", ToolsNs, "android:value");
        }

        private static void RemoveDeclarations(XmlElement manifest, string permission)
        {
            var found = new List<XmlElement>();
            foreach (XmlNode node in manifest.ChildNodes)
            {
                if (node is XmlElement e && e.Name == "uses-permission" && e.GetAttribute("name", AndroidNs) == permission &&
                    e.GetAttribute("node", ToolsNs) != "remove")
                {
                    found.Add(e);
                }
            }
            foreach (var e in found) manifest.RemoveChild(e);
        }

        private static void AddRemoveMarker(XmlDocument doc, XmlElement manifest, string permission)
        {
            foreach (XmlNode node in manifest.ChildNodes)
            {
                if (node is XmlElement e && e.Name == "uses-permission" && e.GetAttribute("name", AndroidNs) == permission &&
                    e.GetAttribute("node", ToolsNs) == "remove")
                {
                    return;
                }
            }

            EnsureToolsNamespace(doc, manifest);
            var marker = doc.CreateElement("uses-permission");
            SetAttribute(doc, marker, "android", "name", AndroidNs, permission);
            SetAttribute(doc, marker, "tools", "node", ToolsNs, "remove");
            manifest.AppendChild(marker);
        }

        private static void EnsureToolsNamespace(XmlDocument doc, XmlElement manifest)
        {
            if (!string.IsNullOrEmpty(manifest.GetAttribute("xmlns:tools"))) return;
            SetAttribute(doc, manifest, "xmlns", "tools", "http://www.w3.org/2000/xmlns/", ToolsNs);
        }

        /// <summary>Sets a namespaced attribute with an explicit prefix (android:, tools:), as the manifest merger expects.</summary>
        private static void SetAttribute(XmlDocument doc, XmlElement element, string prefix, string localName, string ns, string value)
        {
            var existing = element.GetAttributeNode(localName, ns);
            if (existing != null)
            {
                existing.Value = value;
                return;
            }

            var attribute = doc.CreateAttribute(prefix, localName, ns);
            attribute.Value = value;
            element.Attributes.Append(attribute);
        }

        private static XmlElement FirstChild(XmlElement parent, string name)
        {
            foreach (XmlNode node in parent.ChildNodes)
            {
                if (node is XmlElement e && e.Name == name) return e;
            }
            return null;
        }
    }
}
