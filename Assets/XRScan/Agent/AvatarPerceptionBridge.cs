using UnityEngine;
using XRScan.Avatar;
using XRScan.Core;
using XRScan.Mapping;

namespace XRScan.Agent
{
    /// <summary>
    /// The entire scan-&gt;avatar link for the vertical slice: when a new spatial pin is added,
    /// the avatar turns to look at it and speaks its label. Wires SpatialPinManager.OnPinAdded
    /// to GazeController + ITextToSpeech.
    /// </summary>
    public class AvatarPerceptionBridge : MonoBehaviour
    {
        [SerializeField] private SpatialPinManager pinManager;
        [SerializeField] private GazeController gaze;
        [SerializeField] private MonoBehaviour ttsBehaviour; // must implement ITextToSpeech

        private ITextToSpeech _tts;
        private bool _subscribed;

        private void Awake() => _tts = ttsBehaviour as ITextToSpeech;

        public void Configure(SpatialPinManager manager, GazeController gazeController, ITextToSpeech tts)
        {
            Unsubscribe();
            pinManager = manager;
            gaze = gazeController;
            _tts = tts;
            Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (pinManager == null || _subscribed) return;
            pinManager.OnPinAdded += HandlePinAdded;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (pinManager == null || !_subscribed) return;
            pinManager.OnPinAdded -= HandlePinAdded;
            _subscribed = false;
        }

        private void HandlePinAdded(SpatialPin pin)
        {
            if (pin == null) return;
            if (gaze != null) gaze.LookAt(pin.position);
            _tts?.SpeakText(string.IsNullOrEmpty(pin.label) ? "something" : pin.label);
        }
    }
}
