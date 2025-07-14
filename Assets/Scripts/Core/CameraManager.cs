using UnityEngine;
using System.Collections;
using Unity.XR.XREAL;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Manages XREAL camera systems for visual input and analysis.
    /// Handles RGB camera capture, frame processing, and integration with AI vision systems.
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        [Header("Camera Configuration")]
        [SerializeField] private CameraType cameraType = CameraType.RGB;
        [SerializeField] private ResolutionLevel resolutionLevel = ResolutionLevel.Middle;
        [SerializeField] private int targetFrameRate = 30;
        [SerializeField] private bool enableContinuousCapture = true;
        [SerializeField] private float captureInterval = 2.0f;
        
        [Header("Privacy Settings")]
        [SerializeField] private bool showCameraIndicator = true;
        [SerializeField] private GameObject cameraIndicatorUI;
        
        [Header("Processing Settings")]
        [SerializeField] private bool enableImagePreprocessing = true;
        [SerializeField] private int maxImageSize = 512;
        [SerializeField] private TextureFormat outputFormat = TextureFormat.RGB24;
        
        // XREAL Camera components
        private XREALRGBCameraTexture rgbCameraTexture;
        private XREALPhotoCapture photoCapture;
        private XREALVideoCapture videoCapture;
        
        // State tracking
        private bool isInitialized = false;
        private bool isCapturing = false;
        private bool isContinuousMode = false;
        
        // Frame data
        private Texture2D currentFrameTexture;
        private byte[] lastCapturedFrame;
        
        // Events
        public event System.Action<byte[]> OnFrameCaptured;
        public event System.Action<Texture2D> OnFrameTextureReady;
        
        public bool IsInitialized => isInitialized;
        public bool IsCapturing => isCapturing;
        
        public enum ResolutionLevel
        {
            Low,
            Middle,
            High
        }
        
        public void Initialize()
        {
            Debug.Log("[CameraManager] Initializing XREAL camera system...");
            
            StartCoroutine(RequestCameraPermission());
        }
        
        private IEnumerator RequestCameraPermission()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
                
                yield return new WaitForSeconds(1.0f);
                
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
                {
                    Debug.LogError("[CameraManager] Camera permission denied!");
                    yield break;
                }
            }
            #endif
            
            // Initialize XREAL RGB Camera
            InitializeRGBCamera();
            
            isInitialized = true;
            Debug.Log("[CameraManager] Camera system initialized successfully");
            
            if (enableContinuousCapture)
            {
                StartContinuousCapture();
            }
        }
        
        private void InitializeRGBCamera()
        {
            try
            {
                // Create XREAL RGB camera texture singleton
                rgbCameraTexture = XREALRGBCameraTexture.CreateSingleton();
                
                if (rgbCameraTexture != null)
                {
                    Debug.Log("[CameraManager] XREAL RGB Camera initialized");
                    
                    // Start camera capture
                    rgbCameraTexture.StartCapture();
                    
                    // Setup frame texture based on camera resolution
                    var yuvTextures = rgbCameraTexture.GetYUVFormatTextures();
                    if (yuvTextures != null && yuvTextures.Length > 0 && yuvTextures[0] != null)
                    {
                        var sourceTexture = yuvTextures[0];
                        currentFrameTexture = new Texture2D(sourceTexture.width, sourceTexture.height, outputFormat, false);
                        Debug.Log($"[CameraManager] Frame texture created: {sourceTexture.width}x{sourceTexture.height}");
                    }
                }
                else
                {
                    Debug.LogError("[CameraManager] Failed to create XREAL RGB Camera texture");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CameraManager] Error initializing RGB camera: {e.Message}");
            }
        }
        
        public void StartContinuousCapture()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[CameraManager] Cannot start capture - system not initialized");
                return;
            }
            
            if (isContinuousMode)
                return;
                
            Debug.Log("[CameraManager] Starting continuous frame capture...");
            
            isContinuousMode = true;
            isCapturing = true;
            
            if (showCameraIndicator && cameraIndicatorUI != null)
            {
                cameraIndicatorUI.SetActive(true);
            }
            
            StartCoroutine(ContinuousCaptureLoop());
        }
        
        public void StopContinuousCapture()
        {
            Debug.Log("[CameraManager] Stopping continuous capture...");
            
            isContinuousMode = false;
            isCapturing = false;
            
            if (cameraIndicatorUI != null)
            {
                cameraIndicatorUI.SetActive(false);
            }
        }
        
        private IEnumerator ContinuousCaptureLoop()
        {
            while (isContinuousMode && isInitialized)
            {
                CaptureCurrentFrameAsync();
                yield return new WaitForSeconds(captureInterval);
            }
        }
        
        public byte[] CaptureCurrentFrame()
        {
            if (!isInitialized || rgbCameraTexture == null)
            {
                Debug.LogWarning("[CameraManager] Cannot capture frame - camera not ready");
                return null;
            }
            
            try
            {
                var yuvTextures = rgbCameraTexture.GetYUVFormatTextures();
                if (yuvTextures != null && yuvTextures.Length > 0 && yuvTextures[0] != null)
                {
                    var sourceTexture = yuvTextures[0];
                    
                    // Convert YUV to RGB texture
                    var rgbTexture = ConvertYUVToRGB(yuvTextures);
                    
                    if (rgbTexture != null)
                    {
                        // Apply preprocessing if enabled
                        if (enableImagePreprocessing)
                        {
                            rgbTexture = PreprocessImage(rgbTexture);
                        }
                        
                        // Convert to byte array
                        byte[] frameData = rgbTexture.EncodeToJPG(85); // 85% quality
                        lastCapturedFrame = frameData;
                        
                        OnFrameTextureReady?.Invoke(rgbTexture);
                        OnFrameCaptured?.Invoke(frameData);
                        
                        Debug.Log($"[CameraManager] Frame captured: {frameData.Length} bytes");
                        return frameData;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CameraManager] Error capturing frame: {e.Message}");
            }
            
            return null;
        }
        
        private void CaptureCurrentFrameAsync()
        {
            StartCoroutine(CaptureFrameCoroutine());
        }
        
        private IEnumerator CaptureFrameCoroutine()
        {
            var frameData = CaptureCurrentFrame();
            if (frameData != null)
            {
                // Frame captured successfully
                yield return null;
            }
        }
        
        private Texture2D ConvertYUVToRGB(Texture2D[] yuvTextures)
        {
            if (yuvTextures == null || yuvTextures.Length < 3)
                return null;
            
            var yTexture = yuvTextures[0];
            var uTexture = yuvTextures[1];
            var vTexture = yuvTextures[2];
            
            if (yTexture == null)
                return null;
            
            // Create RGB texture
            if (currentFrameTexture == null || 
                currentFrameTexture.width != yTexture.width || 
                currentFrameTexture.height != yTexture.height)
            {
                if (currentFrameTexture != null)
                    DestroyImmediate(currentFrameTexture);
                    
                currentFrameTexture = new Texture2D(yTexture.width, yTexture.height, outputFormat, false);
            }
            
            // Simple approach: use Y channel as grayscale for now
            // TODO: Implement proper YUV to RGB conversion
            Graphics.CopyTexture(yTexture, currentFrameTexture);
            
            return currentFrameTexture;
        }
        
        private Texture2D PreprocessImage(Texture2D sourceTexture)
        {
            if (sourceTexture == null)
                return null;
            
            // Resize if image is too large
            if (sourceTexture.width > maxImageSize || sourceTexture.height > maxImageSize)
            {
                float scale = Mathf.Min((float)maxImageSize / sourceTexture.width, (float)maxImageSize / sourceTexture.height);
                int newWidth = Mathf.RoundToInt(sourceTexture.width * scale);
                int newHeight = Mathf.RoundToInt(sourceTexture.height * scale);
                
                return ResizeTexture(sourceTexture, newWidth, newHeight);
            }
            
            return sourceTexture;
        }
        
        private Texture2D ResizeTexture(Texture2D sourceTexture, int width, int height)
        {
            // Create render texture for resizing
            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height);
            Graphics.Blit(sourceTexture, renderTexture);
            
            // Create new texture with resized content
            RenderTexture.active = renderTexture;
            Texture2D resizedTexture = new Texture2D(width, height, outputFormat, false);
            resizedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            resizedTexture.Apply();
            
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(renderTexture);
            
            return resizedTexture;
        }
        
        public void TakePhoto()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[CameraManager] Cannot take photo - system not initialized");
                return;
            }
            
            StartCoroutine(TakePhotoCoroutine());
        }
        
        private IEnumerator TakePhotoCoroutine()
        {
            Debug.Log("[CameraManager] Taking photo...");
            
            XREALPhotoCapture.CreateAsync(false, (photoCapture) =>
            {
                if (photoCapture != null)
                {
                    this.photoCapture = photoCapture;
                    
                    // Configure camera parameters
                    CameraParameters cameraParameters = new CameraParameters();
                    var resolution = GetResolutionByLevel(resolutionLevel);
                    cameraParameters.cameraResolutionWidth = resolution.width;
                    cameraParameters.cameraResolutionHeight = resolution.height;
                    cameraParameters.pixelFormat = CapturePixelFormat.PNG;
                    cameraParameters.cameraType = cameraType;
                    
                    // Start photo mode
                    photoCapture.StartPhotoModeAsync(cameraParameters, (result) =>
                    {
                        if (result.success)
                        {
                            Debug.Log("[CameraManager] Photo mode started successfully");
                            
                            // Take the photo
                            string filename = $"companion_photo_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
                            string filepath = System.IO.Path.Combine(Application.persistentDataPath, filename);
                            
                            photoCapture.TakePhotoAsync(filepath, PhotoCaptureFileOutputFormat.PNG, (photoCaptureResult) =>
                            {
                                if (photoCaptureResult.success)
                                {
                                    Debug.Log($"[CameraManager] Photo saved to: {filepath}");
                                }
                                else
                                {
                                    Debug.LogError("[CameraManager] Failed to take photo");
                                }
                                
                                // Stop photo mode
                                photoCapture.StopPhotoModeAsync((stopResult) =>
                                {
                                    Debug.Log("[CameraManager] Photo mode stopped");
                                    this.photoCapture = null;
                                });
                            });
                        }
                        else
                        {
                            Debug.LogError("[CameraManager] Failed to start photo mode");
                        }
                    });
                }
                else
                {
                    Debug.LogError("[CameraManager] Failed to create photo capture");
                }
            });
            
            yield return null;
        }
        
        private Resolution GetResolutionByLevel(ResolutionLevel level)
        {
            var supportedResolutions = XREALPhotoCapture.SupportedResolutions;
            if (supportedResolutions != null && supportedResolutions.Length > 0)
            {
                var sortedResolutions = System.Linq.Enumerable.OrderByDescending(supportedResolutions, r => r.width * r.height);
                
                switch (level)
                {
                    case ResolutionLevel.High:
                        return sortedResolutions.First();
                    case ResolutionLevel.Middle:
                        return sortedResolutions.ElementAtOrDefault(1) ?? sortedResolutions.First();
                    case ResolutionLevel.Low:
                        return sortedResolutions.ElementAtOrDefault(2) ?? sortedResolutions.Last();
                    default:
                        return sortedResolutions.First();
                }
            }
            
            return new Resolution { width = 1280, height = 720 };
        }
        
        public byte[] GetLastCapturedFrame()
        {
            return lastCapturedFrame;
        }
        
        public void Cleanup()
        {
            Debug.Log("[CameraManager] Cleaning up camera system...");
            
            StopContinuousCapture();
            
            if (rgbCameraTexture != null && rgbCameraTexture.IsCapturing)
            {
                rgbCameraTexture.StopCapture();
            }
            
            if (photoCapture != null)
            {
                photoCapture.StopPhotoModeAsync((result) => {
                    photoCapture = null;
                });
            }
            
            if (currentFrameTexture != null)
            {
                DestroyImmediate(currentFrameTexture);
                currentFrameTexture = null;
            }
            
            isInitialized = false;
        }
        
        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
