using System;
using System.Collections.Generic;
using UnityEngine;

namespace XRScan.Core
{
    /// <summary>The settings the in-headset panel can show or change.</summary>
    public enum SettingId
    {
        LlmEndpoint,
        LlmModel,
        SpeechServer,
        SpeechProvider,
        CloudOptIn,
        CvServer,
        DevCleartextHosts,
        ResetAll,
        CvToken,
        ReportReply
    }

    /// <summary>How a setting is changed: typed endpoint, cycled choice, confirmed toggle, one-shot action, read-only,
    /// or typed secret (shown only as set / not set).</summary>
    public enum SettingKind
    {
        Endpoint,
        Choice,
        Toggle,
        Action,
        Info,
        Secret
    }

    /// <summary>
    /// How the panel opens the text editor for a row: the text it starts from, and whether the system keyboard hides what
    /// is typed. A secret starts empty and hidden, so the stored value is never on screen.
    /// </summary>
    public sealed class TextEntry
    {
        public TextEntry(string initialText, bool secure, bool url, string placeholder)
        {
            InitialText = initialText ?? string.Empty;
            Secure = secure;
            Url = url;
            Placeholder = placeholder ?? string.Empty;
        }

        /// <summary>The text the editor starts from: the current address for an endpoint, empty for a secret.</summary>
        public string InitialText { get; }

        /// <summary>True for a secret: the system keyboard opens in its secure (password) mode.</summary>
        public bool Secure { get; }

        /// <summary>True for an address: the system keyboard opens with its URL layout.</summary>
        public bool Url { get; }

        public string Placeholder { get; }
    }

    /// <summary>One display row of the settings panel.</summary>
    public sealed class SettingRow
    {
        public SettingRow(SettingId id, SettingKind kind, string label, string value, string detail, bool isWarning)
        {
            Id = id;
            Kind = kind;
            Label = label;
            Value = value ?? string.Empty;
            Detail = detail ?? string.Empty;
            IsWarning = isWarning;
        }

        public SettingId Id { get; }
        public SettingKind Kind { get; }
        public string Label { get; }
        public string Value { get; }
        public string Detail { get; }
        public bool IsWarning { get; }
    }

    /// <summary>
    /// The logic behind the in-headset settings panel, free of UI so it is unit-testable. Every change is a
    /// PlayerPrefs override (through <see cref="ConfigOverrides"/>) layered on top of the config assets
    /// referenced by <see cref="CompanionConfig"/>; <see cref="ResetAll"/> removes them again.
    /// Endpoints are validated against the same transport policy the backends enforce, so the panel
    /// cannot store an http:// endpoint a release build would refuse anyway, and the cloud speech opt-in
    /// needs an explicit confirmation.
    /// </summary>
    public sealed class CompanionSettingsModel
    {
        public const string NotSet = "not set";

        /// <summary>Every PlayerPrefs key the panel may write (secrets included, so a reset also forgets them).</summary>
        public static readonly string[] OverrideKeys =
        {
            LlmConfig.PlayerPrefsEndpointKey,
            LlmConfig.PlayerPrefsHostKey,
            LlmConfig.PlayerPrefsModelKey,
            VoiceConfig.PlayerPrefsBaseUrlKey,
            VoiceConfig.PlayerPrefsProviderKey,
            VoiceConfig.PlayerPrefsCloudOptInKey,
            VoiceConfig.PlayerPrefsApiKey,
            VoiceConfig.PlayerPrefsSelfHostedTokenKey,
            ServerConfig.PlayerPrefsBaseUrlKey,
            ServerConfig.PlayerPrefsApiTokenKey,
        };

        /// <summary>Longest secret the panel stores (the CV server's tokens are 43 characters).</summary>
        public const int MaxSecretLength = 512;

        private readonly CompanionConfig _config;
        private readonly bool _isDevelopmentBuild;
        private readonly IReadOnlyList<string> _devAllowlist;

        public CompanionSettingsModel(CompanionConfig config)
            : this(config, CleartextPolicy.IsDevelopmentBuild, CleartextPolicy.DevAllowlist)
        {
        }

        public CompanionSettingsModel(CompanionConfig config, bool isDevelopmentBuild, IReadOnlyList<string> devAllowlist)
        {
            _config = config ? config : throw new ArgumentNullException(nameof(config));
            _isDevelopmentBuild = isDevelopmentBuild;
            _devAllowlist = devAllowlist ?? Array.Empty<string>();
        }

        public bool IsDevelopmentBuild => _isDevelopmentBuild;

        /// <summary>True after the first activation of the cloud opt-in, until it is confirmed or cancelled.</summary>
        public bool CloudOptInPending { get; private set; }

        /// <summary>
        /// Sends a report the user confirmed (opens the email app, or posts it) and returns what to tell the user.
        /// The settings panel sets it; without it reports cannot be sent.
        /// </summary>
        public Func<ReplyReport, string> SendReport { get; set; }

        /// <summary>The app version a report names.</summary>
        public string AppVersion { get; set; } = Application.version;

        /// <summary>True after the first Report press hid the reply, until the report is sent or cancelled.</summary>
        public bool ReportPending => _reportedReply != null;

        /// <summary>The reason the pending report gives (left / right changes it).</summary>
        public string ReportReason => ReplyReporting.Reasons[_reasonIndex];

        private AiReply _reportedReply;
        private int _reasonIndex;

        public List<SettingRow> BuildRows()
        {
            var rows = new List<SettingRow>();
            rows.Add(ReportRow());

            var llm = _config.llm;
            if (llm != null)
            {
                rows.Add(EndpointRow(SettingId.LlmEndpoint, "LLM server", llm.ResolveEndpoint(_isDevelopmentBuild, _devAllowlist)));
                rows.Add(new SettingRow(SettingId.LlmModel, SettingKind.Choice, "LLM model", llm.model,
                    "A model on your own server; change it with left / right.", false));
            }

            var voice = _config.voice;
            if (voice != null)
            {
                bool cloud = voice.EffectiveProvider == SpeechProvider.Cloud;
                var stt = voice.ResolveSttEndpoint(_isDevelopmentBuild, _devAllowlist);
                string baseUrl = voice.ResolveSelfHostedBaseUrl(out _);

                string speechDetail = cloud
                    ? "Not used while the cloud provider is selected."
                    : stt.IsUsable ? "OK: " + stt.Url : stt.Message;
                rows.Add(new SettingRow(SettingId.SpeechServer, SettingKind.Endpoint, "Speech server",
                    string.IsNullOrEmpty(baseUrl) ? NotSet : baseUrl, speechDetail, !cloud && !stt.IsUsable));

                rows.Add(new SettingRow(SettingId.SpeechProvider, SettingKind.Choice, "Speech provider",
                    cloud ? "Cloud (third party)" : "Self-hosted",
                    cloud ? "Audio and text leave your network." : "Audio and text stay on your own server.", cloud));

                rows.Add(CloudOptInRow(voice, cloud));
            }

            var server = _config.server;
            if (server != null)
            {
                rows.Add(EndpointRow(SettingId.CvServer, "Vision (CV) server", server.ResolveEndpoint(_isDevelopmentBuild, _devAllowlist)));
                bool hasToken = server.HasApiToken;
                rows.Add(new SettingRow(SettingId.CvToken, SettingKind.Secret, "Vision server token", hasToken ? "set" : NotSet,
                    hasToken
                        ? "Sent only to the vision server, with each scan. A: replace it. Reset to defaults removes it."
                        : "The vision server answers only with its API token (CV_SERVER_API_TOKEN on the server).",
                    !hasToken));
            }

            rows.Add(new SettingRow(SettingId.DevCleartextHosts, SettingKind.Info, "Plain-HTTP hosts",
                _isDevelopmentBuild ? (_devAllowlist.Count == 0 ? "none" : string.Join(", ", _devAllowlist)) : "none",
                _isDevelopmentBuild
                    ? "Development build: http:// only for these LAN hosts (CompanionConfig)."
                    : "Release build: https only.",
                false));

            rows.Add(new SettingRow(SettingId.ResetAll, SettingKind.Action, "Reset to defaults", string.Empty,
                "Removes every change made here (and any stored key or token).", false));
            return rows;
        }

        /// <summary>
        /// Text to pre-fill the editor with for an endpoint row (the effective value, possibly blocked). A secret row starts
        /// empty: the stored secret is never put back on screen or into the keyboard.
        /// </summary>
        public string GetEditableText(SettingId id)
        {
            switch (id)
            {
                case SettingId.LlmEndpoint:
                    return _config.llm != null ? UrlOf(_config.llm.ResolveEndpoint(_isDevelopmentBuild, _devAllowlist)) : string.Empty;
                case SettingId.SpeechServer:
                    return _config.voice != null ? _config.voice.ResolveSelfHostedBaseUrl(out _) : string.Empty;
                case SettingId.CvServer:
                    return _config.server != null ? UrlOf(_config.server.ResolveEndpoint(_isDevelopmentBuild, _devAllowlist)) : string.Empty;
                default:
                    return string.Empty;
            }
        }

        /// <summary>True for a row whose value is a secret (typed hidden, shown only as set / not set).</summary>
        public static bool IsSecret(SettingId id) => id == SettingId.CvToken;

        /// <summary>
        /// How the panel opens the editor for <paramref name="id"/>: a secret opens the system keyboard in secure mode with
        /// an empty buffer; an endpoint opens it with the URL layout, pre-filled with the current address.
        /// </summary>
        public TextEntry BeginTextEntry(SettingId id)
        {
            if (IsSecret(id)) return new TextEntry(string.Empty, true, false, "API token");
            return new TextEntry(GetEditableText(id), false, true, "https://host:port");
        }

        /// <summary>
        /// Stores a secret typed in the panel (the vision server's API token) as a PlayerPrefs override. The editor starts
        /// empty, so blank text keeps the stored token; Reset to defaults removes it. The message never contains the secret.
        /// </summary>
        public bool TrySetSecret(SettingId id, string text, out string message)
        {
            if (id != SettingId.CvToken)
            {
                message = "Not a secret setting.";
                return false;
            }

            string value = text == null ? string.Empty : text.Trim();
            if (value.Length == 0)
            {
                message = ConfigOverrides.GetOverride(ServerConfig.PlayerPrefsApiTokenKey) != null
                    ? "Nothing typed, so the vision server token is unchanged. Reset to defaults removes it."
                    : "Nothing typed; no vision server token is set.";
                return true;
            }

            if (value.Length > MaxSecretLength)
            {
                message = "That token is too long (at most " + MaxSecretLength + " characters).";
                return false;
            }

            foreach (char c in value)
            {
                if (c <= ' ' || c > '~')
                {
                    message = "A token has no spaces or special characters; check what you typed.";
                    return false;
                }
            }

            ConfigOverrides.SetOverride(ServerConfig.PlayerPrefsApiTokenKey, value);
            message = "Vision server token saved. It is sent only to the vision server.";
            return true;
        }

        /// <summary>
        /// Validates and stores an endpoint override. Blank text removes the override (the config asset / environment
        /// value applies again). Returns false, with the reason in <paramref name="message"/>, for text that is not an
        /// http(s) URL or that the transport policy would block (plain http:// outside the development allowlist).
        /// </summary>
        public bool TrySetEndpoint(SettingId id, string text, out string message)
        {
            string key = PrefsKeyFor(id);
            if (key == null)
            {
                message = "Not an endpoint setting.";
                return false;
            }

            string value = text == null ? string.Empty : text.Trim();
            if (value.Length == 0)
            {
                ConfigOverrides.SetOverride(key, null);
                if (id == SettingId.LlmEndpoint) ConfigOverrides.SetOverride(LlmConfig.PlayerPrefsHostKey, null);
                message = "Override removed; the configured default applies." + RestartHint(id);
                return true;
            }

            var check = CleartextPolicy.Evaluate(value, _isDevelopmentBuild, _devAllowlist, ConfigOverrides.SourcePlayerPrefs);
            if (check.Status == EndpointStatus.Invalid || check.Status == EndpointStatus.CleartextBlocked)
            {
                message = check.Message;
                return false;
            }

            ConfigOverrides.SetOverride(key, check.Url.ToString());
            if (id == SettingId.LlmEndpoint) ConfigOverrides.SetOverride(LlmConfig.PlayerPrefsHostKey, null); // the URL supersedes the legacy host

            message = "Saved " + check.Url + "." + RestartHint(id);
            if (id == SettingId.SpeechServer && CloudProviderHosts.IsKnownCloudHost(check.Url.Host))
                message += " That is a third-party cloud provider: speech stays off unless you enable the cloud opt-in.";
            return true;
        }

        /// <summary>Left / right on a row. Returns a user-facing notice, or null when nothing changed.</summary>
        public string Cycle(SettingId id, int direction)
        {
            switch (id)
            {
                case SettingId.LlmModel: return CycleModel(direction);
                case SettingId.SpeechProvider: return ToggleProvider();
                case SettingId.ReportReply: return CycleReason(direction);
                default: return null;
            }
        }

        /// <summary>Primary button on a non-endpoint row. Returns a user-facing notice, or null when nothing changed.</summary>
        public string Activate(SettingId id)
        {
            switch (id)
            {
                case SettingId.LlmModel: return CycleModel(1);
                case SettingId.SpeechProvider: return ToggleProvider();
                case SettingId.CloudOptIn: return ActivateCloudOptIn();
                case SettingId.ReportReply: return ActivateReport();
                case SettingId.ResetAll:
                    ResetAll();
                    return "All in-headset changes were removed." + " Restart the app to apply them everywhere.";
                default: return null;
            }
        }

        /// <summary>
        /// Cancels a pending (unconfirmed) cloud opt-in or report, e.g. when the selection moves away. A reply hidden
        /// for a report stays hidden.
        /// </summary>
        public void CancelPending()
        {
            CloudOptInPending = false;
            _reportedReply = null;
        }

        /// <summary>Removes every override written by the panel.</summary>
        public void ResetAll()
        {
            ConfigOverrides.ClearOverrides(OverrideKeys);
            CancelPending();
        }

        /// <summary>
        /// The Report control. The first press hides the latest AI reply at once (it stops being spoken) and shows
        /// what a report sends and where; left / right picks the reason; a second press sends it through
        /// <see cref="SendReport"/>.
        /// </summary>
        private SettingRow ReportRow()
        {
            const string label = "Report last reply";
            if (_reportedReply != null)
            {
                var report = BuildReport(_reportedReply);
                string detail = report.Channel == ReplyReportChannel.Unavailable
                    ? "It cannot be sent: " + report.Error
                    : "Sends " + ReplyReporting.Contents + " (model " + _reportedReply.Model + ", version " + AppVersion +
                      ") to " + report.Recipient + ". Left / right: reason. A: send. B: cancel.";
                return new SettingRow(SettingId.ReportReply, SettingKind.Choice, label, "reason: " + ReportReason, detail, true);
            }

            var reply = AiReplies.Last;
            if (reply == null)
            {
                return new SettingRow(SettingId.ReportReply, SettingKind.Action, label, "no reply yet",
                    "After the companion answers, report an offensive, harmful or wrong answer here.", false);
            }

            return new SettingRow(SettingId.ReportReply, SettingKind.Action, label,
                reply.Hidden ? "hidden" : Excerpt(reply.Reply),
                "A: hide this reply, then report it to Companion Intelligence.", false);
        }

        private string ActivateReport()
        {
            if (_reportedReply == null)
            {
                var reply = AiReplies.Last;
                if (reply == null) return "There is no reply to report yet.";

                AiReplies.Hide(reply);
                _reportedReply = reply;
                _reasonIndex = 0;
                return "Reply hidden. Pick a reason with left / right, then press A again to send the report.";
            }

            var report = BuildReport(_reportedReply);
            _reportedReply = null;
            if (report.Channel == ReplyReportChannel.Unavailable) return "The report could not be sent: " + report.Error;
            return SendReport != null ? SendReport(report) : "Reports cannot be sent from here.";
        }

        private string CycleReason(int direction)
        {
            if (_reportedReply == null) return null;
            int count = ReplyReporting.Reasons.Count;
            _reasonIndex = ((_reasonIndex + (direction >= 0 ? 1 : -1)) % count + count) % count;
            return "Reason: " + ReportReason;
        }

        private ReplyReport BuildReport(AiReply reply) =>
            ReplyReporting.Build(reply, ReportReason, AppVersion, _config.ResolveReplyReportDestination(),
                _isDevelopmentBuild, _devAllowlist);

        private static string Excerpt(string text)
        {
            string flat = (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
            return "\"" + (flat.Length <= 40 ? flat : flat.Substring(0, 40).TrimEnd() + "...") + "\"";
        }

        public static string PrefsKeyFor(SettingId id)
        {
            switch (id)
            {
                case SettingId.LlmEndpoint: return LlmConfig.PlayerPrefsEndpointKey;
                case SettingId.SpeechServer: return VoiceConfig.PlayerPrefsBaseUrlKey;
                case SettingId.CvServer: return ServerConfig.PlayerPrefsBaseUrlKey;
                default: return null;
            }
        }

        /// <summary>The model names offered: the configured choices, the asset default and the current value.</summary>
        public List<string> ModelChoices()
        {
            var choices = new List<string>();
            var llm = _config.llm;
            if (_config.llmModelChoices != null)
            {
                foreach (var choice in _config.llmModelChoices) AddDistinct(choices, choice);
            }
            if (llm != null)
            {
                AddDistinct(choices, llm.defaultModel);
                AddDistinct(choices, llm.model);
            }
            return choices;
        }

        private string CycleModel(int direction)
        {
            var llm = _config.llm;
            if (llm == null) return null;
            var choices = ModelChoices();
            if (choices.Count == 0) return null;

            int index = Math.Max(0, choices.IndexOf(llm.model));
            int next = ((index + (direction >= 0 ? 1 : -1)) % choices.Count + choices.Count) % choices.Count;
            ConfigOverrides.SetOverride(LlmConfig.PlayerPrefsModelKey, choices[next]);
            return "LLM model: " + choices[next];
        }

        private string ToggleProvider()
        {
            var voice = _config.voice;
            if (voice == null) return null;

            bool toCloud = voice.EffectiveProvider != SpeechProvider.Cloud;
            ConfigOverrides.SetOverride(VoiceConfig.PlayerPrefsProviderKey, toCloud ? "cloud" : "selfhosted");
            CloudOptInPending = false;
            if (!toCloud) return "Self-hosted speech selected.";
            return voice.EffectiveCloudOptIn
                ? "Cloud speech selected."
                : "Cloud speech selected, but it stays off until you enable the cloud opt-in.";
        }

        private string ActivateCloudOptIn()
        {
            var voice = _config.voice;
            if (voice == null) return null;

            if (voice.EffectiveCloudOptIn)
            {
                ConfigOverrides.SetOverride(VoiceConfig.PlayerPrefsCloudOptInKey, "false");
                CloudOptInPending = false;
                return "Cloud speech opt-in removed. Speech stays on your own server.";
            }

            if (!CloudOptInPending)
            {
                CloudOptInPending = true;
                return "Press again to confirm: audio and text will be sent to " + CloudHost(voice) + ".";
            }

            ConfigOverrides.SetOverride(VoiceConfig.PlayerPrefsCloudOptInKey, "true");
            CloudOptInPending = false;
            return voice.EffectiveProvider == SpeechProvider.Cloud
                ? "Cloud speech opt-in enabled."
                : "Cloud speech opt-in enabled; select the cloud provider to use it.";
        }

        private SettingRow CloudOptInRow(VoiceConfig voice, bool cloudSelected)
        {
            if (CloudOptInPending)
            {
                return new SettingRow(SettingId.CloudOptIn, SettingKind.Toggle, "Cloud speech opt-in", "press again to confirm",
                    "Audio and text will be sent to " + CloudHost(voice) + ".", true);
            }

            if (voice.EffectiveCloudOptIn)
            {
                return new SettingRow(SettingId.CloudOptIn, SettingKind.Toggle, "Cloud speech opt-in", "on",
                    cloudSelected ? "Speech is sent to " + CloudHost(voice) + "." : "Allowed, but self-hosted speech is selected.",
                    cloudSelected);
            }

            return new SettingRow(SettingId.CloudOptIn, SettingKind.Toggle, "Cloud speech opt-in", "off",
                "Speech is never sent to a third-party cloud.", false);
        }

        private SettingRow EndpointRow(SettingId id, string label, EndpointCheck check)
        {
            string value = check.Url.IsValid ? check.Url.ToString() : NotSet;
            string detail = check.IsUsable ? "OK (from " + check.Source + ")." : check.Message;
            return new SettingRow(id, SettingKind.Endpoint, label, value, detail, !check.IsUsable);
        }

        private static string RestartHint(SettingId id) =>
            id == SettingId.LlmEndpoint || id == SettingId.CvServer ? " Restart the app to apply." : string.Empty;

        private static string UrlOf(EndpointCheck check) => check.Url.IsValid ? check.Url.ToString() : string.Empty;

        private static string CloudHost(VoiceConfig voice) =>
            EndpointUrl.TryParse(voice.cloudSttEndpoint, out var url, out _) ? url.Host : "the cloud provider";

        private static void AddDistinct(List<string> list, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string v = value.Trim();
            if (!list.Contains(v)) list.Add(v);
        }
    }
}
