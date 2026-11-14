using UnityEngine;

namespace XRScan.Core
{
    /// <summary>
    /// The app sends Unity no device data (privacy launch condition S1). Player Settings turn Unity's hardware
    /// statistics off (<c>submitAnalytics: 0</c>) and every Unity service is off in UnityConnectSettings. As a
    /// second line, this switches Unity's runtime analytics and device statistics off before the first scene loads,
    /// in case a build or license setting turns them back on. The Editor keeps its own settings.
    /// </summary>
    public static class UnityDataCollection
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void TurnOffAtStartup()
        {
            if (!Application.isEditor) TurnOff();
        }

        /// <summary>Turns off Unity Analytics, its device statistics and user tracking for this run.</summary>
        public static void TurnOff()
        {
            UnityEngine.Analytics.Analytics.deviceStatsEnabled = false;
            UnityEngine.Analytics.Analytics.limitUserTracking = true;
            UnityEngine.Analytics.Analytics.enabled = false;
        }
    }
}
