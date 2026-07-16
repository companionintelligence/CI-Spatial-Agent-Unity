using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;

namespace XRScan.EditorTools
{
    /// <summary>
    /// Injects the runtime permissions the app needs into the generated Android manifest at build
    /// time (merge-safe — never touches a hand-authored manifest). The microphone permission is
    /// always needed for STT; the passthrough-camera and scene permissions are Meta-specific and are
    /// only added to Quest builds (they'd be inert noise on a Pico APK), keyed off the platform
    /// define that XRScanBuilder sets.
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

            bool isQuest = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)
                .Contains("XRSCAN_QUEST");
            if (isQuest)
            {
                permissions.Add("horizonos.permission.HEADSET_CAMERA"); // Quest Passthrough Camera API
                permissions.Add("com.oculus.permission.USE_SCENE");     // Meta scene understanding
            }

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
