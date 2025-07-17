using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// XREAL Camera Manager - Handles camera capture for vision processing
    /// Simplified and consolidated from multiple camera components
    /// Implements Unity best practices for performance and memory management
    /// </summary>
    public class CameraManager : MonoBehaviour, IXREALCameraManager
    {
        [Header("Camera Configuration")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private int captureWidth = 512;
        [SerializeField] private int captureHeight = 512;
        [SerializeField] private bool autoFindMainCamera = true;
        [SerializeField] private bool enableDebugLogging = true;
        
        [Header("Capture Settings")]
        [SerializeField] private TextureFormat textureFormat = TextureFormat.RGB24;
        [SerializeField] private bool optimizeForVision = true;
        [SerializeField] private FilterMode filterMode = FilterMode.Bilinear;
        
        [Header("Events")]
        public UnityEvent OnCaptureStarted { get; private set; } = new UnityEvent();
        public UnityEvent<Texture2D> OnImageCaptured { get; private set; } = new UnityEvent<Texture2D>();
        public UnityEvent<string> OnError { get; private set; } = new UnityEvent<string>();
        
        // State
        private bool isInitialized = false;
        private Texture2D lastCapturedImage;
        private RenderTexture renderTexture;
        private bool hasCapturedImage = false;
        
        public bool IsInitialized => isInitialized;
        public bool HasCapturedImage => hasCapturedImage;
        
        private void Start()
        {
            Initialize();
        }
        
        public void Initialize()
        {
            if (isInitialized) return;
            
            LogDebug("Initializing XREAL Camera Manager...");
            
            // Auto-find main camera if not assigned
            if (targetCamera == null && autoFindMainCamera)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    targetCamera = FindAnyObjectByType<Camera>();
                }
            }
            
            if (targetCamera == null)
            {
                LogError("No camera found! Please assign a target camera.");
                return;
            }
            
            // Create render texture for capture
            CreateRenderTexture();
            
            isInitialized = true;
            LogDebug($"Camera Manager initialized with camera: {targetCamera.name}");
        }
        
        private void CreateRenderTexture()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
            }
            
            renderTexture = new RenderTexture(captureWidth, captureHeight, 24);
            renderTexture.format = RenderTextureFormat.ARGB32;
            renderTexture.filterMode = filterMode;
            renderTexture.Create();
            
            LogDebug($"Created render texture: {captureWidth}x{captureHeight}");
        }
        
        /// <summary>
        /// Capture an image from the camera
        /// </summary>
        public void CaptureImage()
        {
            if (!isInitialized)
            {
                LogError("Camera Manager not initialized");
                return;
            }
            
            StartCoroutine(CaptureImageCoroutine());
        }
        
        private IEnumerator CaptureImageCoroutine()
        {
            OnCaptureStarted?.Invoke();
            LogDebug("Starting image capture...");
            
            try
            {
                // Capture camera to render texture
                RenderTexture previousTarget = targetCamera.targetTexture;
                targetCamera.targetTexture = renderTexture;
                targetCamera.Render();
                targetCamera.targetTexture = previousTarget;
                
                // Copy render texture to Texture2D
                RenderTexture.active = renderTexture;
                
                // Clean up previous capture
                if (lastCapturedImage != null)
                {
                    DestroyImmediate(lastCapturedImage);
                }
                
                lastCapturedImage = new Texture2D(captureWidth, captureHeight, textureFormat, false);
                lastCapturedImage.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
                lastCapturedImage.Apply();
                
                RenderTexture.active = null;
                
                hasCapturedImage = true;
                LogDebug("Image capture completed successfully");
                OnImageCaptured?.Invoke(lastCapturedImage);
            }
            catch (System.Exception e)
            {
                LogError($"Failed to capture image: {e.Message}");
                OnError?.Invoke($"Capture failed: {e.Message}");
            }
            
            yield return null;
        }
        
        /// <summary>
        /// Get the last captured image (reference, not copy)
        /// </summary>
        public Texture2D GetLastCapturedImage()
        {
            return lastCapturedImage;
        }
        
        /// <summary>
        /// Get a copy of the last captured image (caller is responsible for cleanup)
        /// </summary>
        public Texture2D GetLastCapturedImageCopy()
        {
            if (lastCapturedImage == null) return null;
            
            Texture2D copy = new Texture2D(lastCapturedImage.width, lastCapturedImage.height, lastCapturedImage.format, false);
            copy.SetPixels(lastCapturedImage.GetPixels());
            copy.Apply();
            return copy;
        }
        
        /// <summary>
        /// Clear captured image data
        /// </summary>
        public void ClearCapturedImage()
        {
            if (lastCapturedImage != null)
            {
                DestroyImmediate(lastCapturedImage);
                lastCapturedImage = null;
            }
            hasCapturedImage = false;
            LogDebug("Captured image cleared");
        }
        
        /// <summary>
        /// Set capture resolution
        /// </summary>
        public void SetCaptureResolution(int width, int height)
        {
            if (captureWidth != width || captureHeight != height)
            {
                captureWidth = width;
                captureHeight = height;
                CreateRenderTexture();
                LogDebug($"Capture resolution changed to: {width}x{height}");
            }
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[XREALCameraManager] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[XREALCameraManager] {message}");
        }
        
        private void OnDestroy()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                renderTexture = null;
            }
            
            if (lastCapturedImage != null)
            {
                DestroyImmediate(lastCapturedImage);
                lastCapturedImage = null;
            }
        }
        
        #region Editor Utilities
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void OnValidate()
        {
            // Ensure valid capture dimensions
            captureWidth = Mathf.Max(64, captureWidth);
            captureHeight = Mathf.Max(64, captureHeight);
        }
        
        #endregion
    }
}
