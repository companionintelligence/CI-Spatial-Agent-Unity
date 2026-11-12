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
    /// (<c>android:networkSecurityConfig="@xml/xrscan_network_security_config"</c>).
    /// <para><b>Scope.</b> Android applies the config's cleartext rules only to networking that consults
    /// <c>android.security.NetworkSecurityPolicy</c>: Java/Kotlin stacks in Android plugins (HttpURLConnection,
    /// OkHttp, WebView) and the platform media extractor behind VideoPlayer URL playback. The rules do <b>not</b>
    /// reach the app's own HTTP traffic. Every XRScan backend (LLM, speech, CV server, glTF sync) uses
    /// UnityWebRequest, which on Android runs on Unity's native libcurl inside libunity.so and never consults that
    /// policy. UnityWebRequest cleartext is gated only by Player Settings &gt; "Allow downloads over HTTP" (enforced
    /// natively; see <see cref="UnityHttpBuildGuard"/>) and by the app-level <see cref="CleartextPolicy"/>.
    /// For the Java-level stacks, this config is defense in depth:</para>
    /// <list type="bullet">
    /// <item><b>Release builds</b>: no cleartext to any host, even when Unity's HTTP setting adds
    /// <c>android:usesCleartextTraffic="true"</c> to the manifest (the config takes precedence over it).</item>
    /// <item><b>Development builds</b>: cleartext only for the LAN hosts in
    /// <see cref="CompanionConfig.devCleartextHosts"/> (non-LAN entries are dropped). This is the same allowlist
    /// that <see cref="CleartextPolicy"/> applies to UnityWebRequest.</item>
    /// </list>
    /// <para><b>Trust anchors</b>: system CAs. Development (debuggable) builds also trust user-installed CAs through
    /// <c>debug-overrides</c>, so a LAN server can use a certificate from a locally installed CA. Unlike the cleartext
    /// rules, these anchors can also reach UnityWebRequest: Unity loads its root certificates through the platform's
    /// default <c>X509TrustManager</c> (JNI), which Android 7+ builds from this config.</para>
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
                    ? "Development build: Java-level networking may use cleartext only for " +
                      (hosts.Count == 0 ? "no host" : string.Join(", ", hosts)) + "."
                    : "Release build: Java-level networking may not use cleartext to any host.") +
                " UnityWebRequest ignores this config; Unity's 'Allow downloads over HTTP' setting (UnityHttpBuildGuard) " +
                "and CleartextPolicy gate it.");
        }

        /// <summary>The network security config XML for the given cleartext allowlist and build type.</summary>
        public static string BuildXml(IReadOnlyCollection<string> cleartextHosts, bool developmentBuild)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<!-- Generated at build time by XRScan NetworkSecurityConfigGenerator. Do not edit. -->");
            sb.AppendLine("<!-- The cleartext rules apply to Java-level networking (NetworkSecurityPolicy) only. UnityWebRequest runs on");
            sb.AppendLine("     Unity's native HTTP stack: its cleartext is gated by Player Settings, Allow downloads over HTTP, and by");
            sb.AppendLine("     XRScan CleartextPolicy. -->");
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
        public static List<string> CollectDevCleartextHosts() => CollectDevCleartextHosts(true);

        /// <summary>As <see cref="CollectDevCleartextHosts()"/>; <paramref name="logRejected"/> warns about dropped non-LAN entries.</summary>
        public static List<string> CollectDevCleartextHosts(bool logRejected)
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
                if (logRejected && rejected.Count > 0)
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
