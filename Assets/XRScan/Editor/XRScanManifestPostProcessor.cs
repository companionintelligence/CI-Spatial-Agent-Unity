using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace XRScan.EditorTools
{
    /// <summary>
    /// Injects the runtime permissions the app needs into the generated Android manifest at build
    /// time (merge-safe — never touches a hand-authored manifest): microphone for STT, the Quest
    /// passthrough-camera permission for live-camera scanning, and Meta scene understanding.
    /// </summary>
    public class XRScanManifestPostProcessor : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";

        private static readonly string[] Permissions =
        {
            "android.permission.RECORD_AUDIO",        // STT microphone
            "horizonos.permission.HEADSET_CAMERA",    // Quest Passthrough Camera API
            "com.oculus.permission.USE_SCENE"         // Meta scene understanding
        };

        public int callbackOrder => 1;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var manifest = doc.DocumentElement;
            if (manifest == null) return;

            foreach (var perm in Permissions)
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
