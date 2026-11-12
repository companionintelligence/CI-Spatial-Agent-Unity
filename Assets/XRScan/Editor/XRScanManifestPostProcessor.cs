using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;

namespace XRScan.EditorTools
{
    /// <summary>
    /// Adds the microphone permission (needed by speech-to-text) to the generated Android manifest
    /// at build time — merge-safe, and it never touches a hand-authored manifest.
    ///
    /// Deliberately does NOT add the Meta permissions (horizonos.permission.HEADSET_CAMERA,
    /// com.oculus.permission.USE_SCENE): com.unity.xr.meta-openxr already injects those itself,
    /// correctly gated on its AR features being enabled. Adding them here duplicated the package's
    /// work and leaked Meta-only permissions into the Pico APK.
    ///
    /// On any build other than Pico it also strips the stray <c>com.pvr.tobactivate.permission.AUTH_CHECK</c>
    /// permission that PICO's <c>tob_api</c> .aar (a unityLibrary dependency) merges in — via a
    /// <c>tools:node="remove"</c> marker, which the manifest merger honours for
    /// dependency-contributed permissions. The matching PICO native libs are dropped separately by
    /// <see cref="XRScanNativeLibExcluder"/>.
    /// </summary>
    public class XRScanManifestPostProcessor : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private const string ToolsNs = "http://schemas.android.com/tools";

        public int callbackOrder => 1;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var manifest = doc.DocumentElement;
            if (manifest == null) return;

            AddPermission(doc, manifest, "android.permission.RECORD_AUDIO"); // STT microphone

            // PICO's tob_api.aar contributes this permission during manifest merge; only a Pico build
            // wants it. (Outside Pico builds XRScanNativeLibExcluder also drops the .aar itself; the
            // remove marker stays as a belt-and-braces guard.)
            if (!XRScanBuilder.BuildingForPico())
                RemovePermission(doc, manifest, "com.pvr.tobactivate.permission.AUTH_CHECK");

            doc.Save(manifestPath);
        }

        private static void AddPermission(XmlDocument doc, XmlElement manifest, string name)
        {
            if (HasPermission(manifest, name)) return;
            var el = doc.CreateElement("uses-permission");
            el.SetAttribute("name", AndroidNs, name);
            manifest.AppendChild(el);
        }

        /// <summary>Adds a <c>&lt;uses-permission … tools:node="remove"/&gt;</c> merge directive so the
        /// merger drops a permission contributed by a dependency .aar.</summary>
        private static void RemovePermission(XmlDocument doc, XmlElement manifest, string name)
        {
            EnsureToolsNamespace(doc, manifest);
            var el = doc.CreateElement("uses-permission");
            el.SetAttribute("name", AndroidNs, name);
            var node = doc.CreateAttribute("tools", "node", ToolsNs);
            node.Value = "remove";
            el.Attributes.Append(node);
            manifest.AppendChild(el);
        }

        private static void EnsureToolsNamespace(XmlDocument doc, XmlElement manifest)
        {
            if (!string.IsNullOrEmpty(manifest.GetAttribute("xmlns:tools"))) return;
            var xmlns = doc.CreateAttribute("xmlns", "tools", "http://www.w3.org/2000/xmlns/");
            xmlns.Value = ToolsNs;
            manifest.Attributes.Append(xmlns);
        }

        private static bool HasPermission(XmlElement manifest, string name)
        {
            foreach (XmlElement e in manifest.GetElementsByTagName("uses-permission"))
            {
                if (e.GetAttribute("name", AndroidNs) == name) return true;
            }
            return false;
        }
    }
}
