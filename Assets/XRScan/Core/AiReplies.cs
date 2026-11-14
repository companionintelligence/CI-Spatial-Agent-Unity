using System;

namespace XRScan.Core
{
    /// <summary>One AI reply as the user received it: the question they asked, the answer and the model that wrote it.</summary>
    public sealed class AiReply
    {
        public AiReply(string question, string reply, string model)
        {
            Question = question ?? string.Empty;
            Reply = reply ?? string.Empty;
            Model = string.IsNullOrWhiteSpace(model) ? "unknown" : model.Trim();
        }

        public string Question { get; }
        public string Reply { get; }
        public string Model { get; }

        /// <summary>True once the user hid it to report it.</summary>
        public bool Hidden { get; internal set; }
    }

    /// <summary>
    /// The latest AI reply, shared between the conversation loop, which records each reply and stops speaking one
    /// that is hidden, and the report control in the settings panel, which hides and reports it (Google Play's
    /// AI-generated content policy; Meta VRC.Content.3 and 4). Only the latest reply is kept, in memory.
    /// </summary>
    public static class AiReplies
    {
        public static AiReply Last { get; private set; }

        /// <summary>Raised when the user hides a reply: whoever is speaking it stops.</summary>
        public static event Action<AiReply> HideRequested;

        public static void Record(AiReply reply)
        {
            if (reply != null) Last = reply;
        }

        public static void Hide(AiReply reply)
        {
            if (reply == null || reply.Hidden) return;
            reply.Hidden = true;
            HideRequested?.Invoke(reply);
        }

        /// <summary>Forgets the latest reply.</summary>
        public static void Clear() => Last = null;
    }
}
