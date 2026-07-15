using UnityEngine.Events;

namespace XRScan.Core
{
    /// <summary>Concrete UnityEvent&lt;string&gt; (the generic base is abstract and cannot be
    /// instantiated). Shared by the voice / LLM backends for their string-payload events.</summary>
    [System.Serializable]
    public class StringUnityEvent : UnityEvent<string> { }
}
