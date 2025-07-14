using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Handles communication with local Ollama server for AI processing.
    /// Supports both text-only and multi-modal (text + image) requests.
    /// </summary>
    public class OllamaClient : MonoBehaviour
    {
        [Header("Server Configuration")]
        [SerializeField] private string serverIP = "192.168.1.100";
        [SerializeField] private int serverPort = 11434;
        [SerializeField] private string defaultModel = "llama3.2-vision";
        [SerializeField] private float requestTimeout = 30.0f;
        
        [Header("Request Settings")]
        [SerializeField] private int maxTokens = 500;
        [SerializeField] private float temperature = 0.7f;
        [SerializeField] private bool streamResponse = false;
        [SerializeField] private int maxRetries = 3;
        
        // Connection state
        private bool isInitialized = false;
        private bool isConnected = false;
        private string baseUrl;
        
        // Request management
        private Queue<AIRequest> requestQueue = new Queue<AIRequest>();
        private bool isProcessingRequests = false;
        private List<string> conversationHistory = new List<string>();
        
        // Events
        public event System.Action<bool> OnConnectionStatusChanged;
        public event System.Action<string> OnResponseReceived;
        
        public bool IsInitialized => isInitialized;
        public bool IsConnected => isConnected;
        public string ServerAddress => $"{serverIP}:{serverPort}";
        
        [System.Serializable]
        public class AIRequest
        {
            public string prompt;
            public byte[] imageData;
            public System.Action<string> callback;
            public int retryCount;
            public bool isMultiModal;
            
            public AIRequest(string prompt, System.Action<string> callback)
            {
                this.prompt = prompt;
                this.callback = callback;
                this.imageData = null;
                this.retryCount = 0;
                this.isMultiModal = false;
            }
            
            public AIRequest(string prompt, byte[] imageData, System.Action<string> callback)
            {
                this.prompt = prompt;
                this.imageData = imageData;
                this.callback = callback;
                this.retryCount = 0;
                this.isMultiModal = true;
            }
        }
        
        [System.Serializable]
        public class OllamaRequest
        {
            public string model;
            public string prompt;
            public string[] images;
            public bool stream;
            public OllamaOptions options;
        }
        
        [System.Serializable]
        public class OllamaOptions
        {
            public int num_predict;
            public float temperature;
        }
        
        [System.Serializable]
        public class OllamaResponse
        {
            public string model;
            public string response;
            public bool done;
            public int[] context;
        }
        
        public void Initialize()
        {
            Debug.Log("[OllamaClient] Initializing Ollama client...");
            
            UpdateServerAddress(serverIP, serverPort);
            StartCoroutine(TestConnection());
        }
        
        public void UpdateServerAddress(string ip, int port)
        {
            serverIP = ip;
            serverPort = port;
            baseUrl = $"http://{serverIP}:{serverPort}";
            
            Debug.Log($"[OllamaClient] Server address updated: {baseUrl}");
        }
        
        private IEnumerator TestConnection()
        {
            Debug.Log($"[OllamaClient] Testing connection to {baseUrl}...");
            
            using (UnityWebRequest request = UnityWebRequest.Get($"{baseUrl}/api/tags"))
            {
                request.timeout = 5; // Short timeout for connection test
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("[OllamaClient] Connection successful!");
                    isConnected = true;
                    isInitialized = true;
                    
                    OnConnectionStatusChanged?.Invoke(true);
                    
                    // Start processing request queue
                    StartCoroutine(ProcessRequestQueue());
                }
                else
                {
                    Debug.LogWarning($"[OllamaClient] Connection failed: {request.error}");
                    isConnected = false;
                    OnConnectionStatusChanged?.Invoke(false);
                    
                    // Retry connection after delay
                    yield return new WaitForSeconds(5.0f);
                    StartCoroutine(TestConnection());
                }
            }
        }
        
        public void SendTextRequest(string prompt, System.Action<string> callback)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[OllamaClient] Cannot send request - client not initialized");
                callback?.Invoke(null);
                return;
            }
            
            var request = new AIRequest(prompt, callback);
            requestQueue.Enqueue(request);
            
            Debug.Log($"[OllamaClient] Text request queued: {prompt.Substring(0, Mathf.Min(50, prompt.Length))}...");
        }
        
        public IEnumerator SendMultiModalRequest(string prompt, byte[] imageData, System.Action<string> callback)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[OllamaClient] Cannot send request - client not initialized");
                callback?.Invoke(null);
                yield break;
            }
            
            var request = new AIRequest(prompt, imageData, callback);
            requestQueue.Enqueue(request);
            
            Debug.Log($"[OllamaClient] Multi-modal request queued: {prompt.Substring(0, Mathf.Min(50, prompt.Length))}... (Image: {imageData.Length} bytes)");
        }
        
        private IEnumerator ProcessRequestQueue()
        {
            while (isInitialized)
            {
                if (requestQueue.Count > 0 && !isProcessingRequests)
                {
                    isProcessingRequests = true;
                    var request = requestQueue.Dequeue();
                    
                    yield return StartCoroutine(ProcessSingleRequest(request));
                    
                    isProcessingRequests = false;
                }
                
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        private IEnumerator ProcessSingleRequest(AIRequest request)
        {
            if (request == null)
                yield break;
            
            int attempt = 0;
            bool success = false;
            
            while (!success && attempt < maxRetries)
            {
                attempt++;
                
                if (request.isMultiModal)
                {
                    yield return StartCoroutine(SendMultiModalRequestInternal(request));
                }
                else
                {
                    yield return StartCoroutine(SendTextRequestInternal(request));
                }
                
                // Check if request was successful (callback was called with non-null response)
                success = true; // Simplified - in real implementation, track success state
                
                if (!success && attempt < maxRetries)
                {
                    yield return new WaitForSeconds(2f); // Wait before retry
                }
            }
        }
        
        private IEnumerator SendTextRequestInternal(AIRequest request)
        {
            var ollamaRequest = new OllamaRequest
            {
                model = defaultModel,
                prompt = request.prompt,
                stream = streamResponse,
                options = new OllamaOptions
                {
                    num_predict = maxTokens,
                    temperature = temperature
                }
            };
            
            string jsonPayload = JsonUtility.ToJson(ollamaRequest);
            
            using (UnityWebRequest webRequest = new UnityWebRequest($"{baseUrl}/api/generate", "POST"))
            {
                byte[] jsonData = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
                webRequest.uploadHandler = new UploadHandlerRaw(jsonData);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.timeout = (int)requestTimeout;
                
                yield return webRequest.SendWebRequest();
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string responseText = webRequest.downloadHandler.text;
                    
                    try
                    {
                        var response = JsonUtility.FromJson<OllamaResponse>(responseText);
                        
                        if (response != null && !string.IsNullOrEmpty(response.response))
                        {
                            request.callback?.Invoke(response.response);
                            OnResponseReceived?.Invoke(response.response);
                        }
                        else
                        {
                            Debug.LogError("[OllamaClient] Empty or invalid response received");
                            request.callback?.Invoke(null);
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[OllamaClient] Error parsing response: {e.Message}");
                        request.callback?.Invoke(null);
                    }
                }
                else
                {
                    Debug.LogError($"[OllamaClient] Request failed: {webRequest.error}");
                    request.callback?.Invoke(null);
                }
            }
        }
        
        private IEnumerator SendMultiModalRequestInternal(AIRequest request)
        {
            // Convert byte array to base64 string for Ollama
            string base64Image = System.Convert.ToBase64String(request.imageData);
            
            var ollamaRequest = new OllamaRequest
            {
                model = defaultModel,
                prompt = request.prompt,
                images = new string[] { base64Image },
                stream = streamResponse,
                options = new OllamaOptions
                {
                    num_predict = maxTokens,
                    temperature = temperature
                }
            };
            
            string jsonPayload = JsonUtility.ToJson(ollamaRequest);
            
            using (UnityWebRequest webRequest = new UnityWebRequest($"{baseUrl}/api/generate", "POST"))
            {
                byte[] jsonData = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
                webRequest.uploadHandler = new UploadHandlerRaw(jsonData);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.timeout = (int)requestTimeout;
                
                yield return webRequest.SendWebRequest();
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string responseText = webRequest.downloadHandler.text;
                    
                    try
                    {
                        var response = JsonUtility.FromJson<OllamaResponse>(responseText);
                        
                        if (response != null && !string.IsNullOrEmpty(response.response))
                        {
                            request.callback?.Invoke(response.response);
                            OnResponseReceived?.Invoke(response.response);
                        }
                        else
                        {
                            Debug.LogError("[OllamaClient] Empty or invalid multimodal response received");
                            request.callback?.Invoke(null);
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[OllamaClient] Error parsing multimodal response: {e.Message}");
                        request.callback?.Invoke(null);
                    }
                }
                else
                {
                    Debug.LogError($"[OllamaClient] Multimodal request failed: {webRequest.error}");
                    request.callback?.Invoke(null);
                }
            }
        }
        
        // Public API for manual testing
        public void TestConnection()
        {
            StartCoroutine(TestConnectionCoroutine());
        }
        
        private IEnumerator TestConnectionCoroutine()
        {
            Debug.Log($"[OllamaClient] Testing connection to {baseUrl}...");
            
            using (UnityWebRequest request = UnityWebRequest.Get($"{baseUrl}/api/version"))
            {
                request.timeout = 5;
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[OllamaClient] Connection test successful: {request.downloadHandler.text}");
                }
                else
                {
                    Debug.LogError($"[OllamaClient] Connection test failed: {request.error}");
                }
            }
        }
        
        // Performance monitoring
        public int QueuedRequests => requestQueue.Count;
        public bool IsProcessingRequests => isProcessingRequests;
        public int ConversationLength => conversationHistory.Count;
        
        private void OnDestroy()
        {
            StopAllCoroutines();
            requestQueue.Clear();
            conversationHistory.Clear();
        }
    }
}
