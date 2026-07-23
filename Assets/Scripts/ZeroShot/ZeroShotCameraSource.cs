using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ZeroShotCameraSource : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] string deviceName = "";
    [SerializeField] Vector2Int requestedResolution = new Vector2Int(1280, 720);
    [SerializeField] bool autoStart = true;
    [SerializeField] bool useOverrideTexture = false;
    [SerializeField] Texture overrideTexture;

    [Header("Preview")]
    [SerializeField] RawImage previewImage;

    public WebCamTexture WebCamTexture { get; private set; }
    public Texture CurrentTexture => useOverrideTexture && overrideTexture != null ? overrideTexture : WebCamTexture;

    void Start()
    {
        if (autoStart)
            StartCamera();
    }

    public void StartCamera()
    {
        if (useOverrideTexture && overrideTexture != null)
        {
            if (previewImage != null)
                previewImage.texture = overrideTexture;
            Debug.Log("[ZeroShot] Using override texture for preview.");
            return;
        }

        if (WebCamTexture != null)
            return;

        var device = ResolveDevice();
        if (device == null)
        {
            Debug.LogWarning("[ZeroShot] No webcam devices found.");
            return;
        }

        WebCamTexture = requestedResolution.x > 0 && requestedResolution.y > 0
            ? new WebCamTexture(device.name, requestedResolution.x, requestedResolution.y)
            : new WebCamTexture(device.name);

        WebCamTexture.Play();
        if (previewImage != null)
            previewImage.texture = WebCamTexture;

        Debug.Log($"[ZeroShot] Camera started: {device.name} {WebCamTexture.width}x{WebCamTexture.height}");
    }

    public void StopCamera()
    {
        if (WebCamTexture == null)
            return;
        WebCamTexture.Stop();
        Destroy(WebCamTexture);
        WebCamTexture = null;
    }

    void OnDisable()
    {
        StopCamera();
    }

    WebCamDevice? ResolveDevice()
    {
        var devices = WebCamTexture.devices;
        if (devices == null || devices.Length == 0)
            return null;

        if (!string.IsNullOrEmpty(deviceName))
        {
            var match = devices.FirstOrDefault(d => d.name == deviceName);
            if (!string.IsNullOrEmpty(match.name))
                return match;
        }

        return devices[0];
    }
}
