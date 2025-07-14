using UnityEngine;
using System.Collections;

namespace CompanionIntelligence.Core
{
    /// <summary>
    /// Manages 3D avatar creation, loading, and animation for the AI companion.
    /// Integrates with Ready Player Me for avatar customization and XREAL-optimized rendering.
    /// </summary>
    public class AvatarManager : MonoBehaviour
    {
        [Header("Avatar Configuration")]
        [SerializeField] private Transform avatarContainer;
        [SerializeField] private string defaultAvatarUrl = "";
        [SerializeField] private bool enableLipSync = true;
        [SerializeField] private bool enableFacialExpressions = true;
        [SerializeField] private bool enableGestureAnimation = true;
        
        [Header("XREAL Optimization")]
        [SerializeField] private int xrealLODLevel = 1; // 0=High, 1=Medium, 2=Low
        [SerializeField] private bool enableOcclusionCulling = true;
        [SerializeField] private float maxRenderDistance = 5.0f;
        [SerializeField] private bool adaptToDevicePerformance = true;
        
        [Header("Animation Settings")]
        [SerializeField] private float idleAnimationIntensity = 0.5f;
        [SerializeField] private float responseAnimationDuration = 2.0f;
        [SerializeField] private bool enableLookAtUser = true;
        [SerializeField] private Transform userHead; // XREAL head tracking reference
        
        // Avatar components
        private GameObject currentAvatar;
        private Animator avatarAnimator;
        private SkinnedMeshRenderer avatarRenderer;
        
        // Animation states
        private bool isInitialized = false;
        private bool isLoaded = false;
        private bool isSpeaking = false;
        private bool isListening = false;
        
        // Animation references
        private readonly int idleStateHash = Animator.StringToHash("Idle");
        private readonly int speakingStateHash = Animator.StringToHash("Speaking");
        private readonly int listeningStateHash = Animator.StringToHash("Listening");
        private readonly int greetingStateHash = Animator.StringToHash("Greeting");
        
        // Lip sync components (for future SALSA integration)
        private Component lipsyncComponent;
        
        // Events
        public event System.Action OnAvatarLoaded;
        public event System.Action OnAvatarLoadFailed;
        
        public bool IsInitialized => isInitialized;
        public bool IsLoaded => isLoaded;
        public bool IsSpeaking => isSpeaking;
        
        public void Initialize()
        {
            Debug.Log("[AvatarManager] Initializing avatar system...");
            
            // Setup avatar container
            if (avatarContainer == null)
            {
                var containerGO = new GameObject("AvatarContainer");
                containerGO.transform.SetParent(transform);
                avatarContainer = containerGO.transform;
            }
            
            // Configure for XREAL device performance
            ConfigureForXREAL();
            
            isInitialized = true;
            
            // Load default avatar if URL provided
            if (!string.IsNullOrEmpty(defaultAvatarUrl))
            {
                LoadAvatar(defaultAvatarUrl);
            }
            else
            {
                // Create a simple placeholder avatar
                CreatePlaceholderAvatar();
            }
        }
        
        private void ConfigureForXREAL()
        {
            Debug.Log("[AvatarManager] Configuring avatar system for XREAL devices...");
            
            // Detect XREAL device type and adjust settings
            #if UNITY_ANDROID && !UNITY_EDITOR
            
            // Adjust quality settings based on device
            var deviceModel = SystemInfo.deviceModel.ToLower();
            
            if (deviceModel.Contains("beam pro"))
            {
                // XREAL Beam Pro settings
                xrealLODLevel = 1; // Medium quality
                maxRenderDistance = 4.0f;
                enableOcclusionCulling = true;
                Debug.Log("[AvatarManager] Configured for XREAL Beam Pro");
            }
            else if (deviceModel.Contains("ultra"))
            {
                // XREAL Ultra settings - higher performance
                xrealLODLevel = 0; // High quality
                maxRenderDistance = 6.0f;
                enableOcclusionCulling = false;
                Debug.Log("[AvatarManager] Configured for XREAL Ultra");
            }
            else
            {
                // Default XREAL settings
                xrealLODLevel = 1;
                maxRenderDistance = 5.0f;
                enableOcclusionCulling = true;
                Debug.Log("[AvatarManager] Configured for generic XREAL device");
            }
            
            #else
            
            // Editor/Desktop settings
            xrealLODLevel = 0; // High quality for development
            maxRenderDistance = 10.0f;
            enableOcclusionCulling = false;
            Debug.Log("[AvatarManager] Configured for Editor/Desktop");
            
            #endif
        }
        
        public void LoadAvatar(string avatarUrl)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[AvatarManager] Cannot load avatar - system not initialized");
                return;
            }
            
            Debug.Log($"[AvatarManager] Loading avatar from URL: {avatarUrl}");
            
            StartCoroutine(LoadAvatarCoroutine(avatarUrl));
        }
        
        private IEnumerator LoadAvatarCoroutine(string avatarUrl)
        {
            // For now, create a placeholder since RPM integration requires the actual SDK
            // TODO: Replace with actual Ready Player Me loading when SDK is available
            yield return new WaitForSeconds(1.0f); // Simulate loading time
            
            CreatePlaceholderAvatar();
            
            yield return null;
        }
        
        private void CreatePlaceholderAvatar()
        {
            Debug.Log("[AvatarManager] Creating placeholder avatar...");
            
            // Clean up existing avatar
            if (currentAvatar != null)
            {
                DestroyImmediate(currentAvatar);
            }
            
            // Create a simple capsule as placeholder
            currentAvatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            currentAvatar.name = "PlaceholderAvatar";
            currentAvatar.transform.SetParent(avatarContainer);
            currentAvatar.transform.localPosition = Vector3.zero;
            currentAvatar.transform.localRotation = Quaternion.identity;
            currentAvatar.transform.localScale = new Vector3(0.6f, 1.0f, 0.6f);
            
            // Add a simple material
            var renderer = currentAvatar.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Standard"));
                material.color = new Color(0.7f, 0.8f, 1.0f, 1.0f); // Light blue
                renderer.material = material;
            }
            
            // Add animator component
            avatarAnimator = currentAvatar.GetComponent<Animator>();
            if (avatarAnimator == null)
            {
                avatarAnimator = currentAvatar.AddComponent<Animator>();
            }
            
            // Create a simple animation controller
            CreateSimpleAnimationController();
            
            // Add a simple "face" indicator
            var faceIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            faceIndicator.name = "FaceIndicator";
            faceIndicator.transform.SetParent(currentAvatar.transform);
            faceIndicator.transform.localPosition = new Vector3(0, 0.3f, 0.4f);
            faceIndicator.transform.localScale = Vector3.one * 0.2f;
            
            var faceRenderer = faceIndicator.GetComponent<Renderer>();
            if (faceRenderer != null)
            {
                var faceMaterial = new Material(Shader.Find("Standard"));
                faceMaterial.color = Color.white;
                faceMaterial.EnableKeyword("_EMISSION");
                faceMaterial.SetColor("_EmissionColor", Color.white * 0.3f);
                faceRenderer.material = faceMaterial;
            }
            
            // Optimize for XREAL
            OptimizeAvatarForXREAL();
            
            isLoaded = true;
            OnAvatarLoaded?.Invoke();
            
            Debug.Log("[AvatarManager] Placeholder avatar created and loaded");
            
            // Start idle animation
            SetAnimationState(AvatarState.Idle);
        }
        
        private void CreateSimpleAnimationController()
        {
            // For now, we'll handle animations programmatically
            // TODO: Create proper animation controller when RPM avatars are integrated
            
            // Start simple breathing/idle animation
            StartCoroutine(SimpleIdleAnimation());
        }
        
        private IEnumerator SimpleIdleAnimation()
        {
            if (currentAvatar == null) yield break;
            
            Vector3 originalScale = currentAvatar.transform.localScale;
            float time = 0f;
            
            while (currentAvatar != null && isLoaded)
            {
                if (!isSpeaking && !isListening)
                {
                    // Simple breathing animation
                    float breathe = Mathf.Sin(time * 1.5f) * 0.02f * idleAnimationIntensity;
                    currentAvatar.transform.localScale = originalScale + Vector3.up * breathe;
                }
                
                time += Time.deltaTime;
                yield return null;
            }
        }
        
        private void OptimizeAvatarForXREAL()
        {
            if (currentAvatar == null) return;
            
            // Apply LOD settings
            var renderers = currentAvatar.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                // Adjust material quality based on LOD level
                if (xrealLODLevel >= 2) // Low quality
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                else if (xrealLODLevel >= 1) // Medium quality
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                
                // Add distance culling
                if (enableOcclusionCulling)
                {
                    // TODO: Implement distance-based culling
                }
            }
            
            Debug.Log($"[AvatarManager] Avatar optimized for XREAL (LOD: {xrealLODLevel})");
        }
        
        public enum AvatarState
        {
            Idle,
            Speaking,
            Listening,
            Greeting,
            Thinking
        }
        
        public void SetAnimationState(AvatarState state)
        {
            if (!isLoaded) return;
            
            Debug.Log($"[AvatarManager] Setting avatar state: {state}");
            
            switch (state)
            {
                case AvatarState.Idle:
                    isSpeaking = false;
                    isListening = false;
                    break;
                    
                case AvatarState.Speaking:
                    isSpeaking = true;
                    isListening = false;
                    StartCoroutine(SpeakingAnimation());
                    break;
                    
                case AvatarState.Listening:
                    isSpeaking = false;
                    isListening = true;
                    StartCoroutine(ListeningAnimation());
                    break;
                    
                case AvatarState.Greeting:
                    StartCoroutine(GreetingAnimation());
                    break;
                    
                case AvatarState.Thinking:
                    StartCoroutine(ThinkingAnimation());
                    break;
            }
        }
        
        private IEnumerator SpeakingAnimation()
        {
            if (currentAvatar == null) yield break;
            
            float duration = responseAnimationDuration;
            float elapsed = 0f;
            
            var faceIndicator = currentAvatar.transform.Find("FaceIndicator");
            if (faceIndicator != null)
            {
                var renderer = faceIndicator.GetComponent<Renderer>();
                var material = renderer.material;
                
                while (elapsed < duration && isSpeaking)
                {
                    // Pulsing effect while speaking
                    float pulse = Mathf.Sin(elapsed * 8f) * 0.5f + 0.5f;
                    material.SetColor("_EmissionColor", Color.cyan * pulse);
                    
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                
                // Reset emission
                material.SetColor("_EmissionColor", Color.white * 0.3f);
            }
            
            if (isSpeaking)
            {
                SetAnimationState(AvatarState.Idle);
            }
        }
        
        private IEnumerator ListeningAnimation()
        {
            if (currentAvatar == null) yield break;
            
            var faceIndicator = currentAvatar.transform.Find("FaceIndicator");
            if (faceIndicator != null)
            {
                var renderer = faceIndicator.GetComponent<Renderer>();
                var material = renderer.material;
                
                float time = 0f;
                while (isListening)
                {
                    // Gentle pulsing while listening
                    float pulse = Mathf.Sin(time * 2f) * 0.3f + 0.7f;
                    material.SetColor("_EmissionColor", Color.green * pulse * 0.5f);
                    
                    time += Time.deltaTime;
                    yield return null;
                }
                
                // Reset emission
                material.SetColor("_EmissionColor", Color.white * 0.3f);
            }
        }
        
        private IEnumerator GreetingAnimation()
        {
            if (currentAvatar == null) yield break;
            
            // Simple scale animation for greeting
            Vector3 originalScale = currentAvatar.transform.localScale;
            float duration = 1.0f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                float progress = elapsed / duration;
                float scale = 1f + Mathf.Sin(progress * Mathf.PI) * 0.1f;
                currentAvatar.transform.localScale = originalScale * scale;
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            currentAvatar.transform.localScale = originalScale;
            SetAnimationState(AvatarState.Idle);
        }
        
        private IEnumerator ThinkingAnimation()
        {
            if (currentAvatar == null) yield break;
            
            var faceIndicator = currentAvatar.transform.Find("FaceIndicator");
            if (faceIndicator != null)
            {
                var renderer = faceIndicator.GetComponent<Renderer>();
                var material = renderer.material;
                
                float duration = 2.0f;
                float elapsed = 0f;
                
                while (elapsed < duration)
                {
                    // Thinking indicator - slow color change
                    float progress = elapsed / duration;
                    Color thinkingColor = Color.Lerp(Color.white, Color.yellow, Mathf.Sin(progress * Mathf.PI * 3f));
                    material.SetColor("_EmissionColor", thinkingColor * 0.5f);
                    
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                
                // Reset emission
                material.SetColor("_EmissionColor", Color.white * 0.3f);
            }
            
            SetAnimationState(AvatarState.Idle);
        }
        
        public void StartSpeaking()
        {
            SetAnimationState(AvatarState.Speaking);
        }
        
        public void StopSpeaking()
        {
            SetAnimationState(AvatarState.Idle);
        }
        
        public void StartListening()
        {
            SetAnimationState(AvatarState.Listening);
        }
        
        public void StopListening()
        {
            SetAnimationState(AvatarState.Idle);
        }
        
        public void PlayGreeting()
        {
            SetAnimationState(AvatarState.Greeting);
        }
        
        public void ShowThinking()
        {
            SetAnimationState(AvatarState.Thinking);
        }
        
        public Transform GetAvatarTransform()
        {
            return currentAvatar?.transform;
        }
        
        public void SetAvatarPosition(Vector3 position)
        {
            if (avatarContainer != null)
            {
                avatarContainer.position = position;
            }
        }
        
        public void SetAvatarRotation(Quaternion rotation)
        {
            if (avatarContainer != null)
            {
                avatarContainer.rotation = rotation;
            }
        }
        
        public void Cleanup()
        {
            Debug.Log("[AvatarManager] Cleaning up avatar system...");
            
            if (currentAvatar != null)
            {
                DestroyImmediate(currentAvatar);
                currentAvatar = null;
            }
            
            isInitialized = false;
            isLoaded = false;
            isSpeaking = false;
            isListening = false;
        }
        
        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
