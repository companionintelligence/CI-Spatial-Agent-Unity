using System;
using System.Collections.Generic;
using XRScan.Core;

namespace XRScan.Tests.Config
{
    /// <summary>
    /// Isolates the static override sources: an in-memory PlayerPrefs store and a fake environment, so the
    /// tests never read or write the real PlayerPrefs or depend on the machine's environment variables.
    /// </summary>
    internal sealed class OverrideSandbox : IDisposable
    {
        public readonly MemorySettingsStore Store = new MemorySettingsStore();
        public readonly Dictionary<string, string> Environment = new Dictionary<string, string>();

        public OverrideSandbox()
        {
            ConfigOverrides.Store = Store;
            ConfigOverrides.EnvironmentReader = key => Environment.TryGetValue(key, out var value) ? value : null;
            CleartextPolicy.ClearDevAllowlist();
        }

        public void Dispose()
        {
            ConfigOverrides.ResetToDefaults();
            CleartextPolicy.ClearDevAllowlist();
        }
    }

    /// <summary>Scripted permission backend: records prompts and lets the test answer them.</summary>
    internal sealed class FakePermissionService : IPermissionService
    {
        public bool UsesRuntimePermissions { get; set; } = true;
        public readonly HashSet<string> Granted = new HashSet<string>();
        public readonly List<string> Requests = new List<string>();
        private readonly Dictionary<string, Action<PermissionState>> _pending = new Dictionary<string, Action<PermissionState>>();

        /// <summary>When set, prompts are answered immediately with this result.</summary>
        public PermissionState? AutoAnswer { get; set; }

        public bool IsGranted(string permission) => Granted.Contains(permission);

        public void Request(string permission, Action<PermissionState> onResult)
        {
            Requests.Add(permission);
            if (AutoAnswer.HasValue)
            {
                if (AutoAnswer.Value == PermissionState.Granted) Granted.Add(permission);
                onResult(AutoAnswer.Value);
                return;
            }
            _pending[permission] = onResult;
        }

        public void Answer(string permission, PermissionState result)
        {
            if (result == PermissionState.Granted) Granted.Add(permission);
            var callback = _pending[permission];
            _pending.Remove(permission);
            callback(result);
        }
    }
}
