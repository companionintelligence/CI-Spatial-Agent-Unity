using UnityEngine;
using XRScan.Core;

namespace XRScan.Agent
{
    /// <summary>
    /// The hybrid voice Q&amp;A loop: push-to-talk STT -&gt; LLM (grounded with the scanned-scene
    /// context from <see cref="SceneKnowledgeBase"/>) -&gt; TTS -&gt; visemes. All three stages are
    /// injected as interfaces, so any mix of server / on-device / mock backends works unchanged.
    /// </summary>
    public class ConversationOrchestrator : MonoBehaviour
    {
        [SerializeField] private SceneKnowledgeBase knowledge;
        [SerializeField] private MonoBehaviour sttBehaviour; // ISpeechToText
        [SerializeField] private MonoBehaviour llmBehaviour; // ILlmBackend
        [SerializeField] private MonoBehaviour ttsBehaviour; // ITextToSpeech

        private ISpeechToText _stt;
        private ILlmBackend _llm;
        private ITextToSpeech _tts;
        private bool _wired;

        private void Awake()
        {
            _stt = sttBehaviour as ISpeechToText;
            _llm = llmBehaviour as ILlmBackend;
            _tts = ttsBehaviour as ITextToSpeech;
        }

        public void Configure(ISpeechToText stt, ILlmBackend llm, ITextToSpeech tts, SceneKnowledgeBase kb)
        {
            Unwire();
            _stt = stt; _llm = llm; _tts = tts;
            if (kb != null) knowledge = kb;
            _stt?.Initialize(); _llm?.Initialize(); _tts?.Initialize();
            Wire();
        }

        private void OnEnable() => Wire();
        private void OnDisable() => Unwire();

        private void Wire()
        {
            if (_wired || _stt == null || _llm == null) return;
            _stt.OnTranscriptionComplete.AddListener(HandleTranscript);
            _llm.OnResponseReceived.AddListener(HandleLlmResponse);
            _wired = true;
        }

        private void Unwire()
        {
            if (!_wired) return;
            _stt?.OnTranscriptionComplete.RemoveListener(HandleTranscript);
            _llm?.OnResponseReceived.RemoveListener(HandleLlmResponse);
            _wired = false;
        }

        /// <summary>Push-to-talk toggle — bind to a controller button.</summary>
        public void ToggleListen() => _stt?.ToggleListening();

        private void HandleTranscript(string text)
        {
            string ctx = knowledge != null ? knowledge.BuildContext() : string.Empty;
            string prompt = string.IsNullOrEmpty(ctx) ? text : ctx + "\n\nUser question: " + text;
            Debug.Log("[Conversation] Q: " + text);
            _llm?.SendTextRequest(prompt);
        }

        private void HandleLlmResponse(string response)
        {
            Debug.Log("[Conversation] A: " + response);
            _tts?.SpeakText(response);
        }
    }
}
