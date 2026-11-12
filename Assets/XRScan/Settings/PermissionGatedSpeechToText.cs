using UnityEngine;
using UnityEngine.Events;
using XRScan.Core;

namespace XRScan.Settings
{
    /// <summary>
    /// <see cref="ISpeechToText"/> decorator in front of the real recognizer (e.g. CloudWhisperStt):
    /// <list type="bullet">
    /// <item>asks for the microphone permission the first time the user talks (push-to-talk), then starts the
    /// recording that press asked for;</item>
    /// <item>on denial never opens the microphone and reports why (voice questions stay off; a plain denial is
    /// asked again on the next press, a blocked one points to the system settings);</item>
    /// <item>does not record at all while the configured speech endpoint is unusable (not set, blocked by the
    /// transport policy, or a cloud provider without the opt-in), so no audio is captured that could not be
    /// processed on infrastructure the user chose.</item>
    /// </list>
    /// Events are the inner recognizer's own, so subscribers (ConversationOrchestrator) see its transcripts.
    /// </summary>
    public class PermissionGatedSpeechToText : MonoBehaviour, ISpeechToText
    {
        [Tooltip("The real speech-to-text component (implements ISpeechToText), e.g. CloudWhisperStt.")]
        [SerializeField] private MonoBehaviour inner;

        [Tooltip("Permission flow owner. When empty, the microphone is assumed to be available.")]
        [SerializeField] private RuntimePermissions permissions;

        [Tooltip("When set, recording only starts while this config yields a usable speech-to-text endpoint.")]
        [SerializeField] private VoiceConfig voiceConfig;

        private readonly UnityEvent _noListeningStarted = new UnityEvent();
        private readonly UnityEvent _noListeningStopped = new UnityEvent();
        private readonly UnityEvent<string> _noTranscription = new StringUnityEvent();
        private readonly UnityEvent<string> _noError = new StringUnityEvent();

        private bool _awaitingPermission;
        private bool _innerInitialized;

        private ISpeechToText Inner => inner as ISpeechToText;

        public bool IsInitialized { get; private set; }
        public bool IsListening => Inner != null && Inner.IsListening;
        public bool IsProcessing => _awaitingPermission || (Inner != null && Inner.IsProcessing);

        public UnityEvent OnListeningStarted => Inner != null ? Inner.OnListeningStarted : _noListeningStarted;
        public UnityEvent OnListeningStopped => Inner != null ? Inner.OnListeningStopped : _noListeningStopped;
        public UnityEvent<string> OnTranscriptionComplete => Inner != null ? Inner.OnTranscriptionComplete : _noTranscription;
        public UnityEvent<string> OnError => Inner != null ? Inner.OnError : _noError;

        /// <summary>Code-side wiring (the scene normally sets these).</summary>
        public void Configure(MonoBehaviour speechToText, RuntimePermissions runtimePermissions, VoiceConfig voice)
        {
            inner = speechToText;
            permissions = runtimePermissions;
            voiceConfig = voice;
            _innerInitialized = false;
        }

        public void Initialize()
        {
            IsInitialized = true;
            // The real recognizer is initialized once the microphone is granted: probing devices earlier is
            // pointless on Android (and some recognizers touch the microphone while initializing).
            if (MicrophoneState() == PermissionState.Granted) InitializeInner();
        }

        public void StartListening()
        {
            var stt = Inner;
            if (stt == null)
            {
                Report("No speech-to-text backend is assigned.");
                return;
            }
            if (stt.IsListening || _awaitingPermission) return;
            if (!EndpointUsable()) return;

            if (permissions == null)
            {
                Begin(stt);
                return;
            }

            _awaitingPermission = true;
            permissions.Ensure(AppPermissions.Microphone, state =>
            {
                _awaitingPermission = false;
                if (state == PermissionState.Granted) Begin(stt);
                else Report(PermissionTracker.Describe(AppPermissions.Microphone, state));
            });
        }

        public void StopListening()
        {
            if (Inner != null && Inner.IsListening) Inner.StopListening();
        }

        public void ToggleListening()
        {
            if (IsListening) StopListening();
            else StartListening();
        }

        private void Begin(ISpeechToText stt)
        {
            InitializeInner();
            stt.StartListening();
        }

        private void InitializeInner()
        {
            var stt = Inner;
            if (stt == null || _innerInitialized) return;
            stt.Initialize();
            _innerInitialized = stt.IsInitialized;
        }

        private PermissionState MicrophoneState() =>
            permissions != null ? permissions.GetState(AppPermissions.Microphone) : PermissionState.Granted;

        private bool EndpointUsable()
        {
            if (voiceConfig == null) return true;
            var check = voiceConfig.ResolveSttEndpoint();
            if (check.IsUsable) return true;
            Report("Speech-to-text is unavailable: " + check.Message);
            return false;
        }

        private void Report(string message)
        {
            Debug.LogWarning("[SpeechToText] " + message);
            RuntimePermissions.Post(message);
            OnError.Invoke(message);
        }
    }
}
