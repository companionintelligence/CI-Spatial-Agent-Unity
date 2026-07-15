using UnityEngine;
using UnityEngine.Events;

namespace XRScan.Core
{
    /// <summary>
    /// Speech synthesis seam. The synthesized audio plays through <see cref="OutputSource"/>,
    /// which the viseme / lip-sync system reads to drive the avatar's mouth blendshapes.
    /// Default impl is on-device (Piper); a cloud impl and a mock are also provided.
    /// Generalized from CI-Spatial's IXREALTextToSpeech.
    /// </summary>
    public interface ITextToSpeech
    {
        bool IsInitialized { get; }
        bool IsPlaying { get; }

        /// <summary>The AudioSource the synth plays through; uLipSync attaches here for visemes.</summary>
        AudioSource OutputSource { get; }

        UnityEvent OnSpeechStarted { get; }
        UnityEvent OnSpeechCompleted { get; }
        UnityEvent<string> OnError { get; }

        void Initialize();
        void SpeakText(string text);
        void StopSpeaking();
        void SetVoiceSettings(string voice, float speed, float pitch);
    }
}
