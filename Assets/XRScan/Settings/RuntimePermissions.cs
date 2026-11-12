using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using XRScan.Core;
using XRScan.Platform;

namespace XRScan.Settings
{
    /// <summary>
    /// Owns the runtime-permission flow (Android / Quest; on other platforms everything counts as granted):
    /// <list type="bullet">
    /// <item><b>Microphone</b> - asked the first time the user talks, by <see cref="PermissionGatedSpeechToText"/>.</item>
    /// <item><b>Spatial data</b> (com.oculus.permission.USE_SCENE) then <b>headset camera</b>
    /// (horizonos.permission.HEADSET_CAMERA) - asked one after the other once the AR session is up, which is
    /// when the passthrough scan pipeline first needs them (capture starts with the session). Quest only.</item>
    /// </list>
    /// Denials are handled without blocking the app: the affected feature degrades (no voice / no camera image /
    /// fixed-distance pins), a notice explains why, a plain denial is asked again on next use, and a blocked
    /// ("don't ask again") permission is never re-prompted - <see cref="RequestMissing"/> opens the system
    /// settings instead. After a grant the AR managers that depend on it are restarted so they pick it up.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class RuntimePermissions : MonoBehaviour
    {
        [Tooltip("Ask for spatial data and the headset camera once the AR session is running (Quest only).")]
        [SerializeField] private bool requestSpatialOnStart = true;

        [Tooltip("Seconds to wait after start-up before the spatial prompts, so they appear in a tracked, visible view.")]
        [SerializeField] private float spatialPromptDelaySeconds = 1.5f;

        private IPermissionService _service;
        private PermissionTracker _tracker;
        private Action _pump;

        /// <summary>User-facing permission messages (denials). The settings panel shows them.</summary>
        public static event Action<string> Notice;

        public PermissionTracker Tracker
        {
            get
            {
                if (_tracker == null)
                {
                    _tracker = new PermissionTracker(Service);
                    _tracker.StateChanged += OnStateChanged;
                }
                return _tracker;
            }
        }

        private IPermissionService Service => _service ??= CreatePlatformService();

        /// <summary>Replaces the platform service (tests, or a custom platform).</summary>
        public void UseService(IPermissionService service)
        {
            if (_tracker != null) _tracker.StateChanged -= OnStateChanged;
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _tracker = null;
            _pump = null;
        }

        public PermissionState GetState(string permission) => Tracker.GetState(permission);

        /// <summary>Resolves a permission at first use; see <see cref="PermissionTracker.Ensure"/>.</summary>
        public void Ensure(string permission, Action<PermissionState> onResolved) => Tracker.Ensure(permission, onResolved);

        /// <summary>One-line status for the settings panel.</summary>
        public string Summary()
        {
            if (!Tracker.UsesRuntimePermissions) return "not needed on this platform";
            return "Mic " + PermissionTracker.ShortState(GetState(AppPermissions.Microphone)) +
                   " | Camera " + PermissionTracker.ShortState(GetState(AppPermissions.HeadsetCamera)) +
                   " | Spatial " + PermissionTracker.ShortState(GetState(AppPermissions.SpatialData));
        }

        /// <summary>
        /// Asks again for every missing permission (plain denials), one prompt at a time. When a permission is
        /// blocked, opens the system settings page of the app instead (Android shows no prompt for those).
        /// </summary>
        public void RequestMissing()
        {
            if (!Tracker.UsesRuntimePermissions) return;

            bool blocked = GetState(AppPermissions.Microphone) == PermissionState.DeniedPermanently
                           || (IsQuest() && (GetState(AppPermissions.HeadsetCamera) == PermissionState.DeniedPermanently
                                             || GetState(AppPermissions.SpatialData) == PermissionState.DeniedPermanently));
            if (blocked && OpenAppSettings())
            {
                // The user may allow it there; forget the block so the next use checks again.
                Tracker.Forget(AppPermissions.Microphone);
                Tracker.Forget(AppPermissions.HeadsetCamera);
                Tracker.Forget(AppPermissions.SpatialData);
                return;
            }

            Ensure(AppPermissions.Microphone, _ =>
            {
                if (IsQuest()) RequestSpatial();
            });
        }

        /// <summary>Spatial data, then the headset camera (Android shows one prompt at a time).</summary>
        public void RequestSpatial()
        {
            Ensure(AppPermissions.SpatialData, scene =>
            {
                if (scene == PermissionState.Granted) RestartSpatialManagers();
                Ensure(AppPermissions.HeadsetCamera, camera =>
                {
                    if (camera == PermissionState.Granted) RestartCameraManagers();
                });
            });
        }

        public bool OpenAppSettings()
        {
#if UNITY_ANDROID || UNITY_EDITOR
            if (Application.platform == RuntimePlatform.Android) return AndroidPermissionService.OpenAppSettings();
#endif
            return false;
        }

        private IEnumerator Start()
        {
            if (!requestSpatialOnStart || !Tracker.UsesRuntimePermissions || !IsQuest()) yield break;
            if (spatialPromptDelaySeconds > 0f) yield return new WaitForSecondsRealtime(spatialPromptDelaySeconds);
            RequestSpatial();
        }

        private void Update() => _pump?.Invoke();

        private IPermissionService CreatePlatformService()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var android = new AndroidPermissionService();
            _pump = android.Pump;
            return android;
#else
            return new NoRuntimePermissionService();
#endif
        }

        /// <summary>HEADSET_CAMERA and USE_SCENE are Meta permissions; other headsets do not declare them.</summary>
        private static bool IsQuest() => new PlatformCapabilities().Vendor == XRVendor.MetaQuest;

        private void OnStateChanged(string permission, PermissionState state)
        {
            if (state != PermissionState.Denied && state != PermissionState.DeniedPermanently) return;
            string message = PermissionTracker.Describe(permission, state);
            Debug.LogWarning("[RuntimePermissions] " + message);
            Post(message);
        }

        /// <summary>Shows a user-facing message through the settings panel's notice line.</summary>
        public static void Post(string message)
        {
            if (!string.IsNullOrEmpty(message)) Notice?.Invoke(message);
        }

        private static void RestartCameraManagers()
        {
            foreach (var manager in FindObjectsByType<ARCameraManager>(FindObjectsSortMode.None)) Restart(manager);
        }

        private static void RestartSpatialManagers()
        {
            foreach (var manager in FindObjectsByType<ARPlaneManager>(FindObjectsSortMode.None)) Restart(manager);
            foreach (var manager in FindObjectsByType<ARMeshManager>(FindObjectsSortMode.None)) Restart(manager);
            foreach (var manager in FindObjectsByType<ARRaycastManager>(FindObjectsSortMode.None)) Restart(manager);
            foreach (var manager in FindObjectsByType<ARAnchorManager>(FindObjectsSortMode.None)) Restart(manager);
        }

        /// <summary>Stops and restarts an AR manager's subsystem so it re-checks the permission it was missing.</summary>
        private static void Restart(Behaviour manager)
        {
            if (manager == null || !manager.isActiveAndEnabled) return;
            manager.enabled = false;
            manager.enabled = true;
        }
    }
}
