using System;

namespace XRScan.Core
{
    /// <summary>
    /// Hosts of third-party cloud AI / speech providers. Speech audio and text may only go to one
    /// of these after the explicit cloud opt-in in <see cref="VoiceConfig"/>, even when such a host
    /// is entered as the "self-hosted" endpoint by mistake.
    /// </summary>
    public static class CloudProviderHosts
    {
        private static readonly string[] Domains =
        {
            "openai.com",
            "azure.com",          // Azure OpenAI / Cognitive Services speech
            "googleapis.com",
            "amazonaws.com",
            "elevenlabs.io",
            "deepgram.com",
            "assemblyai.com",
            "groq.com",
            "anthropic.com",
            "together.xyz",
            "fireworks.ai",
            "speechmatics.com",
            "rev.ai",
            "cartesia.ai",
            "play.ht",
            "openrouter.ai",
            "mistral.ai",
            "cohere.com",
            "huggingface.co",
            "replicate.com",
        };

        /// <summary>True when <paramref name="host"/> is (a subdomain of) a known cloud provider.</summary>
        public static bool IsKnownCloudHost(string host)
        {
            string h = EndpointUrl.NormalizeHost(host);
            if (string.IsNullOrEmpty(h)) return false;
            foreach (var domain in Domains)
            {
                if (h == domain || h.EndsWith("." + domain, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
