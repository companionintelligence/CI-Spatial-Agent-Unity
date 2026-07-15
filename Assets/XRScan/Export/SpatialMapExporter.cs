using System.Threading.Tasks;
using GLTFast.Export;
using UnityEngine;

namespace XRScan.Export
{
    /// <summary>
    /// Exports scanned content (pin markers, optionally room meshes) to a glTF (.glb) via
    /// com.unity.cloud.gltfast — the one glTF stack for the app, replacing CompanionScan's
    /// UnityGLTF path (which does not compile on Unity 6.x).
    /// </summary>
    public class SpatialMapExporter
    {
        public async Task<bool> ExportAsync(GameObject[] roots, string filePath, bool binary = true)
        {
            if (roots == null || roots.Length == 0) return false;

            var settings = new ExportSettings { Format = binary ? GltfFormat.Binary : GltfFormat.Json };
            var export = new GameObjectExport(settings);
            if (!export.AddScene(roots, "XRScanSpatialMap")) return false;
            return await export.SaveToFileAndDispose(filePath);
        }
    }
}
