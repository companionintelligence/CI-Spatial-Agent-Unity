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
    /// It is also the reply check (<see cref="IReplyChecker"/>): IBM Granite Guardian (<see cref="LlmConfig.SafetyModel"/>)
    /// on the same server judges each reply for every risk in <see cref="GraniteGuardian.Risks"/>, one request each, sent
    /// together; see <see cref="GraniteGuardian"/>.
    /// </summary>
    public class OllamaBackend : MonoBehaviour, ILlmBackend, IReplyChecker
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

        public string CheckerName => config != null ? config.SafetyModel : LlmConfig.DefaultSafetyModel;

        public float TimeoutSeconds =>
            config != null ? config.SafetyCheckTimeoutSeconds : ReplySafety.ClampTimeout(LlmConfig.DefaultSafetyCheckTimeoutSeconds);

        /// <summary>
        /// Asks the safety model about <paramref name="reply"/> once per risk, all at once, on the language model's server.
        /// The first Yes blocks the reply (the other requests are dropped); it is safe only when every answer is No.
        /// </summary>
        public void Check(string question, string reply, Action<ReplyCheckResult> done)
        {
            if (done == null) return;
            if (config == null || !_initialized || string.IsNullOrEmpty(_baseUrl))
            {
                done(ReplyCheckResult.Unavailable("the LLM server is not set up yet."));
                return;
            }
            StartCoroutine(CheckReply(question, reply, done));
        }

        private IEnumerator CheckReply(string question, string reply, Action<ReplyCheckResult> done)
        {
            string model = config.SafetyModel;
            int timeout = Mathf.CeilToInt(config.SafetyCheckTimeoutSeconds);
            var risks = GraniteGuardian.Risks;
            var requests = new UnityWebRequest[risks.Count];
            var results = new ReplyCheckResult[risks.Count];
            try
            {
                for (int i = 0; i < risks.Count; i++)
                {
                    string json = GraniteGuardian.BuildRequestJson(model, risks[i], question, reply);
                    var web = new UnityWebRequest(_baseUrl + GraniteGuardian.ChatPath, "POST");
                    web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    web.downloadHandler = new DownloadHandlerBuffer();
                    web.SetRequestHeader("Content-Type", "application/json");
                    web.timeout = timeout;
                    requests[i] = web;
                    web.SendWebRequest();
                }

                for (int open = risks.Count; open > 0;)
                {
                    yield return null;
                    open = 0;
                    for (int i = 0; i < risks.Count; i++)
                    {
                        if (results[i] != null) continue;
                        var web = requests[i];
                        if (!web.isDone)
                        {
                            open++;
                            continue;
                        }

                        results[i] = GraniteGuardian.ReadAnswer(model, risks[i], web.responseCode, web.downloadHandler?.text,
                            web.result == UnityWebRequest.Result.Success ? null : (web.error ?? "request failed"));
                        if (results[i].Verdict == ReplyVerdict.Blocked)
                        {
                            done(results[i]);
                            yield break;
                        }
                    }
                }
                done(ReplyCheckResult.Combine(results));
            }
            finally
            {
                foreach (var web in requests) web?.Dispose(); // also aborts the requests a Yes made unnecessary
            }
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
