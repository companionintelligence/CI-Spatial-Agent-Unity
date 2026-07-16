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
    /// </summary>
    public class XRScanManifestPostProcessor : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 1;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;

            var permissions = new List<string> { "android.permission.RECORD_AUDIO" }; // STT microphone

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var manifest = doc.DocumentElement;
            if (manifest == null) return;

            foreach (var perm in permissions)
            {
                if (HasPermission(manifest, perm)) continue;
                var el = doc.CreateElement("uses-permission");
                el.SetAttribute("name", AndroidNs, perm);
                manifest.AppendChild(el);
            }

            doc.Save(manifestPath);
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
