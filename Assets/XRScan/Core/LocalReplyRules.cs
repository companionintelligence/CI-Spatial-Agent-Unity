using System.Text.RegularExpressions;

namespace XRScan.Core
{
    /// <summary>
    /// The short on-device list the Google Play sheet asks for whatever else checks replies (gate 11: "the minimum either
    /// way"), for the two kinds of content Play's AI-generated content policy names: content that facilitates the
    /// exploitation or abuse of children, and deceptive behaviour. <see cref="ReplyGate"/> applies it to every reply
    /// before the reply check, without a server. It only ever blocks, and it is deliberately blunt:
    /// <list type="bullet">
    /// <item><b>Minors:</b> a word for a child or teenager (or an age under 18) together with a sexual word anywhere in
    /// the reply. A refusal that names the topic is blocked too; the companion then says the fixed line instead.</item>
    /// <item><b>Claims to be human:</b> "I am a human / a real person", "I'm not an AI / a bot". The safety prompt tells the
    /// model never to claim to be human (<see cref="SafetyPrompt"/>).</item>
    /// </list>
    /// tools/reply-check/cases.json marks the test cases these rules block ("local"); an EditMode test keeps the marks
    /// true.
    /// </summary>
    public static class LocalReplyRules
    {
        public const string MinorsRule = "on-device rule: minors";
        public const string HumanClaimRule = "on-device rule: claims to be human";

        private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private static readonly Regex Minor = new Regex(
            @"\b(?:child|children|kids?|minors?|underage|pre-?teens?|teens?|teenagers?|teenage|toddlers?|schoolgirls?|schoolboys?|" +
            @"(?:[1-9]|1[0-7]|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen)" +
            @"[- ]years?[- ]olds?)\b", Options);

        private static readonly Regex Sexual = new Regex(
            @"\b(?:sex|sexual|sexually|sexy|nude|nudes|naked|porn|porno|pornography|pornographic|erotic|erotica)\b", Options);

        private static readonly Regex HumanClaim = new Regex(
            @"\bI(?:'m| am) (?:an? )?(?:real |actual )?(?:human|person)\b|" +
            @"\bI(?:'m| am) not (?:an? )?(?:AI|bot|robot|machine|chatbot|computer|language model)\b", Options);

        /// <summary>True, with the rule's name, when an on-device rule blocks <paramref name="reply"/>.</summary>
        public static bool Blocks(string reply, out string rule)
        {
            rule = string.Empty;
            if (string.IsNullOrWhiteSpace(reply)) return false;

            // Models often write typographic apostrophes ("I’m").
            string text = reply.Replace('’', '\'').Replace('ʼ', '\'');

            if (Minor.IsMatch(text) && Sexual.IsMatch(text))
            {
                rule = MinorsRule;
                return true;
            }
            if (HumanClaim.IsMatch(text))
            {
                rule = HumanClaimRule;
                return true;
            }
            return false;
        }
    }
}
