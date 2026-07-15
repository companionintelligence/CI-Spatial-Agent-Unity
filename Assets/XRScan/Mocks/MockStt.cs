using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using XRScan.Core;

namespace XRScan.Mocks
{
    /// <summary>Scripted STT for the in-Editor conversation loop: on stop-listening it emits a
    /// canned question after a short delay, cycling through a small set.</summary>
    public class MockStt : MonoBehaviour, ISpeechToText
    {
        [SerializeField] private string[] scriptedQuestions = { "what do you see?", "what is on the table?", "how many objects are there?" };
        [SerializeField] private float responseDelay = 0.3f;

        private int _index;
        private bool _initialized;
        private bool _listening;
        private bool _processing;

        public UnityEvent OnListeningStarted { get; } = new UnityEvent();
        public UnityEvent OnListeningStopped { get; } = new UnityEvent();
        public UnityEvent<string> OnTranscriptionComplete { get; } = new StringUnityEvent();
        public UnityEvent<string> OnError { get; } = new StringUnityEvent();

        public bool IsInitialized => _initialized;
        public bool IsListening => _listening;
        public bool IsProcessing => _processing;

        public void Initialize() => _initialized = true;

        public void StartListening()
        {
            if (!_initialized) Initialize();
            _listening = true;
            OnListeningStarted.Invoke();
        }

        public void StopListening()
        {
            if (!_listening) return;
            _listening = false;
            OnListeningStopped.Invoke();
            StartCoroutine(Emit());
        }

        public void ToggleListening()
        {
            if (_listening) StopListening(); else StartListening();
        }

        private IEnumerator Emit()
        {
            _processing = true;
            yield return new WaitForSeconds(responseDelay);
            string q = scriptedQuestions[_index % scriptedQuestions.Length];
            _index++;
            _processing = false;
            Debug.Log("[MockStt] transcript: " + q);
            OnTranscriptionComplete.Invoke(q);
        }
    }
}
