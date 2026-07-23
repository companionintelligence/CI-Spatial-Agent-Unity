using System;
using UnityEngine;

public class ZeroShotSubsystem : MonoBehaviour
{
    [SerializeField] ZeroShotBackendConfig backendConfig;
    [SerializeField] ZeroShotDetector detector;
    [SerializeField] bool autoFindDetector = true;
    [SerializeField] bool autoBootstrapFromEnv = true;

    static ZeroShotSubsystem s_Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (s_Instance != null)
            return;
        if (!ShouldAutoEnable())
            return;

        var go = new GameObject("ZeroShotSubsystem");
        DontDestroyOnLoad(go);
        s_Instance = go.AddComponent<ZeroShotSubsystem>();
    }

    static bool ShouldAutoEnable()
    {
        var env = Environment.GetEnvironmentVariable("ZERO_SHOT_AUTO");
        if (!string.IsNullOrEmpty(env) && env == "1")
            return true;
        return PlayerPrefs.GetInt("ZeroShotAuto", 0) == 1;
    }

    void Awake()
    {
        s_Instance = this;
        if (backendConfig == null)
            backendConfig = Resources.Load<ZeroShotBackendConfig>("ZeroShotBackendConfig");

        if (detector == null && autoFindDetector)
            detector = FindObjectOfType<ZeroShotDetector>();

        if (detector != null && backendConfig != null)
            detector.ApplyConfig(backendConfig);
    }
}

