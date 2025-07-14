using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Linq;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// XREAL Camera Manager - Handles camera capture using XREAL SDK
    /// Compatible with XREAL Air, XREAL Light, and XREAL Beam devices
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        [Header("Camera Configuration")]
        [SerializeField] private ResolutionLevel resolutionLevel = ResolutionLevel.Middle;
        [SerializeField] private int targetFrameRate = 30;
        [SerializeField] private bool enableContinuousCapture = true;
        [SerializeField] private float captureInterval = 2.0f;
        [SerializeField] private bool enableDebugLogging = true;
        
        [Header("Capture Settings")]
        [SerializeField] private BlendMode blendMode = BlendMode.Blend;
        [SerializeField] private CapturePixelFormat pixelFormat = CapturePixelFormat.PNG;
        [SerializeField] private CaptureSide captureSide = CaptureSide.Single;
        [SerializeField] private Color backgroundColor = Color.black;
        
        // Events
        public event Action<Texture2D> OnFrameTextureReady;
        public event Action<byte[]> OnFrameDataReady;
        public UnityEvent<string> OnCameraError;
        public UnityEvent OnCameraInitialized;
        
        // XREAL SDK Components
        private XREALPhotoCapture photoCapture;
        private XREALRGBCameraTexture rgbCameraTexture;
        private Resolution currentResolution;
        
        // State tracking
        private bool isInitialized = false;
        private bool isContinuousMode = false;
        private Coroutine continuousCaptureCoroutine;
        private CameraParameters cameraParameters;
        
        public bool IsInitialized => isInitialized;
        public bool IsContinuousMode => isContinuousMode;
        public Resolution CurrentResolution => currentResolution;
        
        // Resolution levels enum for XREAL compatibility
        public enum ResolutionLevel
        {
            Low,
            Middle, 
            High
        }
        
        private void Start()
        {
            StartCoroutine(InitializeCamera());
        }
        
        private IEnumerator InitializeCamera()
        {
            if (enableDebugLogging)
                Debug.Log("[CameraManager] Initializing XREAL camera system...");
            
            try
            {
                // Initialize RGB camera texture for live preview
                rgbCameraTexture = XREALRGBCameraTexture.CreateSingleton();
                
                if (rgbCameraTexture == null)
                {
                    OnCameraError?.Invoke("Failed to create RGB camera texture");
                    yield break;
                }
                
                // Setup camera parameters
                SetupCameraParameters();
                
                // Create photo capture for image capture
                yield return StartCoroutine(CreatePhotoCapture());
                
                if (photoCapture != null)
                {
                    isInitialized = true;
                    OnCameraInitialized?.Invoke();
                    
                    if (enableDebugLogging)
                        Debug.Log("[CameraManager] XREAL camera initialized successfully");
                    
                    // Start continuous capture if enabled
                    if (enableContinuousCapture)
                    {
                        StartContinuousCapture();
                    }
                }
                else
                {
                    OnCameraError?.Invoke("Failed to create photo capture");
                }
            }
            catch (Exception e)
            {
                OnCameraError?.Invoke($"Camera initialization error: {e.Message}");
                if (enableDebugLogging)
                    Debug.LogError($"[CameraManager] Initialization error: {e}");
            }
        }
        
        private void SetupCameraParameters()
        {
            currentResolution = GetResolutionByLevel(resolutionLevel);
            
            cameraParameters = new CameraParameters();
            cameraParameters.cameraType = CameraType.RGB;
            cameraParameters.hologramOpacity = 0.0f;
            cameraParameters.frameRate = targetFrameRate;
            cameraParameters.cameraResolutionWidth = currentResolution.width;
            cameraParameters.cameraResolutionHeight = currentResolution.height;
            cameraParameters.pixelFormat = pixelFormat;
            cameraParameters.blendMode = blendMode;
            cameraParameters.audioState = AudioState.None; // No audio for image capture
            cameraParameters.captureSide = captureSide;
            cameraParameters.backgroundColor = backgroundColor;
            
            if (enableDebugLogging)
                Debug.Log($"[CameraManager] Camera parameters set - Resolution: {currentResolution.width}x{currentResolution.height}, FPS: {targetFrameRate}");
        }
        
        private IEnumerator CreatePhotoCapture()
        {
            bool captureCreated = false;
            
            XREALPhotoCapture.CreateAsync(false, delegate (XREALPhotoCapture captureObject)
            {
                if (captureObject != null)
                {
                    photoCapture = captureObject;
                    captureCreated = true;
                    
                    if (enableDebugLogging)
                        Debug.Log("[CameraManager] Photo capture created successfully");
                }
                else
                {
                    if (enableDebugLogging)
                        Debug.LogError("[CameraManager] Failed to create photo capture object");
                }
            });
            
            // Wait for async creation to complete
            float timeout = 5.0f;
            float elapsed = 0;
            
            while (!captureCreated && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            if (!captureCreated)
            {
                OnCameraError?.Invoke("Photo capture creation timed out");
            }
        }
        
        private Resolution GetResolutionByLevel(ResolutionLevel level)
        {
            var supportedResolutions = XREALPhotoCapture.SupportedResolutions;
            var resolutions = supportedResolutions.OrderByDescending(res => res.width * res.height);
            
            switch (level)
            {
                case ResolutionLevel.High:
                    return resolutions.ElementAt(0);
                case ResolutionLevel.Middle:
                    return resolutions.ElementAt(Mathf.Min(1, resolutions.Count() - 1));
                case ResolutionLevel.Low:
                    return resolutions.ElementAt(Mathf.Min(2, resolutions.Count() - 1));
                default:
                    return resolutions.ElementAt(1); // Default to middle
            }
        }
        
        public void StartContinuousCapture()
        {
            if (!isInitialized)
            {
                OnCameraError?.Invoke("Camera not initialized");
                return;
            }
            
            if (isContinuousMode)
                return;
            
            isContinuousMode = true;
            continuousCaptureCoroutine = StartCoroutine(ContinuousCaptureLoop());
            
            if (enableDebugLogging)
                Debug.Log("[CameraManager] Continuous capture started");
        }
        
        public void StopContinuousCapture()
        {
            if (isContinuousMode && continuousCaptureCoroutine != null)
            {
                StopCoroutine(continuousCaptureCoroutine);
                continuousCaptureCoroutine = null;
            }
            
            isContinuousMode = false;
            
            if (enableDebugLogging)
                Debug.Log("[CameraManager] Continuous capture stopped");
        }
        
        private IEnumerator ContinuousCaptureLoop()
        {
            while (isContinuousMode)
            {
                TriggerSingleCapture();
                yield return new WaitForSeconds(captureInterval);
            }
        }
        
        public void TriggerSingleCapture()
        {
            if (!isInitialized || photoCapture == null)
            {
                OnCameraError?.Invoke("Camera not ready for capture");
                return;
            }
            
            StartCoroutine(CapturePhoto());
        }
        
        private IEnumerator CapturePhoto()
        {
            bool captureComplete = false;
            byte[] imageData = null;
            
            // Activate camera first
            photoCapture.StartPhotoModeAsync(cameraParameters, delegate (XREALPhotoCapture.PhotoCaptureResult result)
            {
                if (result.success)
                {
                    // Take the photo
                    photoCapture.TakePhotoAsync(delegate (XREALPhotoCapture.PhotoCaptureResult captureResult, PhotoCaptureFrame photoCaptureFrame)
                    {
                        if (captureResult.success && photoCaptureFrame != null)
                        {
                            // Get image data
                            imageData = photoCaptureFrame.GetImageData();
                            
                            // Convert to Texture2D if needed
                            if (OnFrameTextureReady != null)
                            {
                                var texture = new Texture2D(currentResolution.width, currentResolution.height, TextureFormat.RGB24, false);
                                texture.LoadImage(imageData);
                                OnFrameTextureReady?.Invoke(texture);
                            }
                            
                            // Provide raw data
                            OnFrameDataReady?.Invoke(imageData);
                            
                            if (enableDebugLogging)
                                Debug.Log($"[CameraManager] Photo captured successfully - {imageData.Length} bytes");
                        }
                        else
                        {
                            OnCameraError?.Invoke("Photo capture failed");
                        }
                        
                        captureComplete = true;
                    });
                }
                else
                {
                    OnCameraError?.Invoke("Failed to start photo mode");
                    captureComplete = true;
                }
            });
            
            // Wait for capture to complete
            yield return new WaitUntil(() => captureComplete);
            
            // Stop photo mode
            if (photoCapture != null)
            {
                photoCapture.StopPhotoModeAsync(delegate (XREALPhotoCapture.PhotoCaptureResult result)
                {
                    if (enableDebugLogging && !result.success)
                        Debug.LogWarning("[CameraManager] Failed to stop photo mode cleanly");
                });
            }
        }
        
        public void UpdateResolution(ResolutionLevel newLevel)
        {
            resolutionLevel = newLevel;
            currentResolution = GetResolutionByLevel(newLevel);
            SetupCameraParameters();
            
            if (enableDebugLogging)
                Debug.Log($"[CameraManager] Resolution updated to {currentResolution.width}x{currentResolution.height}");
        }
        
        public void UpdateCaptureInterval(float interval)
        {
            captureInterval = Mathf.Max(0.5f, interval);
            
            if (enableDebugLogging)
                Debug.Log($"[CameraManager] Capture interval updated to {captureInterval}s");
        }
        
        private void OnDestroy()
        {
            StopContinuousCapture();
            
            if (photoCapture != null)
            {
                photoCapture.Dispose();
                photoCapture = null;
            }
            
            if (rgbCameraTexture != null)
            {
                rgbCameraTexture.StopCapture();
            }
            
            if (enableDebugLogging)
                Debug.Log("[CameraManager] Camera resources cleaned up");
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                StopContinuousCapture();
            }
            else if (enableContinuousCapture && isInitialized)
            {
                StartContinuousCapture();
            }
        }
    }
}
