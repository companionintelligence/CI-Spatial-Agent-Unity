using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Object Detection Manager for XREAL devices using AI-powered visual analysis.
    /// Handles continuous frame analysis, object tracking, and semantic understanding.
    /// 
    /// Tasks Implemented: 5.1-5.10 (Object Detection Pipeline)
    /// </summary>
    public class ObjectDetectionManager : MonoBehaviour
    {
        [Header("Detection Configuration")]
        [SerializeField] private bool enableContinuousDetection = true;
        [SerializeField] private float detectionInterval = 2f;
        [SerializeField] private float minimumConfidence = 0.6f;
        [SerializeField] private int maxFrameQueueSize = 10;
        [SerializeField] private bool enableObjectTracking = true;
        
        [Header("Analysis Settings")]
        [SerializeField] private int maxObjectsPerFrame = 20;
        [SerializeField] private bool enableSemanticLabeling = true;
        [SerializeField] private bool enableInteractionHistory = true;
        [SerializeField] private float objectPersistenceTime = 30f;
        
        [Header("Performance Optimization")]
        [SerializeField] private bool enableFrameSkipping = true;
        [SerializeField] private int frameSkipCount = 3;
        [SerializeField] private bool enableAdaptiveAnalysis = true;
        [SerializeField] private float maxAnalysisTime = 5f;
        
        [Header("Database Settings")]
        [SerializeField] private bool enableObjectDatabase = true;
        [SerializeField] private int maxDatabaseEntries = 1000;
        [SerializeField] private bool saveUnknownObjects = true;
        [SerializeField] private string databasePath = "object_database.json";
        
        [Header("Debugging")]
        [SerializeField] private bool enableDebugLogging = true;
        [SerializeField] private bool visualizeDetections = false;
        [SerializeField] private bool saveDebugFrames = false;
        
        // Events
        public event Action<List<DetectedObject>> OnObjectsDetected;
        public event Action<DetectedObject> OnNewObjectFound;
        public event Action<DetectedObject> OnObjectLost;
        public event Action<DetectedObject> OnObjectInteraction;
        public event Action<string> OnDetectionError;
        
        // Core Components
        private CameraManager cameraManager;
        private OllamaClient ollamaClient;
        
        // Frame Processing
        private Queue<FrameData> frameQueue;
        private bool isProcessing = false;
        private int currentFrameCount = 0;
        private int processedFrameCount = 0;
        
        // Object Tracking
        private List<DetectedObject> currentObjects;
        private List<DetectedObject> previousObjects;
        private Dictionary<string, ObjectEntry> objectDatabase;
        private Dictionary<string, InteractionHistory> interactionHistories;
        
        // Performance Metrics
        private float averageAnalysisTime = 0f;
        private int totalAnalyses = 0;
        private float detectionAccuracy = 0f;
        
        [System.Serializable]
        public class DetectedObject
        {
            public string id;
            public string name;
            public string category;
            public float confidence;
            public Vector2 position; // Normalized screen coordinates (0-1)
            public Vector2 size; // Normalized width/height
            public string description;
            public DateTime detectionTime;
            public float persistenceScore;
            public List<string> attributes;
            public Color debugColor;
            
            public DetectedObject()
            {
                id = System.Guid.NewGuid().ToString();
                attributes = new List<string>();
                detectionTime = DateTime.Now;
                debugColor = UnityEngine.Random.ColorHSV(0f, 1f, 1f, 1f, 0.8f, 1f);
            }
        }
        
        [System.Serializable]
        public class ObjectEntry
        {
            public string name;
            public string category;
            public string description;
            public int detectionCount;
            public DateTime firstSeen;
            public DateTime lastSeen;
            public float averageConfidence;
            public List<string> commonAttributes;
            
            public ObjectEntry()
            {
                commonAttributes = new List<string>();
                firstSeen = DateTime.Now;
                lastSeen = DateTime.Now;
            }
        }
        
        [System.Serializable]
        public class InteractionHistory
        {
            public string objectId;
            public List<InteractionEvent> events;
            
            public InteractionHistory()
            {
                events = new List<InteractionEvent>();
            }
        }
        
        [System.Serializable]
        public class InteractionEvent
        {
            public DateTime timestamp;
            public string interactionType;
            public string description;
            public float duration;
        }
        
        private class FrameData
        {
            public byte[] imageData;
            public DateTime timestamp;
            public int frameNumber;
        }
        
        private void Awake()
        {
            InitializeObjectDetection();
        }
        
        private void Start()
        {
            StartCoroutine(InitializeDetectionSystem());
        }
        
        private void InitializeObjectDetection()
        {
            if (enableDebugLogging)
                Debug.Log("[ObjectDetection] Initializing Object Detection Manager");
            
            // Initialize collections
            frameQueue = new Queue<FrameData>();
            currentObjects = new List<DetectedObject>();
            previousObjects = new List<DetectedObject>();
            objectDatabase = new Dictionary<string, ObjectEntry>();
            interactionHistories = new Dictionary<string, InteractionHistory>();
            
            // Find required components
            cameraManager = FindObjectOfType<CameraManager>();
            if (cameraManager == null)
            {
                Debug.LogError("[ObjectDetection] CameraManager not found! Object detection disabled.");
                enabled = false;
                return;
            }
            
            ollamaClient = FindObjectOfType<OllamaClient>();
            if (ollamaClient == null)
            {
                Debug.LogError("[ObjectDetection] OllamaClient not found! Object detection disabled.");
                enabled = false;
                return;
            }
            
            // Load object database
            if (enableObjectDatabase)
            {
                LoadObjectDatabase();
            }
            
            if (enableDebugLogging)
                Debug.Log("[ObjectDetection] Object Detection Manager initialized");
        }
        
        private IEnumerator InitializeDetectionSystem()
        {
            // Wait for other systems to initialize
            yield return new WaitForSeconds(1f);
            
            if (enableDebugLogging)
                Debug.Log("[ObjectDetection] Starting detection system...");
            
            // Subscribe to camera events
            if (cameraManager != null)
            {
                cameraManager.OnFrameCaptured += HandleFrameCaptured;
            }
            
            // Start continuous detection if enabled
            if (enableContinuousDetection)
            {
                StartCoroutine(ContinuousDetectionLoop());
            }
            
            // Start frame processing
            StartCoroutine(ProcessFrameQueue());
        }
        
        private void HandleFrameCaptured(byte[] frameData)
        {
            if (!enableContinuousDetection || frameQueue.Count >= maxFrameQueueSize)
                return;
            
            currentFrameCount++;
            
            // Apply frame skipping if enabled
            if (enableFrameSkipping && currentFrameCount % (frameSkipCount + 1) != 0)
                return;
            
            // Add frame to processing queue
            FrameData frame = new FrameData
            {
                imageData = frameData,
                timestamp = DateTime.Now,
                frameNumber = currentFrameCount
            };
            
            frameQueue.Enqueue(frame);
            
            if (enableDebugLogging && frameQueue.Count > 5)
                Debug.LogWarning($"[ObjectDetection] Frame queue building up: {frameQueue.Count} frames");
        }
        
        private IEnumerator ContinuousDetectionLoop()
        {
            while (enableContinuousDetection)
            {
                // Adaptive detection interval based on processing load
                float currentInterval = detectionInterval;
                if (enableAdaptiveAnalysis)
                {
                    float queueRatio = (float)frameQueue.Count / maxFrameQueueSize;
                    currentInterval = detectionInterval * (1f + queueRatio);
                }
                
                yield return new WaitForSeconds(currentInterval);
                
                // Update object persistence
                UpdateObjectPersistence();
            }
        }
        
        private IEnumerator ProcessFrameQueue()
        {
            while (true)
            {
                if (frameQueue.Count > 0 && !isProcessing && ollamaClient != null && ollamaClient.IsConnected)
                {
                    FrameData frame = frameQueue.Dequeue();
                    StartCoroutine(AnalyzeFrame(frame));
                }
                
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        private IEnumerator AnalyzeFrame(FrameData frame)
        {
            if (isProcessing)
                yield break;
            
            isProcessing = true;
            float startTime = Time.time;
            
            try
            {
                if (enableDebugLogging)
                    Debug.Log($"[ObjectDetection] Analyzing frame {frame.frameNumber}");
                
                // Build analysis prompt
                string prompt = BuildAnalysisPrompt();
                
                List<DetectedObject> detectedObjects = new List<DetectedObject>();
                bool analysisComplete = false;
                
                // Send frame for AI analysis
                yield return StartCoroutine(ollamaClient.SendMultiModalRequest(prompt, frame.imageData, (response) =>
                {
                    if (!string.IsNullOrEmpty(response))
                    {
                        detectedObjects = ParseDetectionResponse(response, frame.timestamp);
                        
                        if (enableDebugLogging)
                            Debug.Log($"[ObjectDetection] Detected {detectedObjects.Count} objects");
                    }
                    analysisComplete = true;
                }));
                
                // Wait for analysis with timeout
                float timeout = maxAnalysisTime;
                float elapsed = 0f;
                while (!analysisComplete && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                
                if (!analysisComplete)
                {
                    if (enableDebugLogging)
                        Debug.LogWarning("[ObjectDetection] Frame analysis timeout");
                    OnDetectionError?.Invoke("Analysis timeout");
                }
                else
                {
                    // Process detected objects
                    ProcessDetectedObjects(detectedObjects);
                    
                    // Update metrics
                    float analysisTime = Time.time - startTime;
                    UpdatePerformanceMetrics(analysisTime, detectedObjects.Count);
                    
                    processedFrameCount++;
                }
                
                // Save debug frame if enabled
                if (saveDebugFrames && detectedObjects.Count > 0)
                {
                    SaveDebugFrame(frame, detectedObjects);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ObjectDetection] Error analyzing frame: {e.Message}");
                OnDetectionError?.Invoke($"Analysis error: {e.Message}");
            }
            finally
            {
                isProcessing = false;
            }
        }
        
        private string BuildAnalysisPrompt()
        {
            StringBuilder prompt = new StringBuilder();
            
            prompt.AppendLine("Analyze this image and detect all visible objects. For each object, provide:");
            prompt.AppendLine("1. Object name/type");
            prompt.AppendLine("2. Category (person, furniture, electronics, food, vehicle, etc.)");
            prompt.AppendLine("3. Confidence level (0.0-1.0)");
            prompt.AppendLine("4. Approximate position (left/center/right, top/middle/bottom)");
            prompt.AppendLine("5. Brief description");
            prompt.AppendLine("6. Notable attributes or properties");
            prompt.AppendLine("");
            prompt.AppendLine("Format your response as JSON array with this structure:");
            prompt.AppendLine("[{\"name\":\"object_name\", \"category\":\"category\", \"confidence\":0.9, \"position\":\"center_middle\", \"description\":\"brief description\", \"attributes\":[\"attribute1\", \"attribute2\"]}]");
            prompt.AppendLine("");
            prompt.AppendLine("Only include objects you can clearly identify with reasonable confidence.");
            prompt.AppendLine($"Maximum {maxObjectsPerFrame} objects per response.");
            
            return prompt.ToString();
        }
        
        private List<DetectedObject> ParseDetectionResponse(string response, DateTime timestamp)
        {
            List<DetectedObject> objects = new List<DetectedObject>();
            
            try
            {
                // Extract JSON from response (handle markdown formatting)
                string jsonString = ExtractJsonFromResponse(response);
                
                if (string.IsNullOrEmpty(jsonString))
                {
                    if (enableDebugLogging)
                        Debug.LogWarning("[ObjectDetection] No valid JSON found in response");
                    return objects;
                }
                
                // Parse JSON array
                var detectionResults = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(jsonString);
                
                foreach (var result in detectionResults)
                {
                    if (result.ContainsKey("name") && result.ContainsKey("confidence"))
                    {
                        DetectedObject obj = new DetectedObject();
                        obj.name = result["name"].ToString();
                        obj.category = result.ContainsKey("category") ? result["category"].ToString() : "unknown";
                        obj.confidence = Convert.ToSingle(result["confidence"]);
                        obj.description = result.ContainsKey("description") ? result["description"].ToString() : "";
                        obj.detectionTime = timestamp;
                        
                        // Parse position
                        if (result.ContainsKey("position"))
                        {
                            obj.position = ParsePosition(result["position"].ToString());
                        }
                        
                        // Parse attributes
                        if (result.ContainsKey("attributes") && result["attributes"] is Newtonsoft.Json.Linq.JArray attributes)
                        {
                            obj.attributes = attributes.Select(a => a.ToString()).ToList();
                        }
                        
                        // Filter by confidence threshold
                        if (obj.confidence >= minimumConfidence)
                        {
                            objects.Add(obj);
                        }
                    }
                }
                
                if (enableDebugLogging)
                    Debug.Log($"[ObjectDetection] Parsed {objects.Count} valid objects from response");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ObjectDetection] Error parsing detection response: {e.Message}");
                if (enableDebugLogging)
                    Debug.LogError($"[ObjectDetection] Raw response: {response}");
            }
            
            return objects;
        }
        
        private string ExtractJsonFromResponse(string response)
        {
            // Handle markdown code blocks
            int jsonStart = response.IndexOf('[');
            int jsonEnd = response.LastIndexOf(']');
            
            if (jsonStart >= 0 && jsonEnd >= 0 && jsonEnd > jsonStart)
            {
                return response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            }
            
            return "";
        }
        
        private Vector2 ParsePosition(string positionStr)
        {
            // Parse position strings like "center_middle", "left_top", etc.
            float x = 0.5f, y = 0.5f; // Default to center
            
            if (positionStr.Contains("left")) x = 0.25f;
            else if (positionStr.Contains("right")) x = 0.75f;
            
            if (positionStr.Contains("top")) y = 0.75f;
            else if (positionStr.Contains("bottom")) y = 0.25f;
            
            return new Vector2(x, y);
        }
        
        private void ProcessDetectedObjects(List<DetectedObject> newObjects)
        {
            // Store previous frame objects
            previousObjects = new List<DetectedObject>(currentObjects);
            currentObjects = newObjects;
            
            // Update object database
            if (enableObjectDatabase)
            {
                UpdateObjectDatabase(newObjects);
            }
            
            // Track object persistence and changes
            if (enableObjectTracking)
            {
                TrackObjectChanges(newObjects);
            }
            
            // Add semantic labeling
            if (enableSemanticLabeling)
            {
                EnhanceSemanticLabeling(newObjects);
            }
            
            // Trigger events
            OnObjectsDetected?.Invoke(newObjects);
            
            // Check for new objects
            foreach (var obj in newObjects)
            {
                if (!previousObjects.Any(p => IsSameObject(p, obj)))
                {
                    OnNewObjectFound?.Invoke(obj);
                    
                    if (enableDebugLogging)
                        Debug.Log($"[ObjectDetection] New object found: {obj.name} (confidence: {obj.confidence:F2})");
                }
            }
            
            // Check for lost objects
            foreach (var prevObj in previousObjects)
            {
                if (!newObjects.Any(n => IsSameObject(n, prevObj)))
                {
                    OnObjectLost?.Invoke(prevObj);
                    
                    if (enableDebugLogging)
                        Debug.Log($"[ObjectDetection] Object lost: {prevObj.name}");
                }
            }
        }
        
        private bool IsSameObject(DetectedObject obj1, DetectedObject obj2)
        {
            // Simple similarity check based on name and position
            if (obj1.name.ToLower() == obj2.name.ToLower())
            {
                float distance = Vector2.Distance(obj1.position, obj2.position);
                return distance < 0.2f; // Threshold for position similarity
            }
            return false;
        }
        
        private void UpdateObjectDatabase(List<DetectedObject> objects)
        {
            foreach (var obj in objects)
            {
                string key = obj.name.ToLower();
                
                if (objectDatabase.ContainsKey(key))
                {
                    ObjectEntry entry = objectDatabase[key];
                    entry.detectionCount++;
                    entry.lastSeen = obj.detectionTime;
                    entry.averageConfidence = (entry.averageConfidence * (entry.detectionCount - 1) + obj.confidence) / entry.detectionCount;
                    
                    // Merge attributes
                    foreach (var attr in obj.attributes)
                    {
                        if (!entry.commonAttributes.Contains(attr))
                        {
                            entry.commonAttributes.Add(attr);
                        }
                    }
                }
                else if (saveUnknownObjects)
                {
                    ObjectEntry newEntry = new ObjectEntry
                    {
                        name = obj.name,
                        category = obj.category,
                        description = obj.description,
                        detectionCount = 1,
                        averageConfidence = obj.confidence,
                        commonAttributes = new List<string>(obj.attributes)
                    };
                    
                    objectDatabase[key] = newEntry;
                    
                    if (enableDebugLogging)
                        Debug.Log($"[ObjectDetection] Added new object to database: {obj.name}");
                }
            }
            
            // Cleanup old entries if database is too large
            if (objectDatabase.Count > maxDatabaseEntries)
            {
                CleanupObjectDatabase();
            }
        }
        
        private void TrackObjectChanges(List<DetectedObject> objects)
        {
            foreach (var obj in objects)
            {
                // Update persistence score based on consecutive detections
                var prevObj = previousObjects.FirstOrDefault(p => IsSameObject(p, obj));
                if (prevObj != null)
                {
                    obj.persistenceScore = Mathf.Min(prevObj.persistenceScore + 0.1f, 1.0f);
                }
                else
                {
                    obj.persistenceScore = 0.1f;
                }
            }
        }
        
        private void EnhanceSemanticLabeling(List<DetectedObject> objects)
        {
            // Add semantic context based on object combinations
            foreach (var obj in objects)
            {
                if (obj.category == "person")
                {
                    // Check for person-related objects
                    var nearbyObjects = objects.Where(o => o != obj && Vector2.Distance(o.position, obj.position) < 0.3f);
                    foreach (var nearby in nearbyObjects)
                    {
                        if (nearby.category == "electronics" && nearby.name.Contains("phone"))
                        {
                            obj.attributes.Add("using_phone");
                        }
                        else if (nearby.category == "food")
                        {
                            obj.attributes.Add("eating");
                        }
                    }
                }
            }
        }
        
        private void UpdateObjectPersistence()
        {
            // Remove objects that haven't been seen for too long
            currentObjects.RemoveAll(obj => 
                (DateTime.Now - obj.detectionTime).TotalSeconds > objectPersistenceTime);
        }
        
        private void UpdatePerformanceMetrics(float analysisTime, int objectCount)
        {
            totalAnalyses++;
            averageAnalysisTime = (averageAnalysisTime * (totalAnalyses - 1) + analysisTime) / totalAnalyses;
            
            if (enableDebugLogging && totalAnalyses % 10 == 0)
            {
                Debug.Log($"[ObjectDetection] Performance - Avg Time: {averageAnalysisTime:F2}s, Total Objects: {GetTotalObjectCount()}");
            }
        }
        
        private void CleanupObjectDatabase()
        {
            // Remove least frequently detected objects
            var sortedEntries = objectDatabase.OrderBy(kvp => kvp.Value.detectionCount).ToList();
            int toRemove = objectDatabase.Count - maxDatabaseEntries + 100; // Remove extra for buffer
            
            for (int i = 0; i < toRemove && i < sortedEntries.Count; i++)
            {
                objectDatabase.Remove(sortedEntries[i].Key);
            }
            
            if (enableDebugLogging)
                Debug.Log($"[ObjectDetection] Cleaned up object database, removed {toRemove} entries");
        }
        
        private void LoadObjectDatabase()
        {
            try
            {
                string fullPath = System.IO.Path.Combine(Application.persistentDataPath, databasePath);
                if (System.IO.File.Exists(fullPath))
                {
                    string json = System.IO.File.ReadAllText(fullPath);
                    objectDatabase = JsonConvert.DeserializeObject<Dictionary<string, ObjectEntry>>(json);
                    
                    if (enableDebugLogging)
                        Debug.Log($"[ObjectDetection] Loaded object database with {objectDatabase.Count} entries");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ObjectDetection] Error loading object database: {e.Message}");
                objectDatabase = new Dictionary<string, ObjectEntry>();
            }
        }
        
        private void SaveObjectDatabase()
        {
            if (!enableObjectDatabase) return;
            
            try
            {
                string fullPath = System.IO.Path.Combine(Application.persistentDataPath, databasePath);
                string json = JsonConvert.SerializeObject(objectDatabase, Formatting.Indented);
                System.IO.File.WriteAllText(fullPath, json);
                
                if (enableDebugLogging)
                    Debug.Log($"[ObjectDetection] Saved object database with {objectDatabase.Count} entries");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ObjectDetection] Error saving object database: {e.Message}");
            }
        }
        
        private void SaveDebugFrame(FrameData frame, List<DetectedObject> objects)
        {
            // TODO: Implement debug frame saving with object annotations
            if (enableDebugLogging)
                Debug.Log($"[ObjectDetection] Debug frame saved: Frame {frame.frameNumber} with {objects.Count} objects");
        }
        
        // Public API
        public void StartDetection()
        {
            enableContinuousDetection = true;
            if (enableDebugLogging)
                Debug.Log("[ObjectDetection] Detection started");
        }
        
        public void StopDetection()
        {
            enableContinuousDetection = false;
            if (enableDebugLogging)
                Debug.Log("[ObjectDetection] Detection stopped");
        }
        
        public void AnalyzeCurrentFrame()
        {
            if (cameraManager != null && cameraManager.IsInitialized)
            {
                byte[] frameData = cameraManager.CaptureCurrentFrame();
                if (frameData != null)
                {
                    FrameData frame = new FrameData
                    {
                        imageData = frameData,
                        timestamp = DateTime.Now,
                        frameNumber = currentFrameCount++
                    };
                    
                    StartCoroutine(AnalyzeFrame(frame));
                }
            }
        }
        
        public List<DetectedObject> GetCurrentObjects() => new List<DetectedObject>(currentObjects);
        public List<DetectedObject> GetObjectsByCategory(string category) => 
            currentObjects.Where(obj => obj.category.ToLower() == category.ToLower()).ToList();
        public DetectedObject GetObjectByName(string name) => 
            currentObjects.FirstOrDefault(obj => obj.name.ToLower().Contains(name.ToLower()));
        
        // Performance monitoring
        public float GetAverageAnalysisTime() => averageAnalysisTime;
        public int GetTotalAnalyses() => totalAnalyses;
        public int GetTotalObjectCount() => objectDatabase.Count;
        public int GetCurrentObjectCount() => currentObjects.Count;
        public int GetQueueLength() => frameQueue.Count;
        public bool IsProcessing() => isProcessing;
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveObjectDatabase();
            }
        }
        
        private void OnDestroy()
        {
            enableContinuousDetection = false;
            SaveObjectDatabase();
            
            if (cameraManager != null)
            {
                cameraManager.OnFrameCaptured -= HandleFrameCaptured;
            }
        }
    }
}
