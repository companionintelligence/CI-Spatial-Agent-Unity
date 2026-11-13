using UnityEngine;
using XRScan.Agent;
using XRScan.Avatar;
using XRScan.Core;
using XRScan.Mapping;
using XRScan.Mocks;
using XRScan.Platform;
using XRScan.Vision;

namespace XRScan.App
{
    /// <summary>
    /// Single startup wiring point for the whole app. Chooses a camera provider and a detector
    /// (on-device Sentis / remote CV server; demo mocks only behind the developer switch
    /// <c>useMocks</c>), configures the scan pipeline, the avatar perception bridge (scan → look + speak),
    /// and the voice Q&amp;A conversation loop (STT → LLM grounded in scanned pins → TTS).
    /// Trigger via Scan() / ToggleListen(). With no vision server set, scanning says so instead of
    /// showing made-up detections.
    /// </summary>
    public class AppBootstrap : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private ServerConfig serverConfig;

        [Tooltip("Developer switch, off by default: scripted demo camera, detections and voice for the development " +
                 "scene (XRScan_Dev). Release builds of a scene with it on fail (ReleaseMockGuard).")]
        [SerializeField] private bool useMocks = false;

        [Header("Scan")]
        [SerializeField] private ScanController scanController;
        [SerializeField] private SpatialPinManager pinManager;
        [SerializeField] private MockCameraProvider mockCamera;
        [SerializeField] private ARFoundationCameraProvider arCamera;
        [SerializeField] private DesktopCameraProvider desktopCamera;

        [Header("Avatar")]
        [SerializeField] private AvatarPerceptionBridge perceptionBridge;
        [SerializeField] private GazeController gaze;
        [SerializeField] private MonoBehaviour ttsBehaviour; // ITextToSpeech

        [Header("Conversation (voice Q&A)")]
        [SerializeField] private ConversationOrchestrator conversation;
        [SerializeField] private SceneKnowledgeBase knowledge;
        [SerializeField] private MonoBehaviour sttBehaviour; // ISpeechToText
        [SerializeField] private MonoBehaviour llmBehaviour; // ILlmBackend

        private IPlatformCapabilities _capabilities;

        private void Start()
        {
            _capabilities = new PlatformCapabilities();
            Debug.Log($"[AppBootstrap] Vendor={_capabilities.Vendor} RGB={_capabilities.HasRgbCameraAccess} SceneMesh={_capabilities.HasSceneMesh}");

            ICameraProvider camera = CameraProviderFactory.Select(
                _capabilities, useMocks ? mockCamera : null, arCamera, desktopCamera);

            IObjectDetector detector = useMocks
                ? new MockObjectDetector()
                : DetectorFactory.Select(serverConfig, new SentisYoloDetector(), null);

            if (scanController != null) scanController.Configure(camera, detector);

            var tts = ttsBehaviour as ITextToSpeech;
            tts?.Initialize();
            if (perceptionBridge != null) perceptionBridge.Configure(pinManager, gaze, tts);

            if (conversation != null)
            {
                var stt = sttBehaviour as ISpeechToText;
                var llm = llmBehaviour as ILlmBackend;
                conversation.Configure(stt, llm, tts, knowledge);
            }

            Debug.Log("[AppBootstrap] Ready. Scan() to place a pin; ToggleListen() to talk.");
        }

        /// <summary>Scan trigger (controller button / UI / debug key).</summary>
        public void Scan()
        {
            if (scanController != null) scanController.Scan();
        }

        /// <summary>Push-to-talk toggle for the voice Q&amp;A loop.</summary>
        public void ToggleListen()
        {
            if (conversation != null) conversation.ToggleListen();
        }
    }
}
