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
            isProcessingRequests = true;
            
            while (isInitialized)
            {
                if (requestQueue.Count > 0 && isConnected)
                {
                    var request = requestQueue.Dequeue();
                    yield return StartCoroutine(ProcessSingleRequest(request));
                }
                
                yield return new WaitForSeconds(0.1f);
            }
            
            isProcessingRequests = false;
        }
        
        private IEnumerator ProcessSingleRequest(AIRequest request)
        {
            Debug.Log($"[OllamaClient] Processing {(request.isMultiModal ? "multi-modal" : "text")} request...");
            
            // Create Ollama request payload
            var ollamaRequest = new OllamaRequest
            {
                model = defaultModel,
                prompt = BuildPromptWithHistory(request.prompt),
                stream = streamResponse,
                options = new OllamaOptions
                {
                    num_predict = maxTokens,
                    temperature = temperature
                }
            };
            
            // Add image data if multi-modal
            if (request.isMultiModal && request.imageData != null)
            {
                string base64Image = System.Convert.ToBase64String(request.imageData);
                ollamaRequest.images = new string[] { base64Image };
            }
            
            // Convert to JSON
            string jsonData = JsonUtility.ToJson(ollamaRequest);
            
            using (UnityWebRequest webRequest = new UnityWebRequest($"{baseUrl}/api/generate", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.timeout = (int)requestTimeout;
                
                yield return webRequest.SendWebRequest();
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string responseText = webRequest.downloadHandler.text;
                    Debug.Log($"[OllamaClient] Response received: {responseText.Length} characters");
                    
                    // Parse response
                    string aiResponse = ParseOllamaResponse(responseText);
                    
                    if (!string.IsNullOrEmpty(aiResponse))
                    {
                        // Add to conversation history
                        AddToConversationHistory(request.prompt, aiResponse);
                        
                        OnResponseReceived?.Invoke(aiResponse);
                        request.callback?.Invoke(aiResponse);
                    }
                    else
                    {
                        Debug.LogWarning("[OllamaClient] Empty response from server");
                        request.callback?.Invoke(null);
                    }
                }
                else
                {
                    Debug.LogError($"[OllamaClient] Request failed: {webRequest.error}");
                    
                    // Retry logic
                    if (request.retryCount < maxRetries)
                    {
                        request.retryCount++;
                        Debug.Log($"[OllamaClient] Retrying request ({request.retryCount}/{maxRetries})...");
                        requestQueue.Enqueue(request);
                    }
                    else
                    {
                        Debug.LogError("[OllamaClient] Request failed after maximum retries");
                        request.callback?.Invoke(null);
                    }
                }
            }
        }
        
        private string BuildPromptWithHistory(string currentPrompt)
        {
            var promptBuilder = new StringBuilder();
            
            // Add system context
            promptBuilder.AppendLine("You are a helpful AI assistant integrated into XREAL smart glasses. You help users with daily tasks, answer questions, and analyze their environment. Keep responses concise and natural.");
            promptBuilder.AppendLine();
            
            // Add recent conversation history (last 5 exchanges)
            int historyCount = Mathf.Min(5, conversationHistory.Count);
            for (int i = conversationHistory.Count - historyCount; i < conversationHistory.Count; i++)
            {
                promptBuilder.AppendLine(conversationHistory[i]);
            }
            
            // Add current prompt
            promptBuilder.AppendLine($"Human: {currentPrompt}");
            promptBuilder.Append("Assistant: ");
            
            return promptBuilder.ToString();
        }
        
        private void AddToConversationHistory(string prompt, string response)
        {
            conversationHistory.Add($"Human: {prompt}");
            conversationHistory.Add($"Assistant: {response}");
            
            // Limit history size
            while (conversationHistory.Count > 20)
            {
                conversationHistory.RemoveAt(0);
            }
        }
        
        private string ParseOllamaResponse(string responseText)
        {
            try
            {
                // Handle streaming response (multiple JSON objects)
                if (streamResponse)
                {
                    var lines = responseText.Split('\n');
                    var responseBuilder = new StringBuilder();
                    
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            var response = JsonUtility.FromJson<OllamaResponse>(line);
                            if (response != null && !string.IsNullOrEmpty(response.response))
                            {
                                responseBuilder.Append(response.response);
                            }
                        }
                    }
                    
                    return responseBuilder.ToString();
                }
                else
                {
                    // Single response
                    var response = JsonUtility.FromJson<OllamaResponse>(responseText);
                    return response?.response ?? "";
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[OllamaClient] Error parsing response: {e.Message}");
                return null;
            }
        }
        
        public void ClearConversationHistory()
        {
            conversationHistory.Clear();
            Debug.Log("[OllamaClient] Conversation history cleared");
        }
        
        public List<string> GetConversationHistory()
        {
            return new List<string>(conversationHistory);
        }
        
        public void Cleanup()
        {
            Debug.Log("[OllamaClient] Cleaning up Ollama client...");
            
            isInitialized = false;
            isConnected = false;
            isProcessingRequests = false;
            
            // Clear any pending requests
            requestQueue.Clear();
        }
        
        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
