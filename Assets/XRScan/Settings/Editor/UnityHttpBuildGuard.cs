using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using XRScan.Core;

namespace XRScan.EditorTools
{
    /// <summary>
    /// Build guard for the Unity-level gate on plain http://.
    /// <para>Every XRScan backend (LLM, speech, CV server, glTF sync) sends HTTP through UnityWebRequest. On Android
    /// that runs on Unity's native libcurl inside libunity.so, which never consults Android's
    /// <c>NetworkSecurityPolicy</c>, so the generated network security config
    /// (<see cref="NetworkSecurityConfigGenerator"/>) does not reach it. Only two checks gate those requests:</para>
    /// <list type="number">
    /// <item>Unity's project-wide Player Settings &gt; "Allow downloads over HTTP"
    /// (<see cref="PlayerSettings.insecureHttpOption"/>), which the player enforces natively
    /// ("Insecure connection not allowed").</item>
    /// <item>The app-level <see cref="CleartextPolicy"/>, which every backend endpoint is resolved through:
    /// https always; plain http only in development builds, for allow-listed LAN hosts.</item>
    /// </list>
    /// <para>This guard keeps the first check in place: a release build fails while the setting is
    /// "Always allowed", because Unity would then send plain http:// from a shipping build. Development builds only
    /// log: a warning when dev cleartext hosts are configured but the setting is "Not allowed" (Unity refuses them
    /// too), and a warning while it is "Always allowed" (the next release build would fail).</para>
    /// </summary>
    public class UnityHttpBuildGuard : IPreprocessBuildWithReport
    {
        public enum Verdict { Ok, Warn, Fail }

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            bool development = (report.summary.options & BuildOptions.Development) != 0;
            int devHosts = development ? NetworkSecurityConfigGenerator.CollectDevCleartextHosts(false).Count : 0;

            var verdict = Evaluate(PlayerSettings.insecureHttpOption, development, devHosts, out string message);
            message = "[UnityHttpBuildGuard] " + message;
            switch (verdict)
            {
                case Verdict.Fail: throw new BuildFailedException(message);
                case Verdict.Warn: Debug.LogWarning(message); break;
                default: Debug.Log(message); break;
            }
        }

        /// <summary>
        /// Whether a build may go ahead with Unity's "Allow downloads over HTTP" <paramref name="option"/>.
        /// <paramref name="devCleartextHostCount"/> is the number of sanitized dev cleartext hosts (development builds).
        /// </summary>
        public static Verdict Evaluate(InsecureHttpOption option, bool developmentBuild, int devCleartextHostCount, out string message)
        {
            string setting = "Player Settings > 'Allow downloads over HTTP' is '" + Label(option) + "'";

            if (!developmentBuild)
            {
                if (option == InsecureHttpOption.AlwaysAllowed)
                {
                    message = setting + ", so this release build would let UnityWebRequest send plain http://. The Android " +
                              "network security config cannot stop that: UnityWebRequest runs on Unity's native HTTP stack, " +
                              "which ignores it. Set it to '" + Label(InsecureHttpOption.DevelopmentOnly) + "' or '" +
                              Label(InsecureHttpOption.NotAllowed) + "'.";
                    return Verdict.Fail;
                }

                message = "Release build: " + setting + ", so Unity refuses plain http:// requests; CleartextPolicy allows https only.";
                return Verdict.Ok;
            }

            if (option == InsecureHttpOption.NotAllowed)
            {
                if (devCleartextHostCount > 0)
                {
                    message = "Development build with " + devCleartextHostCount + " dev cleartext host(s), but " + setting +
                              ", so Unity refuses plain http:// to them as well. Set it to '" +
                              Label(InsecureHttpOption.DevelopmentOnly) + "' to reach a LAN server over http.";
                    return Verdict.Warn;
                }

                message = "Development build: " + setting + ", so Unity refuses plain http://; no dev cleartext hosts are configured.";
                return Verdict.Ok;
            }

            string scope = devCleartextHostCount > 0
                ? "CleartextPolicy limits it to the " + devCleartextHostCount + " allow-listed LAN host(s)."
                : "CleartextPolicy allows it for no host (the dev cleartext allowlist is empty).";

            if (option == InsecureHttpOption.AlwaysAllowed)
            {
                message = "Development build: " + setting + "; " + scope + " A release build fails with this setting: use '" +
                          Label(InsecureHttpOption.DevelopmentOnly) + "'.";
                return Verdict.Warn;
            }

            message = "Development build: " + setting + ", so Unity allows plain http://; " + scope;
            return Verdict.Ok;
        }

        /// <summary>The Player Settings label of an option.</summary>
        public static string Label(InsecureHttpOption option)
        {
            switch (option)
            {
                case InsecureHttpOption.NotAllowed: return "Not allowed";
                case InsecureHttpOption.DevelopmentOnly: return "Allowed in development builds";
                case InsecureHttpOption.AlwaysAllowed: return "Always allowed";
                default: return option.ToString();
            }
        }
    }
}
