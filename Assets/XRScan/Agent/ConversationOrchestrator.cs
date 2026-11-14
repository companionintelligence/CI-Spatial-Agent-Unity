using System;
using UnityEngine;
using XRScan.Core;

namespace XRScan.Agent
{
    /// <summary>
    /// The hybrid voice Q&amp;A loop: push-to-talk STT -&gt; LLM (grounded with the scanned-scene
    /// context from <see cref="SceneKnowledgeBase"/>) -&gt; reply check -&gt; TTS -&gt; visemes. All stages are
    /// injected as interfaces, so any mix of server / on-device / mock backends works unchanged.
    /// <para>Every reply passes the reply check before it is spoken or recorded (<see cref="ReplyGate"/>): the
    /// on-device rules, then the language-model backend's <see cref="IReplyChecker"/> (OllamaBackend: IBM Granite
    /// Guardian on the same server), with only the user's question and the reply. A flagged reply is replaced by
    /// <see cref="ReplySafety.BlockedLine"/>; a reply the check cannot judge (error, timeout, missing model, unreadable
    /// answer, no checker at all) by <see cref="ReplySafety.UnavailableLine"/>, which points to Settings. A withheld
    /// reply is never spoken, shown, logged or kept.</para>
    /// <para>Each reply that passes is recorded in <see cref="AiReplies"/> with its question and model, so the user can
    /// report it; a reply the user hides stops being spoken. A withheld reply is not recorded, so the last reply that
    /// was spoken stays the one the user can report. One question goes to the model at a time: a question asked while
    /// it is still answering is not sent, so a reply is never checked or recorded with a question it did not answer.</para>
    /// </summary>
    public class ConversationOrchestrator : MonoBehaviour
    {
        [SerializeField] private SceneKnowledgeBase knowledge;
        [SerializeField] private MonoBehaviour sttBehaviour; // ISpeechToText
        [SerializeField] private MonoBehaviour llmBehaviour; // ILlmBackend (and normally IReplyChecker)
        [SerializeField] private MonoBehaviour ttsBehaviour; // ITextToSpeech

        private ISpeechToText _stt;
        private ILlmBackend _llm;
        private ITextToSpeech _tts;
        private IReplyChecker _checker;
        private ReplyGate _gate;
        private bool _wired;
        private string _question = string.Empty;

        /// <summary>Seconds on which the reply check's deadline runs (real time; tests set their own).</summary>
        public Func<double> Clock { get; set; } = () => Time.realtimeSinceStartupAsDouble;

        /// <summary>True while a reply waits for its check.</summary>
        public bool IsCheckingReply => _gate != null && _gate.IsChecking;

        private void Awake()
        {
            _stt = sttBehaviour as ISpeechToText;
            _llm = llmBehaviour as ILlmBackend;
            _tts = ttsBehaviour as ITextToSpeech;
            UseChecker(llmBehaviour as IReplyChecker);
        }

        /// <param name="checker">The reply check; null takes the LLM backend's own. Without either, every reply is withheld.</param>
        public void Configure(ISpeechToText stt, ILlmBackend llm, ITextToSpeech tts, SceneKnowledgeBase kb, IReplyChecker checker = null)
        {
            Unwire();
            _stt = stt; _llm = llm; _tts = tts;
            if (kb != null) knowledge = kb;
            UseChecker(checker ?? llm as IReplyChecker);
            _stt?.Initialize(); _llm?.Initialize(); _tts?.Initialize();
            Wire();
        }

        private void UseChecker(IReplyChecker checker)
        {
            _checker = checker;
            _gate = new ReplyGate(_checker, () => Clock());
        }

        private void OnEnable() => Wire();
        private void OnDisable() => Unwire();

        private void Update() => CheckReplyDeadline();

        /// <summary>Withholds a reply whose check has run out of time (called every frame).</summary>
        public void CheckReplyDeadline() => _gate?.Tick();

        private void Wire()
        {
            if (_wired || _stt == null || _llm == null) return;
            _stt.OnTranscriptionComplete.AddListener(HandleTranscript);
            _llm.OnResponseReceived.AddListener(HandleLlmResponse);
            AiReplies.HideRequested += StopSpeakingHiddenReply;
            _wired = true;
        }

        private void Unwire()
        {
            if (!_wired) return;
            _stt?.OnTranscriptionComplete.RemoveListener(HandleTranscript);
            _llm?.OnResponseReceived.RemoveListener(HandleLlmResponse);
            AiReplies.HideRequested -= StopSpeakingHiddenReply;
            _wired = false;
        }

        /// <summary>Push-to-talk toggle — bind to a controller button.</summary>
        public void ToggleListen() => _stt?.ToggleListening();

        /// <summary>
        /// Sends the user's question to the model, one at a time. A question that arrives while the model is still
        /// answering is not sent (the backend would drop it anyway) and does not replace the question kept for the reply,
        /// so the reply that comes is checked and recorded with the question it answers.
        /// </summary>
        private void HandleTranscript(string text)
        {
            if (_llm == null) return;
            if (_llm.IsProcessing)
            {
                Debug.Log("[Conversation] The model is still answering the last question; this one was not sent.");
                return;
            }

            _question = text ?? string.Empty;
            string ctx = knowledge != null ? knowledge.BuildContext() : string.Empty;
            string prompt = string.IsNullOrEmpty(ctx) ? text : ctx + "\n\nUser question: " + text;
            Debug.Log("[Conversation] Q: " + text);
            _llm.SendTextRequest(prompt);
        }

        /// <summary>Every reply of the LLM backend, text or vision request alike, arrives here and goes to the check.</summary>
        private void HandleLlmResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                Debug.Log("[Conversation] The model sent an empty reply; nothing to say.");
                return;
            }

            string question = _question;
            string model = _llm?.ModelName;
            if (_gate == null) UseChecker(_checker);
            _gate.Submit(question, response, result => Deliver(question, response, model, result));
        }

        private void Deliver(string question, string reply, string model, ReplyCheckResult result)
        {
            ReplySafety.Record(result);
            switch (result.Verdict)
            {
                case ReplyVerdict.Safe:
                    Debug.Log("[Conversation] A: " + reply);
                    AiReplies.Record(new AiReply(question, reply, model));
                    _tts?.SpeakText(reply);
                    return;

                case ReplyVerdict.Blocked:
                    Debug.Log("[Conversation] Reply withheld: flagged by " + result.Flag + ".");
                    _tts?.SpeakText(ReplySafety.BlockedLine);
                    return;

                default:
                    Debug.LogWarning("[Conversation] Reply withheld: the safety check is unavailable (" + result.Reason + ")");
                    _tts?.SpeakText(ReplySafety.UnavailableLine);
                    UserNotices.Post(ReplySafety.UnavailableNotice(result));
                    return;
            }
        }

        private void StopSpeakingHiddenReply(AiReply reply) => _tts?.StopSpeaking();
    }
}
