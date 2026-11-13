namespace XRScan.Core
{
    /// <summary>Why an endpoint can or cannot be used.</summary>
    public enum EndpointStatus
    {
        Ok,
        NotConfigured,
        Invalid,
        CleartextBlocked,
        CloudNotOptedIn
    }

    /// <summary>
    /// The outcome of resolving a service endpoint (where the value came from, and whether policy
    /// allows it). Backends that only accept a URL string get <see cref="UrlOrUnavailable"/>, which
    /// substitutes an address under the reserved <c>.invalid</c> TLD (RFC 6761) for an unusable
    /// endpoint: such a request fails fast at name resolution, so no audio, prompt or image is ever
    /// sent to an unintended host. The human-readable reason is in <see cref="Message"/>.
    /// </summary>
    public readonly struct EndpointCheck
    {
        public EndpointStatus Status { get; }
        public EndpointUrl Url { get; }
        public string Source { get; }
        public string Message { get; }

        public bool IsUsable => Status == EndpointStatus.Ok && Url.IsValid;

        private EndpointCheck(EndpointStatus status, EndpointUrl url, string source, string message)
        {
            Status = status;
            Url = url;
            Source = source ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public static EndpointCheck Ok(EndpointUrl url, string source) =>
            new EndpointCheck(EndpointStatus.Ok, url, source, string.Empty);

        public static EndpointCheck Fail(EndpointStatus status, string source, string message, EndpointUrl url = default) =>
            new EndpointCheck(status, url, source, message);

        /// <summary>A never-resolvable stand-in URL used when an endpoint is unconfigured or blocked.</summary>
        public static string UnavailableUrl(string service) =>
            "https://" + (string.IsNullOrEmpty(service) ? "service" : service) + ".endpoint-unavailable.invalid";

        /// <summary>The endpoint URL when usable, else <see cref="UnavailableUrl"/>.</summary>
        public string UrlOrUnavailable(string service) => IsUsable ? Url.ToString() : UnavailableUrl(service);

        /// <summary>Same check with a relative path appended to the URL (status unchanged).</summary>
        public EndpointCheck WithPath(string relativePath)
        {
            if (!Url.IsValid) return this;
            EndpointUrl.TryParse(Url.Combine(relativePath), out var combined, out _);
            return new EndpointCheck(Status, combined.IsValid ? combined : Url, Source, Message);
        }

        public override string ToString() =>
            IsUsable ? Url + " (" + Source + ")" : Status + ": " + Message;
    }
}
