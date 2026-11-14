using System;
using System.Collections.Generic;
using UnityEngine;

namespace XRScan.Core
{
    /// <summary>How a report of an AI reply leaves the app.</summary>
    public enum ReplyReportChannel
    {
        /// <summary>A message the user's email app opens with everything filled in (needs no server).</summary>
        Email,

        /// <summary>A JSON POST to an HTTPS endpoint.</summary>
        Https,

        /// <summary>The destination cannot be used; see <see cref="ReplyReport.Error"/>.</summary>
        Unavailable
    }

    /// <summary>A report of one AI reply, ready to send.</summary>
    public sealed class ReplyReport
    {
        public ReplyReportChannel Channel { get; internal set; }

        /// <summary>Email: the mailto: URI with the subject and body filled in.</summary>
        public string MailtoUri { get; internal set; } = string.Empty;

        /// <summary>HTTPS: the endpoint as <see cref="CleartextPolicy"/> allowed it (https only in release builds).</summary>
        public string CheckedUrl { get; internal set; } = string.Empty;

        /// <summary>HTTPS: the JSON body.</summary>
        public string Json { get; internal set; } = string.Empty;

        /// <summary>Where the report goes, as the user is told: an email address or a host name.</summary>
        public string Recipient { get; internal set; } = string.Empty;

        /// <summary>Why it cannot be sent (<see cref="ReplyReportChannel.Unavailable"/> only).</summary>
        public string Error { get; internal set; } = string.Empty;
    }

    /// <summary>
    /// Builds the report the Report control sends for one AI reply (Google Play's AI-generated content policy; Meta
    /// VRC.Content.3): the user's question, the reply, the model name, the app version and a reason, nothing else.
    /// The destination comes from <see cref="CompanionConfig.replyReportDestination"/>. By default it is an email to
    /// the company's published support address, which needs no server; an https:// endpoint that takes a JSON POST
    /// works too, once there is one.
    /// </summary>
    public static class ReplyReporting
    {
        /// <summary>The company's published support address (LICENSE, docs/License-FAQ.md, the privacy policy).</summary>
        public const string SupportAddress = "support@companionintelligence.com";

        public const string DefaultDestination = "mailto:" + SupportAddress;

        /// <summary>Environment variable that overrides the destination (Editor, desktop and CI runs).</summary>
        public const string EnvDestinationKey = "XRSCAN_REPORT_DESTINATION";

        public const string AppName = "CI Spatial Companion";

        /// <summary>Longest question or reply a report carries; longer text is cut (a mailto: URI has practical limits).</summary>
        public const int MaxTextLength = 1500;

        /// <summary>What a report contains, for the confirmation the user sees before it is sent.</summary>
        public const string Contents = "your question, the reply, the model name, the app version and the reason";

        public static readonly IReadOnlyList<string> Reasons = new[]
        {
            "Offensive or hateful",
            "Sexual content",
            "Dangerous or harmful",
            "False or misleading",
            "Something else",
        };

        [Serializable]
        private class Payload
        {
            public string app;
            public string app_version;
            public string model;
            public string reason;
            public string question;
            public string reply;
        }

        public static ReplyReport Build(AiReply reply, string reason, string appVersion, string destination,
            bool isDevelopmentBuild, IEnumerable<string> devAllowlist)
        {
            var report = new ReplyReport();
            if (reply == null) return Unavailable(report, "there is no reply to report.");

            string target = string.IsNullOrWhiteSpace(destination) ? DefaultDestination : destination.Trim();
            string why = string.IsNullOrWhiteSpace(reason) ? Reasons[Reasons.Count - 1] : reason.Trim();
            string version = string.IsNullOrWhiteSpace(appVersion) ? "unknown" : appVersion.Trim();
            string question = Clip(reply.Question);
            string answer = Clip(reply.Reply);

            if (target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                string address = target.Substring("mailto:".Length);
                int query = address.IndexOf('?');
                if (query >= 0) address = address.Substring(0, query);
                address = address.Trim();
                if (!IsEmailAddress(address)) return Unavailable(report, "the report address is not an email address.");

                string subject = "Report: AI reply in " + AppName;
                string body =
                    "I am reporting an answer from " + AppName + ".\r\n\r\n" +
                    "Reason: " + why + "\r\n" +
                    "Model: " + reply.Model + "\r\n" +
                    "App version: " + version + "\r\n\r\n" +
                    "My question:\r\n" + question + "\r\n\r\n" +
                    "The answer:\r\n" + answer + "\r\n";

                report.Channel = ReplyReportChannel.Email;
                report.Recipient = address;
                report.MailtoUri = "mailto:" + address + "?subject=" + Uri.EscapeDataString(subject) +
                                   "&body=" + Uri.EscapeDataString(body);
                return report;
            }

            var check = CleartextPolicy.Evaluate(target, isDevelopmentBuild, devAllowlist, ConfigOverrides.SourceAsset);
            if (!check.IsUsable) return Unavailable(report, check.Message);

            report.Channel = ReplyReportChannel.Https;
            report.CheckedUrl = check.Url.ToString();
            report.Recipient = check.Url.Host;
            report.Json = JsonUtility.ToJson(new Payload
            {
                app = AppName,
                app_version = version,
                model = reply.Model,
                reason = why,
                question = question,
                reply = answer,
            });
            return report;
        }

        private static ReplyReport Unavailable(ReplyReport report, string error)
        {
            report.Channel = ReplyReportChannel.Unavailable;
            report.Error = error;
            return report;
        }

        private static string Clip(string text)
        {
            text = text ?? string.Empty;
            return text.Length <= MaxTextLength ? text : text.Substring(0, MaxTextLength) + "...";
        }

        private static bool IsEmailAddress(string text)
        {
            int at = text.IndexOf('@');
            if (at <= 0 || at != text.LastIndexOf('@') || at == text.Length - 1) return false;
            if (text.IndexOf('.', at) < 0) return false;
            foreach (char c in text)
            {
                if (c <= ' ' || c > '~' || c == '<' || c == '>' || c == '"' || c == ',' || c == ';' || c == '&') return false;
            }
            return true;
        }
    }
}
