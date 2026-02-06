using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Performance monitoring and analytics for XREAL Vision System
    /// Tracks metrics, memory usage, and system performance
    /// </summary>
    public static class XREALPerformanceMonitor
    {
        private static Dictionary<string, PerformanceMetric> metrics = new Dictionary<string, PerformanceMetric>();
        private static Dictionary<string, Stopwatch> activeTimers = new Dictionary<string, Stopwatch>();
        private static bool isEnabled = true;
        
        [Serializable]
        public class PerformanceMetric
        {
            public string name;
            public int executionCount;
            public float totalTime;
            public float averageTime;
            public float lastExecutionTime;
            public float minTime = float.MaxValue;
            public float maxTime = float.MinValue;
            public DateTime lastUpdated;
        }
        
        public static bool IsEnabled
        {
            get => isEnabled;
            set => isEnabled = value;
        }
        
        /// <summary>
        /// Start timing a performance metric
        /// </summary>
        public static void StartTimer(string metricName)
        {
            if (!isEnabled) return;
            
            if (!activeTimers.ContainsKey(metricName))
            {
                activeTimers[metricName] = new Stopwatch();
            }
            
            activeTimers[metricName].Restart();
        }
        
        /// <summary>
        /// Stop timing and record the metric
        /// </summary>
        public static void StopTimer(string metricName)
        {
            if (!isEnabled) return;
            
            if (activeTimers.TryGetValue(metricName, out Stopwatch timer))
            {
                timer.Stop();
                RecordMetric(metricName, (float)timer.Elapsed.TotalMilliseconds);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[PerformanceMonitor] Timer '{metricName}' was not started");
            }
        }
        
        /// <summary>
        /// Record a performance metric value
        /// </summary>
        public static void RecordMetric(string metricName, float value)
        {
            if (!isEnabled) return;
            
            if (!metrics.TryGetValue(metricName, out PerformanceMetric metric))
            {
                metric = new PerformanceMetric { name = metricName };
                metrics[metricName] = metric;
            }
            
            metric.executionCount++;
            metric.totalTime += value;
            metric.averageTime = metric.totalTime / metric.executionCount;
            metric.lastExecutionTime = value;
            metric.lastUpdated = DateTime.Now;
            
            if (value < metric.minTime)
                metric.minTime = value;
            if (value > metric.maxTime)
                metric.maxTime = value;
        }
        
        /// <summary>
        /// Get performance metric by name
        /// </summary>
        public static PerformanceMetric GetMetric(string metricName)
        {
            return metrics.TryGetValue(metricName, out PerformanceMetric metric) ? metric : null;
        }
        
        /// <summary>
        /// Get all recorded metrics
        /// </summary>
        public static Dictionary<string, PerformanceMetric> GetAllMetrics()
        {
            return new Dictionary<string, PerformanceMetric>(metrics);
        }
        
        /// <summary>
        /// Clear all metrics
        /// </summary>
        public static void ClearMetrics()
        {
            metrics.Clear();
            activeTimers.Clear();
        }
        
        /// <summary>
        /// Log performance summary
        /// </summary>
        public static void LogPerformanceSummary()
        {
            if (!isEnabled || metrics.Count == 0) return;
            
            UnityEngine.Debug.Log("[PerformanceMonitor] === Performance Summary ===");
            foreach (var kvp in metrics)
            {
                var metric = kvp.Value;
                UnityEngine.Debug.Log($"[PerformanceMonitor] {metric.name}: " +
                                      $"Count={metric.executionCount}, " +
                                      $"Avg={metric.averageTime:F2}ms, " +
                                      $"Min={metric.minTime:F2}ms, " +
                                      $"Max={metric.maxTime:F2}ms, " +
                                      $"Last={metric.lastExecutionTime:F2}ms");
            }
        }
        
        /// <summary>
        /// Record memory usage
        /// </summary>
        public static void RecordMemoryUsage(string context = "General")
        {
            if (!isEnabled) return;
            
            var memoryUsage = GC.GetTotalMemory(false) / (1024 * 1024); // MB
            RecordMetric($"Memory_{context}", memoryUsage);
        }
        
        /// <summary>
        /// Record frame time
        /// </summary>
        public static void RecordFrameTime()
        {
            if (!isEnabled) return;
            
            RecordMetric("FrameTime", Time.unscaledDeltaTime * 1000f); // ms
        }
        
        /// <summary>
        /// Get performance report as JSON string
        /// </summary>
        public static string GetPerformanceReport()
        {
            if (metrics.Count == 0) return "{}";
            
            var report = new Dictionary<string, object>();
            report["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            report["metrics"] = metrics;
            
            return JsonUtility.ToJson(report, true);
        }
    }
    
    /// <summary>
    /// Performance monitoring MonoBehaviour for automatic updates
    /// </summary>
    public class XREALPerformanceTracker : MonoBehaviour
    {
        [Header("Monitoring Settings")]
        [SerializeField] private bool enableAutoLogging = true;
        [SerializeField] private float logInterval = 60.0f; // seconds
        [SerializeField] private bool trackFrameTime = true;
        [SerializeField] private bool trackMemoryUsage = true;
        
        private float lastLogTime;
        
        private void Start()
        {
            lastLogTime = Time.time;
        }
        
        private void Update()
        {
            if (trackFrameTime)
            {
                XREALPerformanceMonitor.RecordFrameTime();
            }
            
            if (enableAutoLogging && Time.time - lastLogTime >= logInterval)
            {
                if (trackMemoryUsage)
                {
                    XREALPerformanceMonitor.RecordMemoryUsage("Update");
                }
                
                XREALPerformanceMonitor.LogPerformanceSummary();
                lastLogTime = Time.time;
            }
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus) // Resuming
            {
                XREALPerformanceMonitor.RecordMemoryUsage("AppResume");
            }
        }
        
        private void OnDestroy()
        {
            XREALPerformanceMonitor.LogPerformanceSummary();
        }
    }
}
