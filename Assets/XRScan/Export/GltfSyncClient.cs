using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using XRScan.Core;

namespace XRScan.Export
{
    /// <summary>
    /// Uploads an exported .glb + pin metadata to the server (/v1/gltf). Ported from CompanionScan;
    /// base URL comes from ServerConfig (no hardcoding).
    /// </summary>
    public class GltfSyncClient
    {
        private readonly ServerConfig _config;

        public GltfSyncClient(ServerConfig config) => _config = config;

        public async Task<bool> UploadAsync(byte[] glb, string fileName, string metadataJson)
        {
            var baseUrl = _config != null ? _config.ResolveBaseUrl() : null;
            if (string.IsNullOrWhiteSpace(baseUrl) || glb == null) return false;

            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", glb, fileName, "model/gltf-binary")
            };
            if (!string.IsNullOrEmpty(metadataJson))
                form.Add(new MultipartFormDataSection("metadata", metadataJson));

            using var req = UnityWebRequest.Post(baseUrl.TrimEnd('/') + "/v1/gltf", form);
            req.timeout = Mathf.Max(5, _config.requestTimeoutSeconds);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            return req.result == UnityWebRequest.Result.Success;
        }
    }
}
