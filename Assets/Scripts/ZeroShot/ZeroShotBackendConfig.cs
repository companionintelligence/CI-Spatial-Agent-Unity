using UnityEngine;

[CreateAssetMenu(menuName = "ZeroShot/Backend Config", fileName = "ZeroShotBackendConfig")]
public class ZeroShotBackendConfig : ScriptableObject
{
    [Header("Endpoint")]
    public string endpointUrl = "http://127.0.0.1:8000/detect";

    [Header("Auth")]
    public bool useBearerAuth = false;
    [TextArea(2, 4)] public string apiKey = "";

    [Header("Headers")]
    public string contentType = "application/json";
    public string acceptHeader = "application/json";

    [Header("Defaults")]
    public string defaultPrompt = "a person";
    [Range(0f, 1f)] public float defaultThreshold = 0.3f;
    [Range(1, 200)] public int defaultMaxDetections = 50;
}

