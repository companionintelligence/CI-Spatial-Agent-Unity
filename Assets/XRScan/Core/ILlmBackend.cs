using UnityEngine;
using UnityEngine.Events;

namespace XRScan.Core
{
    /// <summary>
    /// The pluggable intelligence seam the whole app rotates on. Swap the concrete
    /// backend (Ollama / OpenAI / Claude / Companion Intelligence server) via config
    /// with no recompile. Vision requests are only issued on platforms where the
    /// camera provider reports <see cref="ICameraProvider.SupportsRgbFrames"/>.
    /// Generalized from CI-Spatial's IXREALOllamaConnector.
    /// </summary>
    public interface ILlmBackend
    {
        bool IsInitialized { get; }
        bool IsConnected { get; }
        bool IsProcessing { get; }
        string ServerAddress { get; }

        UnityEvent OnProcessingStarted { get; }
        UnityEvent<string> OnResponseReceived { get; }
        UnityEvent<string> OnError { get; }

        void Initialize();
        void SendTextRequest(string prompt);
        void SendVisionRequest(Texture2D image, string prompt);
    }
}
