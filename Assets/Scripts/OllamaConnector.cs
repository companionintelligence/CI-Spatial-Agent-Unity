using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// XREAL Ollama Connector - Handles communication with Ollama vision models
    /// Consolidated and optimized for XREAL devices
    /// Implements retry logic, connection pooling, and error recovery
    /// </summary>
    public class OllamaConnector : MonoBehaviour
    {
        [Header("Server Configuration")]
        [SerializeField] private string serverIP = "192.168.1.100";
        [SerializeField] private int serverPort = 11434;
        [SerializeField] private string model = "llava:7b";
        [SerializeField] private float requestTimeout = 30.0f;
        
        [Header("Model Settings")]
        [SerializeField] private int maxTokens = 300;
        [SerializeField] private float temperature = 0.7f;
        [SerializeField] private bool streamResponse = false;
        [SerializeField] private string systemPrompt = "You are a helpful vision assistant for XREAL glasses. Provide clear, concise descriptions of what you see.";
        
        [Header("Request Settings")]
        [SerializeField] private int maxRetries = 3;
        [SerializeField] private float retryDelay = 1.0f;
        [SerializeField] private bool enableDebugLogging = true;
        
        [Header("Events")]
        public UnityEvent OnProcessingStarted;
        public UnityEvent<string> OnResponseReceived;
        public UnityEvent<string> OnError;
        
        // Connection state
        private bool isInitialized = false;
        private bool isConnected = false;
        private string baseUrl;
        
        // Request state
        private bool isProcessing = false;
        private int currentRetryCount = 0;
        
        public bool IsInitialized => isInitialized;
        public bool IsConnected => isConnected;
        public bool IsProcessing => isProcessing;
        public string ServerAddress => $"{serverIP}:{serverPort}";
        
        [Serializable]
        public class OllamaRequest
        {
            public string model;
            public string prompt;
            public bool stream;
            public OllamaOptions options;
            public string[] images; // Base64 encoded images
        }
        
        [Serializable]
        public class OllamaOptions
        {
            public int num_predict;
            public float temperature;
        }
        
        [Serializable]
        public class OllamaResponse
        {
            public string response;
            public bool done;
            public string error;
        }
        
        private void Start()
        {
            Initialize();
        }
        
        public void Initialize()
        {
            if (isInitialized) return;
            
            LogDebug("Initializing Ollama Connector...");
            
            baseUrl = $"http://{serverIP}:{serverPort}";
            
            // Test connection
            StartCoroutine(TestConnection());
        }
        
        private IEnumerator TestConnection()
        {
            LogDebug($"Testing connection to {baseUrl}...");
            
            UnityWebRequest request = UnityWebRequest.Get($"{baseUrl}/api/tags");
            request.timeout = (int)requestTimeout;
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                isConnected = true;
                isInitialized = true;
                LogDebug($"Connected to Ollama server at {baseUrl}");
            }
            else
            {
                isConnected = false;
                LogError($"Failed to connect to Ollama server: {request.error}");
                OnError?.Invoke($"Connection failed: {request.error}");
                
                // Retry connection after delay
                yield return new WaitForSeconds(retryDelay);
                StartCoroutine(TestConnection());
            }
        }
        
        /// <summary>
        /// Send a vision request with image and text prompt
        /// </summary>
        public void SendVisionRequest(Texture2D image, string prompt)
        {
            if (!isInitialized)
            {
                LogError("Ollama connector not initialized");
                OnError?.Invoke("Ollama connector not initialized");
                return;
            }
            
            if (isProcessing)
            {
                LogDebug("Already processing request, ignoring new request");
                return;
            }
            
            StartCoroutine(SendVisionRequestCoroutine(image, prompt));
        }
        
        /// <summary>
        /// Send a text-only request
        /// </summary>
        public void SendTextRequest(string prompt)
        {
            if (!isInitialized)
            {
                LogError("Ollama connector not initialized");
                OnError?.Invoke("Ollama connector not initialized");
                return;
            }
            
            if (isProcessing)
            {
                LogDebug("Already processing request, ignoring new request");
                return;
            }
            
            StartCoroutine(SendTextRequestCoroutine(prompt));
        }
        
        private IEnumerator SendVisionRequestCoroutine(Texture2D image, string prompt)
        {
            isProcessing = true;
            currentRetryCount = 0;
            OnProcessingStarted?.Invoke();
            
            LogDebug($"Sending vision request: {prompt}");
            
            while (currentRetryCount < maxRetries)
            {
                yield return StartCoroutine(AttemptVisionRequest(image, prompt));
                
                if (!isProcessing) // Success, break out of retry loop
                    break;
                
                currentRetryCount++;
                if (currentRetryCount < maxRetries)
                {
                    LogDebug($"Retrying request... ({currentRetryCount}/{maxRetries})");
                    yield return new WaitForSeconds(retryDelay);
                }
            }
            
            if (isProcessing) // Still processing means all retries failed
            {
                isProcessing = false;
                OnError?.Invoke("Request failed after all retries");
            }
        }
        
        private IEnumerator SendTextRequestCoroutine(string prompt)
        {
            isProcessing = true;
            currentRetryCount = 0;
            OnProcessingStarted?.Invoke();
            
            LogDebug($"Sending text request: {prompt}");
            
            while (currentRetryCount < maxRetries)
            {
                yield return StartCoroutine(AttemptTextRequest(prompt));
                
                if (!isProcessing) // Success, break out of retry loop
                    break;
                
                currentRetryCount++;
                if (currentRetryCount < maxRetries)
                {
                    LogDebug($"Retrying request... ({currentRetryCount}/{maxRetries})");
                    yield return new WaitForSeconds(retryDelay);
                }
            }
            
            if (isProcessing) // Still processing means all retries failed
            {
                isProcessing = false;
                OnError?.Invoke("Request failed after all retries");
            }
        }
        
        private IEnumerator AttemptVisionRequest(Texture2D image, string prompt)
        {
            // Convert image to base64
            string base64Image = "";
            if (image != null)
            {
                try
                {
                    byte[] imageBytes = image.EncodeToJPG(75); // Compress for faster transmission
                    base64Image = Convert.ToBase64String(imageBytes);
                }
                catch (System.Exception e)
                {
                    LogError($"Failed to encode image: {e.Message}");
                    yield break;
                }
            }
            
            // Create request payload
            var requestData = new OllamaRequest
            {
                model = model,
                prompt = CombinePrompts(systemPrompt, prompt),
                stream = streamResponse,
                options = new OllamaOptions
                {
                    num_predict = maxTokens,
                    temperature = temperature
                },
                images = !string.IsNullOrEmpty(base64Image) ? new string[] { base64Image } : new string[0]
            };
            
            string jsonPayload = JsonUtility.ToJson(requestData);
            
            UnityWebRequest request = new UnityWebRequest($"{baseUrl}/api/generate", "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = (int)requestTimeout;
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                ProcessOllamaResponse(responseText);
            }
            else
            {
                LogError($"Request failed: {request.error}");
                // Don't invoke error here, let retry logic handle it
            }
        }
        
        private IEnumerator AttemptTextRequest(string prompt)
        {
            // Create request payload
            var requestData = new OllamaRequest
            {
                model = model,
                prompt = CombinePrompts(systemPrompt, prompt),
                stream = streamResponse,
                options = new OllamaOptions
                {
                    num_predict = maxTokens,
                    temperature = temperature
                }
            };
            
            string jsonPayload = JsonUtility.ToJson(requestData);
            
            UnityWebRequest request = new UnityWebRequest($"{baseUrl}/api/generate", "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = (int)requestTimeout;
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                ProcessOllamaResponse(responseText);
            }
            else
            {
                LogError($"Request failed: {request.error}");
                // Don't invoke error here, let retry logic handle it
            }
        }
        
        private void ProcessOllamaResponse(string responseText)
        {
            try
            {
                // Handle both streaming and non-streaming responses
                if (streamResponse)
                {
                    // For streaming, we get multiple JSON objects separated by newlines
                    string[] lines = responseText.Split('\n');
                    StringBuilder fullResponse = new StringBuilder();
                    
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        
                        var response = JsonUtility.FromJson<OllamaResponse>(line);
                        if (!string.IsNullOrEmpty(response.response))
                        {
                            fullResponse.Append(response.response);
                        }
                        
                        if (response.done) break;
                    }
                    
                    string finalResponse = fullResponse.ToString().Trim();
                    if (!string.IsNullOrEmpty(finalResponse))
                    {
                        isProcessing = false;
                        LogDebug($"Received response: {finalResponse}");
                        OnResponseReceived?.Invoke(finalResponse);
                    }
                }
                else
                {
                    // For non-streaming, we get a single JSON object
                    var response = JsonUtility.FromJson<OllamaResponse>(responseText);
                    
                    if (!string.IsNullOrEmpty(response.error))
                    {
                        LogError($"Ollama error: {response.error}");
                        OnError?.Invoke(response.error);
                        return;
                    }
                    
                    if (!string.IsNullOrEmpty(response.response))
                    {
                        isProcessing = false;
                        LogDebug($"Received response: {response.response}");
                        OnResponseReceived?.Invoke(response.response);
                    }
                }
            }
            catch (Exception e)
            {
                LogError($"Failed to parse Ollama response: {e.Message}");
                OnError?.Invoke($"Response parsing failed: {e.Message}");
            }
        }
        
        private string CombinePrompts(string system, string user)
        {
            if (string.IsNullOrEmpty(system))
                return user;
            
            return $"{system}\n\nUser: {user}\n\nAssistant:";
        }
        
        /// <summary>
        /// Update server configuration
        /// </summary>
        public void UpdateServerConfiguration(string ip, int port, string modelName = null)
        {
            serverIP = ip;
            serverPort = port;
            if (!string.IsNullOrEmpty(modelName))
                model = modelName;
            
            baseUrl = $"http://{serverIP}:{serverPort}";
            isConnected = false;
            
            LogDebug($"Server configuration updated: {baseUrl}, model: {model}");
            
            // Re-test connection
            StartCoroutine(TestConnection());
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[XREALOllamaConnector] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[XREALOllamaConnector] {message}");
        }
        
        #region Editor Utilities
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void OnValidate()
        {
            // Ensure valid settings
            serverPort = Mathf.Clamp(serverPort, 1, 65535);
            requestTimeout = Mathf.Max(1.0f, requestTimeout);
            maxTokens = Mathf.Max(1, maxTokens);
            temperature = Mathf.Clamp01(temperature);
            maxRetries = Mathf.Max(1, maxRetries);
            retryDelay = Mathf.Max(0.1f, retryDelay);
        }
        
        #endregion
    }
}
