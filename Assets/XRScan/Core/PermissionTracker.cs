using System;
using System.Collections.Generic;
using UnityEngine;

namespace XRScan.Core
{
    /// <summary>The runtime ("dangerous") permissions the companion asks for, and what is lost without them.</summary>
    public static class AppPermissions
    {
        /// <summary>Microphone, for push-to-talk speech-to-text.</summary>
        public const string Microphone = "android.permission.RECORD_AUDIO";

        /// <summary>Quest passthrough camera images (Passthrough Camera API), for room scanning.</summary>
        public const string HeadsetCamera = "horizonos.permission.HEADSET_CAMERA";

        /// <summary>Meta spatial data (scene model, planes, meshes, environment raycasts), for pin placement.</summary>
        public const string SpatialData = "com.oculus.permission.USE_SCENE";

        public static string DisplayName(string permission)
        {
            switch (permission)
            {
                case Microphone: return "Microphone";
                case HeadsetCamera: return "Headset camera";
                case SpatialData: return "Spatial data";
                default: return permission;
            }
        }

        /// <summary>What the app does without the permission (shown to the user on denial).</summary>
        public static string DeniedConsequence(string permission)
        {
            switch (permission)
            {
                case Microphone: return "voice questions are off";
                case HeadsetCamera: return "room scanning gets no camera image";
                case SpatialData: return "pins are placed at a fixed distance instead of on room surfaces";
                default: return "the feature that needs it is off";
            }
        }
    }

    /// <summary>Where a runtime permission stands.</summary>
    public enum PermissionState
    {
        NotRequested,
        Requesting,
        Granted,
        Denied,

        /// <summary>Denied with "don't ask again" (or blocked by policy): only the system settings can grant it now.</summary>
        DeniedPermanently
    }

    /// <summary>Platform permission backend (Android runtime permissions; always-granted elsewhere).</summary>
    public interface IPermissionService
    {
        /// <summary>False where the platform has no runtime permission model (Editor, desktop): everything counts as granted.</summary>
        bool UsesRuntimePermissions { get; }

        bool IsGranted(string permission);

        /// <summary>
        /// Shows the system prompt. <paramref name="onResult"/> receives Granted, Denied or DeniedPermanently
        /// and must be invoked on the main thread.
        /// </summary>
        void Request(string permission, Action<PermissionState> onResult);
    }

    /// <summary>
    /// Requests a permission at the moment a feature first needs it and remembers the outcome:
    /// <list type="bullet">
    /// <item>already granted (or no runtime permission model) → resolves immediately, no prompt;</item>
    /// <item>permanently denied → resolves immediately as DeniedPermanently, no prompt (Android would not show
    /// one; the user has to enable it in the system settings);</item>
    /// <item>otherwise shows the system prompt once; callers that need the same permission while the prompt
    /// is open share it. A plain denial is asked again the next time the feature is used.</item>
    /// </list>
    /// Free of Unity callbacks so it is unit-testable with a fake <see cref="IPermissionService"/>.
    /// </summary>
    public sealed class PermissionTracker
    {
        /// <summary>A prompt that has not answered within this time is assumed lost and may be shown again.</summary>
        public const double RequestTimeoutSeconds = 60.0;

        private readonly IPermissionService _service;
        private readonly Func<double> _clock;
        private readonly Dictionary<string, PermissionState> _states = new Dictionary<string, PermissionState>();
        private readonly Dictionary<string, double> _requestedAt = new Dictionary<string, double>();
        private readonly Dictionary<string, List<Action<PermissionState>>> _waiting = new Dictionary<string, List<Action<PermissionState>>>();

        /// <summary>Raised whenever a permission's state changes (permission, new state).</summary>
        public event Action<string, PermissionState> StateChanged;

        public PermissionTracker(IPermissionService service, Func<double> clock = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _clock = clock ?? (() => Time.realtimeSinceStartupAsDouble);
        }

        public bool UsesRuntimePermissions => _service.UsesRuntimePermissions;

        public PermissionState GetState(string permission)
        {
            if (!_service.UsesRuntimePermissions || _service.IsGranted(permission)) return PermissionState.Granted;
            if (_states.TryGetValue(permission, out var state) && state != PermissionState.Granted) return state;
            return PermissionState.NotRequested;
        }

        public bool IsGranted(string permission) => GetState(permission) == PermissionState.Granted;

        /// <summary>Resolves the permission now if possible, else prompts once (see the class summary).</summary>
        public void Ensure(string permission, Action<PermissionState> onResolved)
        {
            if (string.IsNullOrEmpty(permission)) throw new ArgumentException("permission is required", nameof(permission));

            var state = GetState(permission);
            if (state == PermissionState.Granted || state == PermissionState.DeniedPermanently)
            {
                if (state == PermissionState.Granted) SetState(permission, PermissionState.Granted);
                onResolved?.Invoke(state);
                return;
            }

            if (!_waiting.TryGetValue(permission, out var callbacks))
            {
                callbacks = new List<Action<PermissionState>>();
                _waiting[permission] = callbacks;
            }
            if (onResolved != null) callbacks.Add(onResolved);

            bool promptOpen = state == PermissionState.Requesting
                              && _requestedAt.TryGetValue(permission, out var since)
                              && _clock() - since < RequestTimeoutSeconds;
            if (promptOpen) return;

            _requestedAt[permission] = _clock();
            SetState(permission, PermissionState.Requesting);
            _service.Request(permission, result => Complete(permission, result));
        }

        /// <summary>Forgets a permanent denial, e.g. after the user was sent to the system settings.</summary>
        public void Forget(string permission)
        {
            if (_states.Remove(permission)) StateChanged?.Invoke(permission, GetState(permission));
        }

        private void Complete(string permission, PermissionState result)
        {
            if (result != PermissionState.Granted && result != PermissionState.DeniedPermanently)
                result = PermissionState.Denied; // dismissed / unexpected → a plain denial (ask again next time)

            SetState(permission, result);
            if (!_waiting.TryGetValue(permission, out var callbacks)) return;
            _waiting.Remove(permission);
            foreach (var callback in callbacks) callback(result);
        }

        private void SetState(string permission, PermissionState state)
        {
            if (_states.TryGetValue(permission, out var previous) && previous == state) return;
            _states[permission] = state;
            StateChanged?.Invoke(permission, state);
        }

        /// <summary>User-facing one-liner for a permission state.</summary>
        public static string Describe(string permission, PermissionState state)
        {
            string name = AppPermissions.DisplayName(permission);
            switch (state)
            {
                case PermissionState.Granted: return name + ": allowed";
                case PermissionState.Requesting: return name + ": waiting for your answer";
                case PermissionState.Denied:
                    return name + " permission denied: " + AppPermissions.DeniedConsequence(permission) + ". You will be asked again next time.";
                case PermissionState.DeniedPermanently:
                    return name + " permission is blocked: " + AppPermissions.DeniedConsequence(permission) +
                           ". Allow it in the system Settings > Apps > Permissions.";
                default: return name + ": not asked yet";
            }
        }

        /// <summary>Short form for status lines ("Mic ok").</summary>
        public static string ShortState(PermissionState state)
        {
            switch (state)
            {
                case PermissionState.Granted: return "ok";
                case PermissionState.Requesting: return "asking";
                case PermissionState.Denied: return "denied";
                case PermissionState.DeniedPermanently: return "blocked";
                default: return "not asked";
            }
        }
    }
}
