using System;

namespace XRScan.Core
{
    /// <summary>
    /// Holds a language-model reply back until it has been checked, and fails closed: a reply is released only when the
    /// check answers <see cref="ReplyVerdict.Safe"/> before the deadline. The on-device rules
    /// (<see cref="LocalReplyRules"/>) run first, whatever the checker. A flagged reply, an error, an exception, a
    /// timeout, a missing checker or a missing answer delivers a withheld result instead; an answer that comes late or a
    /// second time is ignored. A newer reply replaces one that is still being checked, which is then never released.
    /// The owner calls <see cref="Tick"/> every frame so the deadline applies even when the checker never answers.
    /// </summary>
    public sealed class ReplyGate
    {
        /// <summary>Why every reply is withheld when the language-model backend brings no reply check.</summary>
        public const string NoCheckerReason = "this language model backend has no reply safety check.";

        private sealed class Pending
        {
            public int Ticket;
            public double Deadline;
            public float Timeout;
            public Action<ReplyCheckResult> Deliver;
        }

        private readonly IReplyChecker _checker;
        private readonly Func<double> _clock;
        private int _ticket;
        private Pending _pending;

        /// <param name="checker">The reply check; null withholds every reply.</param>
        /// <param name="clock">Seconds, for the deadline (the owner's real-time clock; tests pass their own).</param>
        public ReplyGate(IReplyChecker checker, Func<double> clock)
        {
            _checker = checker;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>True while a reply waits for its check.</summary>
        public bool IsChecking => _pending != null;

        /// <summary>
        /// Checks <paramref name="reply"/> and calls <paramref name="deliver"/> once with the outcome: at once for the
        /// on-device rules, a missing checker or a checker that throws, otherwise when the checker answers or
        /// <see cref="Tick"/> finds the deadline passed.
        /// </summary>
        public void Submit(string question, string reply, Action<ReplyCheckResult> deliver)
        {
            if (deliver == null) throw new ArgumentNullException(nameof(deliver));

            int ticket = ++_ticket;
            _pending = null; // a reply still being checked is superseded and never released

            if (LocalReplyRules.Blocks(reply, out string rule))
            {
                deliver(ReplyCheckResult.Blocked(rule));
                return;
            }
            if (_checker == null)
            {
                deliver(ReplyCheckResult.Unavailable(NoCheckerReason));
                return;
            }

            var pending = new Pending
            {
                Ticket = ticket, Deadline = double.MaxValue, Timeout = ReplySafety.MinTimeoutSeconds, Deliver = deliver
            };
            _pending = pending;
            try
            {
                pending.Timeout = ReplySafety.ClampTimeout(_checker.TimeoutSeconds);
                pending.Deadline = _clock() + pending.Timeout;
                _checker.Check(question ?? string.Empty, reply ?? string.Empty,
                    result => Complete(ticket, result ?? ReplyCheckResult.Unavailable("the safety check gave no answer.")));
            }
            catch (Exception e)
            {
                Complete(ticket, ReplyCheckResult.Unavailable("the safety check failed (" + e.GetType().Name + ")."));
            }
        }

        /// <summary>Withholds the reply being checked once its deadline has passed.</summary>
        public void Tick()
        {
            var pending = _pending;
            if (pending == null || _clock() < pending.Deadline) return;
            Complete(pending.Ticket, ReplyCheckResult.Unavailable(
                "the safety check did not answer within " + pending.Timeout.ToString("0.#") + " seconds."));
        }

        private void Complete(int ticket, ReplyCheckResult result)
        {
            var pending = _pending;
            if (pending == null || pending.Ticket != ticket) return; // late, repeated or superseded
            _pending = null;
            pending.Deliver(result);
        }
    }
}
