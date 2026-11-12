using System;
using System.Collections.Generic;
using UnityEngine;

namespace XRScan.Core
{
    /// <summary>Key/value storage for runtime setting overrides (PlayerPrefs on device).</summary>
    public interface ISettingsStore
    {
        /// <summary>The stored value, or null when the key is absent.</summary>
        string GetString(string key);
        void SetString(string key, string value);
        void DeleteKey(string key);
        void Save();
    }

    /// <summary>Default store: Unity PlayerPrefs (what the in-headset settings panel writes).</summary>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        public string GetString(string key) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key, string.Empty) : null;
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value ?? string.Empty);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Save() => PlayerPrefs.Save();
    }

    /// <summary>In-memory store for tests and previews; never touches the real PlayerPrefs.</summary>
    public sealed class MemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);

        public int Count => _values.Count;
        public string GetString(string key) => _values.TryGetValue(key, out var v) ? v : null;
        public void SetString(string key, string value) => _values[key] = value ?? string.Empty;
        public void DeleteKey(string key) => _values.Remove(key);
        public void Save() { }
    }

    /// <summary>
    /// The resolution order every config asset shares: (1) a PlayerPrefs override (written by the
    /// in-headset settings panel), (2) an environment variable (Editor / desktop / CI), then (3) the
    /// serialized value on the config asset. The store and environment reader are swappable so
    /// EditMode tests never read or write the real PlayerPrefs.
    /// </summary>
    public static class ConfigOverrides
    {
        public const string SourcePlayerPrefs = "PlayerPrefs";
        public const string SourceEnvironment = "environment";
        public const string SourceAsset = "config asset";

        private static ISettingsStore s_store = new PlayerPrefsSettingsStore();
        private static Func<string, string> s_environment = Environment.GetEnvironmentVariable;

        public static ISettingsStore Store
        {
            get => s_store;
            set => s_store = value ?? new PlayerPrefsSettingsStore();
        }

        public static Func<string, string> EnvironmentReader
        {
            get => s_environment;
            set => s_environment = value ?? Environment.GetEnvironmentVariable;
        }

        /// <summary>Restores the PlayerPrefs store and the process environment reader.</summary>
        public static void ResetToDefaults()
        {
            s_store = new PlayerPrefsSettingsStore();
            s_environment = Environment.GetEnvironmentVariable;
        }

        /// <summary>The trimmed PlayerPrefs override, or null when unset / blank.</summary>
        public static string GetOverride(string prefsKey)
        {
            if (string.IsNullOrEmpty(prefsKey)) return null;
            var value = s_store.GetString(prefsKey);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>The trimmed environment variable, or null when unset / blank.</summary>
        public static string GetEnvironment(string envKey)
        {
            if (string.IsNullOrEmpty(envKey)) return null;
            string value;
            try { value = s_environment(envKey); }
            catch (System.Security.SecurityException) { value = null; }
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>PlayerPrefs override, then environment variable, then the asset value.</summary>
        public static string Resolve(string prefsKey, string envKey, string assetValue, out string source)
        {
            var value = GetOverride(prefsKey);
            if (value != null) { source = SourcePlayerPrefs; return value; }

            value = GetEnvironment(envKey);
            if (value != null) { source = SourceEnvironment; return value; }

            source = SourceAsset;
            return string.IsNullOrWhiteSpace(assetValue) ? string.Empty : assetValue.Trim();
        }

        /// <summary>Parses a boolean override ("1/0", "true/false", "yes/no", "on/off"); null when unset or unparsable.</summary>
        public static bool? GetBoolOverride(string prefsKey)
        {
            var value = GetOverride(prefsKey);
            if (value == null) return null;
            switch (value.ToLowerInvariant())
            {
                case "1": case "true": case "yes": case "on": return true;
                case "0": case "false": case "no": case "off": return false;
                default: return null;
            }
        }

        /// <summary>Stores (or, for a blank value, clears) a PlayerPrefs override and saves.</summary>
        public static void SetOverride(string prefsKey, string value)
        {
            if (string.IsNullOrEmpty(prefsKey)) return;
            if (string.IsNullOrWhiteSpace(value)) s_store.DeleteKey(prefsKey);
            else s_store.SetString(prefsKey, value.Trim());
            s_store.Save();
        }

        /// <summary>Removes the given overrides so the environment / asset values apply again.</summary>
        public static void ClearOverrides(IEnumerable<string> prefsKeys)
        {
            if (prefsKeys == null) return;
            foreach (var key in prefsKeys)
            {
                if (!string.IsNullOrEmpty(key)) s_store.DeleteKey(key);
            }
            s_store.Save();
        }
    }
}
