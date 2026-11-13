using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using XRScan.Core;

namespace XRScan.Llm
{
    /// <summary>
    /// ILlmBackend backed by a local / LAN Ollama server (/api/generate). Supports text and
    /// vision (base64 image) requests. Ported from CI-Spatial's OllamaConnector; host / port /
    /// model come from LlmConfig, so there is no hardcoded IP. Swap for OpenAI / Claude / the
    /// Companion Intelligence server behind the same ILlmBackend seam with no downstream change.
    /// Every request sends the fixed safety prompt and the persona as Ollama's <c>system</c> message
    /// (<see cref="LlmConfig.BuildSystemMessage"/>) and the user's text, unchanged, as the prompt.
    /// </summary>
    public class OllamaBackend : MonoBehaviour, ILlmBackend
    {
        [SerializeField] private LlmConfig config;

        public UnityEvent OnProcessingStarted { get; } = new UnityEvent();
        public UnityEvent<string> OnResponseReceived { get; } = new StringUnityEvent();
        public UnityEvent<string> OnError { get; } = new StringUnityEvent();

        private bool _initialized;
        private bool _connected;
        private bool _processing;
        private string _baseUrl;

        public bool IsInitialized => _initialized;
        public bool IsConnected => _connected;
        public bool IsProcessing => _processing;
        public string ServerAddress => config != null ? config.ResolveHost() + ":" + config.port : "(unconfigured)";
        public string ModelName => config != null ? config.model : string.Empty;

        public void Configure(LlmConfig cfg) => config = cfg;

        public void Initialize()
        {
            if (_initialized) return;
            if (config == null) { OnError.Invoke("LlmConfig not assigned"); return; }
            _baseUrl = config.BaseUrl();
            StartCoroutine(TestConnection());
        }

        private IEnumerator TestConnection()
        {
            using var req = UnityWebRequest.Get(_baseUrl + "/api/tags");
            req.timeout = Mathf.Max(1, (int)config.requestTimeoutSeconds);
            yield return req.SendWebRequest();
            _connected = req.result == UnityWebRequest.Result.Success;
            _initialized = true;
            if (_connected) Debug.Log("[OllamaBackend] connected " + _baseUrl);
            else OnError.Invoke("Ollama connect failed: " + req.error);
        }

        public void SendTextRequest(string prompt) { if (Ready()) StartCoroutine(Send(prompt, null)); }
        public void SendVisionRequest(Texture2D image, string prompt) { if (Ready()) StartCoroutine(Send(prompt, image)); }

        private bool Ready()
        {
            if (!_initialized) { OnError.Invoke("Ollama not initialized"); return false; }
            if (_processing) { Debug.Log("[OllamaBackend] busy, ignoring request"); return false; }
            return true;
        }

        [Serializable] private class Req { public string model; public string system; public string prompt; public bool stream; public Opt options; public string[] images; }
        [Serializable] private class Opt { public int num_predict; public float temperature; }
        [Serializable] private class Resp { public string response; public bool done; public string error; }

        /// <summary>
        /// The /api/generate body: the safety prompt and the persona as the <c>system</c> message, the user's text as the
        /// <c>prompt</c>, and the images (base64) for a vision request.
        /// </summary>
        public static string BuildRequestJson(LlmConfig cfg, string prompt, string[] images)
        {
            var req = new Req
            {
                model = cfg.model,
                system = cfg.BuildSystemMessage(),
                prompt = prompt ?? string.Empty,
                stream = false,
                options = new Opt { num_predict = cfg.maxTokens, temperature = cfg.temperature },
                images = images ?? new string[0]
            };
            return JsonUtility.ToJson(req);
        }

        private IEnumerator Send(string prompt, Texture2D image)
        {
            _processing = true;
            OnProcessingStarted.Invoke();

            string json = BuildRequestJson(config, prompt,
                image != null ? new[] { Convert.ToBase64String(image.EncodeToJPG(75)) } : null);
            using var web = new UnityWebRequest(_baseUrl + "/api/generate", "POST");
            web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            web.downloadHandler = new DownloadHandlerBuffer();
            web.SetRequestHeader("Content-Type", "application/json");
            web.timeout = Mathf.Max(1, (int)config.requestTimeoutSeconds);

            yield return web.SendWebRequest();
            _processing = false;

            if (web.result == UnityWebRequest.Result.Success)
            {
                var r = JsonUtility.FromJson<Resp>(web.downloadHandler.text);
                if (r != null && !string.IsNullOrEmpty(r.error)) { OnError.Invoke(r.error); yield break; }
                OnResponseReceived.Invoke(r != null && r.response != null ? r.response.Trim() : string.Empty);
            }
            else
            {
                OnError.Invoke("Ollama request failed: " + web.error);
            }
        }
    }
}
