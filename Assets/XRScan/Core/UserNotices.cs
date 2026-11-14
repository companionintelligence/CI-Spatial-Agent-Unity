using System;

namespace XRScan.Core
{
    /// <summary>
    /// Short messages for the user from code that has no UI of its own, such as the scan pipeline. The in-headset
    /// settings panel shows them as a head-locked toast, the same way it shows the permission notices.
    /// </summary>
    public static class UserNotices
    {
        public static event Action<string> Posted;

        public static void Post(string message)
        {
            if (!string.IsNullOrWhiteSpace(message)) Posted?.Invoke(message);
        }
    }
}
