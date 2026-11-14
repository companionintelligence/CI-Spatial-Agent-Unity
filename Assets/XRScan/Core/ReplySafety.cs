using System;

namespace XRScan.Core
{
    /// <summary>
    /// What the companion says instead of a reply it withholds, and the outcome of the latest reply check, which the
    /// settings panel shows next to the name of the safety model. The lines are fixed text written here, never model
    /// output, so they need no check of their own.
    /// </summary>
    public static class ReplySafety
    {
        /// <summary>Said instead of a reply the check flagged.</summary>
        public const string BlockedLine = "I can't give that answer. Please ask me something else.";

        /// <summary>Said instead of a reply the check could not judge (error, timeout, missing model, unreadable answer).</summary>
        public const string UnavailableLine =
            "I can't answer right now because my safety check isn't working. Open Settings to see why.";

        /// <summary>Shortest and longest time a check may take (a configured value outside this range is clamped).</summary>
        public const float MinTimeoutSeconds = 1f;
        public const float MaxTimeoutSeconds = 120f;

        /// <summary>The outcome of the latest check, or null before the first reply.</summary>
        public static ReplyCheckResult Last { get; private set; }

        /// <summary>Raised after each check, for UI that shows <see cref="Last"/>.</summary>
        public static event Action Changed;

        public static void Record(ReplyCheckResult result)
        {
            if (result == null) return;
            Last = result;
            Changed?.Invoke();
        }

        /// <summary>Forgets the latest outcome.</summary>
        public static void Clear() => Last = null;

        /// <summary>A configured timeout within <see cref="MinTimeoutSeconds"/> and <see cref="MaxTimeoutSeconds"/>.</summary>
        public static float ClampTimeout(float seconds)
        {
            if (float.IsNaN(seconds) || seconds < MinTimeoutSeconds) return MinTimeoutSeconds;
            return seconds > MaxTimeoutSeconds ? MaxTimeoutSeconds : seconds;
        }

        /// <summary>The notice shown with <see cref="UnavailableLine"/>, naming why (for example the missing model).</summary>
        public static string UnavailableNotice(ReplyCheckResult result) =>
            "Replies are withheld: " + (result != null && result.Reason.Length > 0 ? result.Reason : "the safety check is not working.") +
            " Settings shows the safety check.";
    }
}
