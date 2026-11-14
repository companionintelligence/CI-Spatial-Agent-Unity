using System;
using System.Collections.Generic;

namespace XRScan.Core
{
    /// <summary>What a check of one language-model reply decided.</summary>
    public enum ReplyVerdict
    {
        /// <summary>Nothing was flagged: the reply may be shown and spoken.</summary>
        Safe,

        /// <summary>Flagged: the reply is withheld and the companion says <see cref="ReplySafety.BlockedLine"/>.</summary>
        Blocked,

        /// <summary>
        /// No clear answer (an error, a timeout, a missing model, an answer the app cannot read): the reply is withheld
        /// and the companion says <see cref="ReplySafety.UnavailableLine"/>.
        /// </summary>
        Unavailable
    }

    /// <summary>The outcome of checking one reply. It never holds the reply or the question.</summary>
    public sealed class ReplyCheckResult
    {
        private ReplyCheckResult(ReplyVerdict verdict, string flag, string reason)
        {
            Verdict = verdict;
            Flag = flag ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public ReplyVerdict Verdict { get; }

        /// <summary>Blocked: what flagged the reply (a Granite Guardian risk name or an on-device rule).</summary>
        public string Flag { get; }

        /// <summary>Unavailable: why the check gave no answer, as the user is told (settings panel, notice) and logged.</summary>
        public string Reason { get; }

        public static ReplyCheckResult Safe() => new ReplyCheckResult(ReplyVerdict.Safe, null, null);

        public static ReplyCheckResult Blocked(string flag) =>
            new ReplyCheckResult(ReplyVerdict.Blocked, string.IsNullOrWhiteSpace(flag) ? "unsafe" : flag.Trim(), null);

        public static ReplyCheckResult Unavailable(string reason) =>
            new ReplyCheckResult(ReplyVerdict.Unavailable, null,
                string.IsNullOrWhiteSpace(reason) ? "the safety check gave no answer." : reason.Trim());

        /// <summary>
        /// The checks of one reply (one per risk) as one result: blocked when any of them is blocked, safe only when every
        /// one of them is safe, unavailable otherwise (a missing result counts as unavailable, and so does an empty list).
        /// </summary>
        public static ReplyCheckResult Combine(IReadOnlyList<ReplyCheckResult> results)
        {
            if (results == null || results.Count == 0) return Unavailable("no check ran.");

            ReplyCheckResult unavailable = null;
            foreach (var result in results)
            {
                if (result == null)
                {
                    unavailable = unavailable ?? Unavailable(null);
                    continue;
                }
                if (result.Verdict == ReplyVerdict.Blocked) return result;
                if (result.Verdict == ReplyVerdict.Unavailable && unavailable == null) unavailable = result;
            }
            return unavailable ?? Safe();
        }

        public override string ToString() =>
            Verdict == ReplyVerdict.Blocked ? "Blocked (" + Flag + ")"
            : Verdict == ReplyVerdict.Unavailable ? "Unavailable (" + Reason + ")"
            : "Safe";
    }

    /// <summary>
    /// Checks a language-model reply before the app shows or speaks it (Google Play gate 11 and Meta gate 12 of the
    /// listing sheets). <see cref="ReplyGate"/> runs it and fails closed around it: only a <see cref="ReplyVerdict.Safe"/>
    /// answer within <see cref="TimeoutSeconds"/> releases a reply. The default is the language-model backend's own
    /// check, <c>OllamaBackend</c>: IBM Granite Guardian on the same Ollama server.
    /// </summary>
    public interface IReplyChecker
    {
        /// <summary>What checks the replies, as the settings panel names it (the safety model).</summary>
        string CheckerName { get; }

        /// <summary>How long a check may take before the reply is withheld.</summary>
        float TimeoutSeconds { get; }

        /// <summary>
        /// Checks <paramref name="reply"/>, the answer to <paramref name="question"/> (the user's own words, without the
        /// scene context), and sends nothing else. Calls <paramref name="done"/> once, on the main thread; the gate
        /// ignores any later call.
        /// </summary>
        void Check(string question, string reply, Action<ReplyCheckResult> done);
    }
}
