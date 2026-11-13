namespace XRScan.Core
{
    /// <summary>
    /// The AI disclosure (EU AI Act Art. 50(1); privacy launch condition S2): a one-time notice at first launch,
    /// shown whatever the server settings are, so a build with preset servers shows it too, and the same line kept
    /// in the settings panel. The line also gives Google Play the in-app privacy-policy text it asks for.
    /// </summary>
    public static class AiDisclosure
    {
        /// <summary>The wording of privacy/DECISIONS.md S2. Change it only together with the published policy.</summary>
        public const string Line =
            "Answers come from an AI language model and can be wrong. Privacy policy: ci.computer/privacy/spatial-companion";

        /// <summary>PlayerPrefs key that records that the user has seen and closed the notice.</summary>
        public const string AcknowledgedKey = "XRScan.AiNoticeAcknowledged";

        /// <summary>Raise it to show the notice again once, for example after the wording changes.</summary>
        public const string NoticeVersion = "1";

        /// <summary>True until the user closes the notice. It does not depend on any server setting.</summary>
        public static bool NoticeNeeded => ConfigOverrides.GetOverride(AcknowledgedKey) != NoticeVersion;

        public static void Acknowledge() => ConfigOverrides.SetOverride(AcknowledgedKey, NoticeVersion);
    }
}
