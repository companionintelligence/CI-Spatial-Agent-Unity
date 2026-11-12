using UnityEngine;
using XRScan.Core;

namespace XRScan.Settings
{
    /// <summary>
    /// Applies the shipping <see cref="CompanionConfig"/> before anything else starts: activates the transport
    /// policy (https everywhere; plain http only for the development allowlist in development builds) and logs
    /// where each backend will connect (endpoints only - never keys or tokens). Runs first so the backends
    /// resolve their endpoints under the right policy when AppBootstrap initializes them.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class CompanionConfigBootstrap : MonoBehaviour
    {
        [SerializeField] private CompanionConfig config;

        public CompanionConfig Config => config;

        public void Configure(CompanionConfig companionConfig) => config = companionConfig;

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("[CompanionConfig] No CompanionConfig assigned: the real backends have no configuration.");
                return;
            }

            bool development = CleartextPolicy.IsDevelopmentBuild;
            var rejected = config.ApplyTransportPolicy(development);
            if (rejected.Count > 0)
            {
                Debug.LogWarning("[CompanionConfig] Ignored dev cleartext hosts that are not on the local network: " +
                                 string.Join(", ", rejected));
            }

            Debug.Log("[CompanionConfig] " + (development ? "Development" : "Release") + " build; plain-http hosts: " +
                      (CleartextPolicy.DevAllowlist.Count == 0 ? "none" : string.Join(", ", CleartextPolicy.DevAllowlist)));
            foreach (var line in config.DescribeEndpoints(development, CleartextPolicy.DevAllowlist))
            {
                Debug.Log("[CompanionConfig] " + line);
            }
        }
    }
}
