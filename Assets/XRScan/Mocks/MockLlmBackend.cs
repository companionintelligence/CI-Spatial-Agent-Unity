using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using XRScan.Core;

namespace XRScan.Mocks
{
    /// <summary>Scripted LLM backend for the in-Editor conversation loop: replies after a short
    /// delay with an answer loosely grounded in the (scene-context-bearing) prompt.</summary>
    public class MockLlmBackend : MonoBehaviour, ILlmBackend
    {
        [SerializeField] private float thinkSeconds = 0.4f;
        private bool _processing;

        public UnityEvent OnProcessingStarted { get; } = new UnityEvent();
        public UnityEvent<string> OnResponseReceived { get; } = new StringUnityEvent();
        public UnityEvent<string> OnError { get; } = new StringUnityEvent();

        public bool IsInitialized => true;
        public bool IsConnected => true;
        public bool IsProcessing => _processing;
        public string ServerAddress => "mock";

        public void Initialize() { }
        public void SendTextRequest(string prompt) => StartCoroutine(Reply(prompt));
        public void SendVisionRequest(Texture2D image, string prompt) => StartCoroutine(Reply(prompt));

        private IEnumerator Reply(string prompt)
        {
            _processing = true;
            OnProcessingStarted.Invoke();
            yield return new WaitForSeconds(thinkSeconds);
            _processing = false;

            string grounded = prompt != null && prompt.Contains("cup")
                ? "Yes — I can see a cup nearby, along with a couple of other things."
                : "I can see a few objects in the room. Ask me about any of them.";
            Debug.Log("[MockLlmBackend] response generated");
            OnResponseReceived.Invoke(grounded);
        }
    }
}
