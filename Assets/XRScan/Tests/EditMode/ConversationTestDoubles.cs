using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using XRScan.Core;

namespace XRScan.Tests.EditMode
{
    /// <summary>Speech-to-text that the test drives by invoking <see cref="OnTranscriptionComplete"/>.</summary>
    internal sealed class FakeStt : ISpeechToText
    {
        public bool IsInitialized => true;
        public bool IsListening => false;
        public bool IsProcessing => false;
        public UnityEvent OnListeningStarted { get; } = new UnityEvent();
        public UnityEvent OnListeningStopped { get; } = new UnityEvent();
        public UnityEvent<string> OnTranscriptionComplete { get; } = new StringUnityEvent();
        public UnityEvent<string> OnError { get; } = new StringUnityEvent();
        public void Initialize() { }
        public void StartListening() { }
        public void StopListening() { }
        public void ToggleListening() { }
    }

    /// <summary>
    /// A language model that records every prompt it is sent; the test answers by invoking <see cref="OnResponseReceived"/>,
    /// and sets <see cref="IsProcessing"/> while the model is meant to be still writing a reply.
    /// </summary>
    internal class FakeLlm : ILlmBackend
    {
        public readonly List<string> Prompts = new List<string>();
        public string LastPrompt => Prompts.Count > 0 ? Prompts[Prompts.Count - 1] : null;
        public bool IsInitialized => true;
        public bool IsConnected => true;
        public bool IsProcessing { get; set; }
        public string ServerAddress => "test";
        public string ModelName => "test-model:1b";
        public UnityEvent OnProcessingStarted { get; } = new UnityEvent();
        public UnityEvent<string> OnResponseReceived { get; } = new StringUnityEvent();
        public UnityEvent<string> OnError { get; } = new StringUnityEvent();
        public void Initialize() { }
        public void SendTextRequest(string prompt) => Prompts.Add(prompt);
        public void SendVisionRequest(Texture2D image, string prompt) => Prompts.Add(prompt);
    }

    /// <summary>Text-to-speech that records every line it is asked to speak.</summary>
    internal sealed class FakeTts : ITextToSpeech
    {
        public readonly List<string> Lines = new List<string>();
        public int Stops;
        public string Spoken => Lines.Count > 0 ? Lines[Lines.Count - 1] : null;
        public bool IsInitialized => true;
        public bool IsPlaying => false;
        public AudioSource OutputSource => null;
        public UnityEvent OnSpeechStarted { get; } = new UnityEvent();
        public UnityEvent OnSpeechCompleted { get; } = new UnityEvent();
        public UnityEvent<string> OnError { get; } = new StringUnityEvent();
        public void Initialize() { }
        public void SpeakText(string text) => Lines.Add(text);
        public void StopSpeaking() => Stops++;
        public void SetVoiceSettings(string voice, float speed, float pitch) { }
    }

    /// <summary>
    /// A reply check the test controls: it records what it is asked, then answers <see cref="Answer"/> at once, holds the
    /// callback in <see cref="Pending"/> when <see cref="Hold"/> is set, or throws when <see cref="Throw"/> is set.
    /// </summary>
    internal sealed class FakeChecker : IReplyChecker
    {
        public readonly List<KeyValuePair<string, string>> Calls = new List<KeyValuePair<string, string>>();
        public ReplyCheckResult Answer = ReplyCheckResult.Safe();
        public bool Hold;
        public bool Throw;
        public Action<ReplyCheckResult> Pending;

        public string CheckerName => "fake-guardian:1b";
        public float TimeoutSeconds { get; set; } = 10f;

        public void Check(string question, string reply, Action<ReplyCheckResult> done)
        {
            Calls.Add(new KeyValuePair<string, string>(question, reply));
            if (Throw) throw new InvalidOperationException("checker failed");
            if (Hold)
            {
                Pending = done;
                return;
            }
            done(Answer);
        }
    }
}
