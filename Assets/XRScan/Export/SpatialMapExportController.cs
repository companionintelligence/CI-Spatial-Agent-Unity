using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using XRScan.Core;
using XRScan.Mapping;

namespace XRScan.Export
{
    /// <summary>
    /// Triggers a scan export: gathers the pin markers, writes a .glb + a pin-metadata sidecar
    /// (labels / positions / scores), and optionally uploads them to the server.
    /// </summary>
    public class SpatialMapExportController : MonoBehaviour
    {
        [SerializeField] private SpatialPinManager pinManager;
        [SerializeField] private ServerConfig serverConfig;
        [SerializeField] private bool uploadToServer = false;

        private readonly SpatialMapExporter _exporter = new SpatialMapExporter();

        public async void ExportScan() => await ExportScanAsync();

        public async Task<bool> ExportScanAsync()
        {
            if (pinManager == null || pinManager.PinRoot == null)
            {
                Debug.LogWarning("[Export] No pin manager / pin root.");
                return false;
            }

            var root = pinManager.PinRoot.gameObject;
            string path = Path.Combine(Application.persistentDataPath, "xrscan_map.glb");

            bool ok = await _exporter.ExportAsync(new[] { root }, path, true);
            if (!ok) { Debug.LogError("[Export] glTF export failed."); return false; }

            string meta = BuildMetadata();
            File.WriteAllText(Path.ChangeExtension(path, ".json"), meta);
            Debug.Log("[Export] wrote " + path + " (" + pinManager.Pins.Count + " pins)");

            if (uploadToServer && serverConfig != null)
            {
                var glb = File.ReadAllBytes(path);
                bool up = await new GltfSyncClient(serverConfig).UploadAsync(glb, "xrscan_map.glb", meta);
                Debug.Log("[Export] upload " + (up ? "ok" : "failed"));
            }
            return true;
        }

        private string BuildMetadata()
        {
            var sb = new StringBuilder();
            sb.Append("{\"pins\":[");
            var pins = pinManager.Pins;
            for (int i = 0; i < pins.Count; i++)
            {
                var p = pins[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"label\":\"").Append(Escape(p.label)).Append("\",\"x\":").Append(p.position.x.ToString("F3"))
                  .Append(",\"y\":").Append(p.position.y.ToString("F3")).Append(",\"z\":").Append(p.position.z.ToString("F3"))
                  .Append(",\"score\":").Append(p.score.ToString("F2")).Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string Escape(string s)
            => string.IsNullOrEmpty(s) ? string.Empty : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
