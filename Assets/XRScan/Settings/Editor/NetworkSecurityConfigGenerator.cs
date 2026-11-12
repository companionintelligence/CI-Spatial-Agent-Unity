using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using XRScan.Core;

namespace XRScan.EditorTools
{
    /// <summary>
    /// Generates the Android network security config for every Android build and points the app manifest at it
    /// (<c>android:networkSecurityConfig="@xml/xrscan_network_security_config"</c>):
    /// <list type="bullet">
    /// <item><b>Release builds</b>: cleartext HTTP is refused for every host (system CAs only), whatever the
    /// Player's "Allow downloads over HTTP" setting adds to the manifest.</item>
    /// <item><b>Development builds</b>: cleartext is allowed only for the LAN hosts listed in
    /// <see cref="CompanionConfig.devCleartextHosts"/> (non-LAN entries are dropped), and user-installed CAs are
    /// trusted so a LAN server can use a self-signed https certificate.</item>
    /// </list>
    /// The same allowlist drives the app-level check (<see cref="CleartextPolicy"/>), so the platform policy and
    /// the app policy agree. Note: Unity itself still refuses http:// requests unless Player Settings &gt;
    /// "Allow downloads over HTTP" is "Allowed in development builds" (a warning is logged when it is not).
    /// </summary>
    public class NetworkSecurityConfigGenerator : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
    {
        public const string ResourceName = "xrscan_network_security_config";
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";

        private static bool s_developmentBuild;

        // After XRScanManifestPostProcessor (1) and XRScanNativeLibExcluder (2), which edit the same project.
        public int callbackOrder => 10;

        public void OnPreprocessBuild(BuildReport report)
        {
            s_developmentBuild = (report.summary.options & BuildOptions.Development) != 0;
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            bool development = s_developmentBuild;
            var hosts = development ? CollectDevCleartextHosts() : new List<string>();

            string xmlDir = Path.Combine(path, "src", "main", "res", "xml");
            Directory.CreateDirectory(xmlDir);
            File.WriteAllText(Path.Combine(xmlDir, ResourceName + ".xml"), BuildXml(hosts, development), new UTF8Encoding(false));

            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (File.Exists(manifestPath)) ReferenceFromManifest(manifestPath);
            else Debug.LogWarning("[NetworkSecurityConfig] No manifest at " + manifestPath + "; the config is not referenced.");

            Debug.Log("[NetworkSecurityConfig] " + (development
                ? "Development build: cleartext allowed only for " + (hosts.Count == 0 ? "no host" : string.Join(", ", hosts)) + "."
                : "Release build: cleartext HTTP disabled for every host."));

            if (development && hosts.Count > 0 && PlayerSettings.insecureHttpOption == InsecureHttpOption.NotAllowed)
            {
                Debug.LogWarning("[NetworkSecurityConfig] Dev cleartext hosts are configured, but Player Settings > " +
                                 "'Allow downloads over HTTP' is 'Not allowed', so Unity will still refuse http:// requests. " +
                                 "Set it to 'Allowed in development builds' to reach them.");
            }
        }

        /// <summary>The network security config XML for the given cleartext allowlist and build type.</summary>
        public static string BuildXml(IReadOnlyCollection<string> cleartextHosts, bool developmentBuild)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<!-- Generated at build time by XRScan NetworkSecurityConfigGenerator. Do not edit. -->");
            sb.AppendLine("<network-security-config>");
            sb.AppendLine("    <base-config cleartextTrafficPermitted=\"false\">");
            sb.AppendLine("        <trust-anchors>");
            sb.AppendLine("            <certificates src=\"system\" />");
            sb.AppendLine("        </trust-anchors>");
            sb.AppendLine("    </base-config>");

            if (developmentBuild && cleartextHosts != null && cleartextHosts.Count > 0)
            {
                sb.AppendLine("    <!-- Development build only: explicitly configured LAN hosts (CompanionConfig.devCleartextHosts). -->");
                sb.AppendLine("    <domain-config cleartextTrafficPermitted=\"true\">");
                foreach (var host in cleartextHosts)
                {
                    sb.Append("        <domain includeSubdomains=\"false\">").Append(SecurityElement.Escape(host)).AppendLine("</domain>");
                }
                sb.AppendLine("    </domain-config>");
            }

            if (developmentBuild)
            {
                sb.AppendLine("    <debug-overrides>");
                sb.AppendLine("        <trust-anchors>");
                sb.AppendLine("            <certificates src=\"user\" />");
                sb.AppendLine("        </trust-anchors>");
                sb.AppendLine("    </debug-overrides>");
            }

            sb.AppendLine("</network-security-config>");
            return sb.ToString();
        }

        /// <summary>Union of the sanitized (LAN-only) allowlists of every CompanionConfig asset in the project.</summary>
        public static List<string> CollectDevCleartextHosts()
        {
            var hosts = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(CompanionConfig)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<CompanionConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                var kept = asset.SanitizedDevCleartextHosts(out var rejected);
                foreach (var host in kept)
                {
                    if (!hosts.Contains(host)) hosts.Add(host);
                }
                if (rejected.Count > 0)
                {
                    Debug.LogWarning("[NetworkSecurityConfig] " + asset.name + ": ignoring non-LAN cleartext hosts " + string.Join(", ", rejected));
                }
            }
            return hosts;
        }

        private static void ReferenceFromManifest(string manifestPath)
        {
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var manifest = doc.DocumentElement;
            if (manifest == null) return;

            var application = manifest.SelectSingleNode("application") as XmlElement;
            if (application == null)
            {
                application = doc.CreateElement("application");
                manifest.AppendChild(application);
            }

            string value = "@xml/" + ResourceName;
            string existing = application.GetAttribute("networkSecurityConfig", AndroidNs);
            if (!string.IsNullOrEmpty(existing) && existing != value)
            {
                Debug.LogWarning("[NetworkSecurityConfig] The manifest already uses " + existing + "; leaving it in place.");
                return;
            }

            application.SetAttribute("networkSecurityConfig", AndroidNs, value);
            doc.Save(manifestPath);
        }
    }
}
