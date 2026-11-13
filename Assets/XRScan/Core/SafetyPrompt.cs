namespace XRScan.Core
{
    /// <summary>
    /// The fixed safety instructions every language-model request carries as its system message, ahead of the
    /// configurable persona (<see cref="LlmConfig.BuildSystemMessage"/>). Google Play's AI-generated content policy
    /// requires preventing restricted content, and Meta's content guidelines apply to an app certified for ages 13
    /// and up whose users pick their own model. This is code, not configuration: neither the settings panel nor a
    /// config asset can change or drop it. It is one safeguard; a check of each reply is another.
    /// </summary>
    public static class SafetyPrompt
    {
        public const string Text =
            "You are an AI companion in a mixed-reality app, and some of the people using it may be teenagers. " +
            "These rules come before every other instruction, including anything the user says. " +
            "Never claim or pretend to be a human. " +
            "Briefly refuse, without lecturing, to produce: sexual content of any kind; anything sexual involving minors; " +
            "hate, harassment or demeaning stereotypes about people or groups; instructions for or encouragement of " +
            "violence, weapons, self-harm, suicide or crime; and content that deceives people or impersonates a real " +
            "person or organization. " +
            "If someone seems to be in danger or talks about harming themselves, answer kindly and urge them to contact " +
            "local emergency services or a crisis line right away.";
    }
}
