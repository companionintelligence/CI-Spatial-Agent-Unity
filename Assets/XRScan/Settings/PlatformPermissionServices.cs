using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
using XRScan.Core;

namespace XRScan.Settings
{
    /// <summary>Editor / desktop: there is no runtime permission model, so everything counts as granted.</summary>
    public sealed class NoRuntimePermissionService : IPermissionService
    {
        public bool UsesRuntimePermissions => false;
        public bool IsGranted(string permission) => true;
        public void Request(string permission, Action<PermissionState> onResult) => onResult?.Invoke(PermissionState.Granted);
    }

#if UNITY_ANDROID || UNITY_EDITOR
    /// <summary>
    /// Android runtime permissions through <see cref="UnityEngine.Android.Permission"/>. The platform may answer on
    /// another thread, so results are queued and delivered on the main thread by <see cref="Pump"/> (called every
    /// frame by <see cref="RuntimePermissions"/>). Compiled in the Editor as well so the API use is type-checked
    /// there; it is only instantiated on an Android device.
    /// </summary>
    public sealed class AndroidPermissionService : IPermissionService
    {
        private readonly ConcurrentQueue<Action> _results = new ConcurrentQueue<Action>();

        public bool UsesRuntimePermissions => true;

        public bool IsGranted(string permission) => UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission);

        public void Request(string permission, Action<PermissionState> onResult)
        {
            int delivered = 0;
            void Deliver(Func<PermissionState> state)
            {
                if (Interlocked.Exchange(ref delivered, 1) == 1) return; // one answer per prompt
                _results.Enqueue(() => onResult?.Invoke(state()));
            }

            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => Deliver(() => PermissionState.Granted);
            // Unity recommends this over the (unreliable) "don't ask again" callback: after a denial, Android
            // stops offering a rationale once the user has blocked the permission (or denied it twice).
            // Evaluated in Pump, i.e. on the main thread.
            callbacks.PermissionDenied += denied => Deliver(() =>
                UnityEngine.Android.Permission.ShouldShowRequestPermissionRationale(denied)
                    ? PermissionState.Denied
                    : PermissionState.DeniedPermanently);
            callbacks.PermissionRequestDismissed += _ => Deliver(() => PermissionState.Denied);
            UnityEngine.Android.Permission.RequestUserPermission(permission, callbacks);
        }

        /// <summary>Delivers queued results on the calling (main) thread.</summary>
        public void Pump()
        {
            while (_results.TryDequeue(out var deliver)) deliver();
        }

        /// <summary>Opens this app's page in the system settings, where a blocked permission can be allowed.</summary>
        public static bool OpenAppSettings()
        {
            try
            {
                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                using (var uri = uriClass.CallStatic<AndroidJavaObject>("parse", "package:" + Application.identifier))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS", uri))
                {
                    const int flagActivityNewTask = 0x10000000;
                    intent.Call<AndroidJavaObject>("addFlags", flagActivityNewTask)?.Dispose();
                    UnityEngine.Android.AndroidApplication.currentActivity.Call("startActivity", intent);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RuntimePermissions] Could not open the app settings: " + e.Message);
                return false;
            }
        }
    }
#endif
}
