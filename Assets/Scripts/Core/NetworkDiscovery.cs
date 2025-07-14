using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Discovers Ollama servers on the local network and manages connection configuration.
    /// Supports both automatic discovery and manual server configuration.
    /// </summary>
    public class NetworkDiscovery : MonoBehaviour
    {
        [Header("Discovery Configuration")]
        [SerializeField] private int discoveryPort = 11434;
        [SerializeField] private float discoveryTimeout = 10.0f;
        [SerializeField] private string[] commonServerIPs = {
            "192.168.1.100", "192.168.1.101", "192.168.1.102",
            "192.168.0.100", "192.168.0.101", "192.168.0.102",
            "10.0.0.100", "10.0.0.101", "10.0.0.102"
        };
        
        [Header("Manual Configuration")]
        [SerializeField] private bool allowManualConfiguration = true;
        [SerializeField] private string manualServerIP = "";
        [SerializeField] private int manualServerPort = 11434;
        
        // Discovery state
        private bool isDiscovering = false;
        private List<ServerInfo> discoveredServers = new List<ServerInfo>();
        private ServerInfo selectedServer = null;
        
        // Events
        public event System.Action<ServerInfo> OnServerDiscovered;
        public event System.Action<List<ServerInfo>> OnDiscoveryComplete;
        public event System.Action<ServerInfo> OnServerSelected;
        
        [System.Serializable]
        public class ServerInfo
        {
            public string ipAddress;
            public int port;
            public float responseTime;
            public bool isOnline;
            public string serverVersion;
            public List<string> availableModels;
            
            public ServerInfo(string ip, int port)
            {
                this.ipAddress = ip;
                this.port = port;
                this.responseTime = 0f;
                this.isOnline = false;
                this.serverVersion = "";
                this.availableModels = new List<string>();
            }
            
            public string GetAddress()
            {
                return $"{ipAddress}:{port}";
            }
        }
        
        public IEnumerator DiscoverOllamaServer()
        {
            Debug.Log("[NetworkDiscovery] Starting Ollama server discovery...");
            
            if (isDiscovering)
            {
                Debug.LogWarning("[NetworkDiscovery] Discovery already in progress");
                yield break;
            }
            
            isDiscovering = true;
            discoveredServers.Clear();
            
            // First, try manual configuration if provided
            if (allowManualConfiguration && !string.IsNullOrEmpty(manualServerIP))
            {
                Debug.Log($"[NetworkDiscovery] Testing manual server configuration: {manualServerIP}:{manualServerPort}");
                yield return StartCoroutine(TestServer(manualServerIP, manualServerPort));
            }
            
            // If no manual server found, start network discovery
            if (selectedServer == null)
            {
                yield return StartCoroutine(AutoDiscoverServers());
            }
            
            isDiscovering = false;
            
            // Report discovery results
            OnDiscoveryComplete?.Invoke(discoveredServers);
            
            if (selectedServer != null)
            {
                Debug.Log($"[NetworkDiscovery] Server selected: {selectedServer.GetAddress()}");
                OnServerSelected?.Invoke(selectedServer);
            }
            else
            {
                Debug.LogWarning("[NetworkDiscovery] No Ollama servers found on the network");
            }
        }
        
        private IEnumerator AutoDiscoverServers()
        {
            Debug.Log("[NetworkDiscovery] Starting automatic server discovery...");
            
            // Get local network information
            string localIP = GetLocalIPAddress();
            if (string.IsNullOrEmpty(localIP))
            {
                Debug.LogError("[NetworkDiscovery] Could not determine local IP address");
                yield break;
            }
            
            Debug.Log($"[NetworkDiscovery] Local IP: {localIP}");
            
            // Generate IP range based on local IP
            var ipRange = GenerateIPRange(localIP);
            
            // Add common server IPs to the range
            var allIPs = new List<string>(ipRange);
            allIPs.AddRange(commonServerIPs);
            
            // Remove duplicates and invalid IPs
            var uniqueIPs = new HashSet<string>(allIPs);
            uniqueIPs.Remove(localIP); // Don't test local device
            
            Debug.Log($"[NetworkDiscovery] Testing {uniqueIPs.Count} IP addresses...");
            
            // Test servers concurrently (in batches to avoid overwhelming the network)
            int batchSize = 5;
            var ipList = new List<string>(uniqueIPs);
            
            for (int i = 0; i < ipList.Count; i += batchSize)
            {
                var batch = new List<Coroutine>();
                
                for (int j = i; j < Mathf.Min(i + batchSize, ipList.Count); j++)
                {
                    batch.Add(StartCoroutine(TestServer(ipList[j], discoveryPort)));
                }
                
                // Wait for batch to complete
                foreach (var coroutine in batch)
                {
                    yield return coroutine;
                }
                
                // Small delay between batches
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        private IEnumerator TestServer(string ipAddress, int port)
        {
            var serverInfo = new ServerInfo(ipAddress, port);
            float startTime = Time.time;
            
            // Test HTTP connection to Ollama API
            string testUrl = $"http://{ipAddress}:{port}/api/tags";
            
            using (var request = UnityEngine.Networking.UnityWebRequest.Get(testUrl))
            {
                request.timeout = 3; // Short timeout for discovery
                
                yield return request.SendWebRequest();
                
                serverInfo.responseTime = Time.time - startTime;
                
                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    serverInfo.isOnline = true;
                    
                    // Parse server response to get available models
                    try
                    {
                        var responseText = request.downloadHandler.text;
                        ParseServerInfo(serverInfo, responseText);
                        
                        Debug.Log($"[NetworkDiscovery] Found Ollama server at {serverInfo.GetAddress()} (Response: {serverInfo.responseTime:F2}s)");
                        
                        discoveredServers.Add(serverInfo);
                        OnServerDiscovered?.Invoke(serverInfo);
                        
                        // Select the first responding server as default
                        if (selectedServer == null)
                        {
                            selectedServer = serverInfo;
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[NetworkDiscovery] Error parsing server info from {ipAddress}: {e.Message}");
                    }
                }
                else
                {
                    // Server not responding or not an Ollama server
                    Debug.Log($"[NetworkDiscovery] No Ollama server at {ipAddress}:{port} ({request.error})");
                }
            }
        }
        
        private void ParseServerInfo(ServerInfo serverInfo, string responseText)
        {
            try
            {
                // Parse the /api/tags response to get available models
                var response = JsonUtility.FromJson<OllamaTagsResponse>(responseText);
                if (response != null && response.models != null)
                {
                    foreach (var model in response.models)
                    {
                        if (!string.IsNullOrEmpty(model.name))
                        {
                            serverInfo.availableModels.Add(model.name);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkDiscovery] Error parsing models list: {e.Message}");
            }
        }
        
        [System.Serializable]
        public class OllamaTagsResponse
        {
            public OllamaModel[] models;
        }
        
        [System.Serializable]
        public class OllamaModel
        {
            public string name;
            public string modified_at;
            public long size;
        }
        
        private List<string> GenerateIPRange(string localIP)
        {
            var ipList = new List<string>();
            
            try
            {
                // Parse local IP to determine network range
                var parts = localIP.Split('.');
                if (parts.Length == 4)
                {
                    string networkBase = $"{parts[0]}.{parts[1]}.{parts[2]}";
                    
                    // Scan common IP range (100-200 to avoid most DHCP ranges)
                    for (int i = 100; i <= 200; i++)
                    {
                        ipList.Add($"{networkBase}.{i}");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkDiscovery] Error generating IP range: {e.Message}");
            }
            
            return ipList;
        }
        
        private string GetLocalIPAddress()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint.Address.ToString();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkDiscovery] Error getting local IP: {e.Message}");
                return "";
            }
        }
        
        public void SetManualServer(string ipAddress, int port)
        {
            manualServerIP = ipAddress;
            manualServerPort = port;
            
            Debug.Log($"[NetworkDiscovery] Manual server configuration set: {ipAddress}:{port}");
        }
        
        public void SelectServer(ServerInfo server)
        {
            if (server != null && server.isOnline)
            {
                selectedServer = server;
                OnServerSelected?.Invoke(selectedServer);
                
                Debug.Log($"[NetworkDiscovery] Server manually selected: {server.GetAddress()}");
            }
        }
        
        public ServerInfo GetSelectedServer()
        {
            return selectedServer;
        }
        
        public List<ServerInfo> GetDiscoveredServers()
        {
            return new List<ServerInfo>(discoveredServers);
        }
        
        public bool IsDiscovering()
        {
            return isDiscovering;
        }
        
        public void StopDiscovery()
        {
            Debug.Log("[NetworkDiscovery] Stopping server discovery...");
            isDiscovering = false;
        }
    }
}
