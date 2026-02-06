using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ZeroShotDetector : MonoBehaviour
{
    [Serializable]
    class ZeroShotRequest
    {
        public string prompt;
        public float threshold;
        public int max_detections;
        public string image_base64;
    }

    [Serializable]
    class ZeroShotDetection
    {
        public string label;
        public float confidence;
        public float[] bbox; // [x, y, w, h] in pixel space
    }

    [Serializable]
    class ZeroShotResponse
    {
        public int frame_width;
        public int frame_height;
        public List<ZeroShotDetection> detections;
    }

    public enum AnchorMode { BoundingBox2D, Spatial3D, Both }

    [Header("Endpoint")]
    [SerializeField] string endpointUrl = "http://127.0.0.1:8000/detect";

    [Header("Input")]
    [SerializeField] ZeroShotCameraSource cameraSource;
    [SerializeField] string textPrompt = "a person";
    [Range(0f, 1f)] public float threshold = 0.3f;
    [Range(1, 200)] public int maxDetections = 50;

    [Header("UI")]
    [SerializeField] RawImage previewImage;
    [SerializeField] RectTransform boundingBoxContainer;
    [SerializeField] InputField promptInput;
    [SerializeField] Button sendButton;
    [SerializeField] Text statusText;

    [Header("Anchors")]
    [SerializeField] AnchorMode anchorMode = AnchorMode.BoundingBox2D;
    [SerializeField] GameObject anchorPrefab;
    [SerializeField] Camera worldCamera;
    [SerializeField] float fallbackDepth = 2.0f;
    [SerializeField] LayerMask anchorRaycastMask = ~0;

    readonly List<GameObject> _spawnedBoxes = new List<GameObject>();
    readonly List<GameObject> _spawnedAnchors = new List<GameObject>();
    Font _defaultFont;

    void Start()
    {
        _defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (sendButton != null)
            sendButton.onClick.AddListener(SendRequest);

        if ((anchorMode == AnchorMode.Spatial3D || anchorMode == AnchorMode.Both) && anchorPrefab == null)
        {
            anchorPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            anchorPrefab.name = "ZeroShotAnchor";
            anchorPrefab.transform.localScale = Vector3.one * 0.1f;
            anchorPrefab.SetActive(false);
        }
    }

    public void SendRequest()
    {
        StartCoroutine(SendRequestRoutine());
    }

    IEnumerator SendRequestRoutine()
    {
        if (cameraSource == null || cameraSource.CurrentTexture == null)
        {
            SetStatus("No camera texture.");
            yield break;
        }

        var tex = cameraSource.CurrentTexture;
        var jpg = EncodeTextureToJpg(tex);
        if (jpg == null || jpg.Length == 0)
        {
            SetStatus("Failed to encode image.");
            yield break;
        }

        var request = new ZeroShotRequest
        {
            prompt = promptInput != null && !string.IsNullOrEmpty(promptInput.text) ? promptInput.text : textPrompt,
            threshold = threshold,
            max_detections = maxDetections,
            image_base64 = Convert.ToBase64String(jpg)
        };

        string json = JsonUtility.ToJson(request);
        using (var uwr = new UnityWebRequest(endpointUrl, "POST"))
        {
            uwr.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/json");

            SetStatus("Sending request...");
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                SetStatus($"Request failed: {uwr.error}");
                yield break;
            }

            var response = JsonUtility.FromJson<ZeroShotResponse>(uwr.downloadHandler.text);
            if (response == null || response.detections == null)
            {
                SetStatus("No detections.");
                ClearVisuals();
                yield break;
            }

            DrawDetections(response);
        }
    }

    void DrawDetections(ZeroShotResponse response)
    {
        ClearVisuals();

        var container = boundingBoxContainer != null ? boundingBoxContainer : previewImage?.rectTransform;
        if (container == null)
        {
            SetStatus("No UI container for bounding boxes.");
            return;
        }

        int frameWidth = response.frame_width > 0 ? response.frame_width : previewImage.texture.width;
        int frameHeight = response.frame_height > 0 ? response.frame_height : previewImage.texture.height;

        var imgRect = previewImage != null ? previewImage.rectTransform : container;
        float scaleX = imgRect.rect.width / frameWidth;
        float scaleY = imgRect.rect.height / frameHeight;

        foreach (var det in response.detections)
        {
            if (det == null || det.bbox == null || det.bbox.Length < 4)
                continue;
            if (det.confidence < threshold)
                continue;

            float x = det.bbox[0] * scaleX;
            float y = det.bbox[1] * scaleY;
            float w = det.bbox[2] * scaleX;
            float h = det.bbox[3] * scaleY;

            if (anchorMode == AnchorMode.BoundingBox2D || anchorMode == AnchorMode.Both)
                _spawnedBoxes.Add(CreateBoundingBox(container, x, y, w, h, det.label, det.confidence));

            if ((anchorMode == AnchorMode.Spatial3D || anchorMode == AnchorMode.Both) && anchorPrefab != null)
            {
                TrySpawnAnchor(det, frameWidth, frameHeight, imgRect);
            }
        }

        SetStatus($"Detections: {_spawnedBoxes.Count}");
    }

    GameObject CreateBoundingBox(RectTransform parent, float x, float y, float w, float h, string label, float confidence)
    {
        var box = new GameObject("BBox", typeof(RectTransform), typeof(Image), typeof(Outline));
        var rt = box.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);

        var img = box.GetComponent<Image>();
        img.color = new Color(0, 0, 0, 0);

        var outline = box.GetComponent<Outline>();
        outline.effectColor = Color.green;
        outline.effectDistance = new Vector2(2, 2);

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.SetParent(rt, false);
        labelRt.anchorMin = new Vector2(0, 1);
        labelRt.anchorMax = new Vector2(0, 1);
        labelRt.pivot = new Vector2(0, 1);
        labelRt.anchoredPosition = new Vector2(4, -4);
        labelRt.sizeDelta = new Vector2(Mathf.Max(120, w), 20);

        var text = labelGo.GetComponent<Text>();
        text.font = _defaultFont;
        text.fontSize = 14;
        text.color = Color.green;
        text.text = string.IsNullOrEmpty(label) ? $"{confidence:F2}" : $"{label} ({confidence:F2})";

        return box;
    }

    void TrySpawnAnchor(ZeroShotDetection det, int frameWidth, int frameHeight, RectTransform previewRect)
    {
        if (worldCamera == null)
            worldCamera = Camera.main;
        if (worldCamera == null)
            return;

        float cx = det.bbox[0] + det.bbox[2] * 0.5f;
        float cy = det.bbox[1] + det.bbox[3] * 0.5f;

        var corners = new Vector3[4];
        previewRect.GetWorldCorners(corners);
        // corners: 0 bottom-left, 2 top-right
        float nx = Mathf.Clamp01(cx / frameWidth);
        float ny = Mathf.Clamp01(cy / frameHeight);

        float screenX = Mathf.Lerp(corners[0].x, corners[2].x, nx);
        float screenY = Mathf.Lerp(corners[0].y, corners[2].y, 1f - ny);

        Ray ray = worldCamera.ScreenPointToRay(new Vector3(screenX, screenY, 0));
        Vector3 spawnPos = ray.GetPoint(fallbackDepth);

        if (Physics.Raycast(ray, out var hit, 50f, anchorRaycastMask))
            spawnPos = hit.point;

        var anchor = Instantiate(anchorPrefab, spawnPos, Quaternion.identity);
        if (!anchor.activeSelf)
            anchor.SetActive(true);
        _spawnedAnchors.Add(anchor);
    }

    void ClearVisuals()
    {
        foreach (var go in _spawnedBoxes)
            if (go) Destroy(go);
        _spawnedBoxes.Clear();

        foreach (var go in _spawnedAnchors)
            if (go) Destroy(go);
        _spawnedAnchors.Clear();
    }

    static byte[] EncodeTextureToJpg(Texture texture, int quality = 80)
    {
        if (texture == null)
            return null;

        var temp = RenderTexture.GetTemporary(texture.width, texture.height, 0);
        Graphics.Blit(texture, temp);
        var prev = RenderTexture.active;
        RenderTexture.active = temp;

        var tex2D = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        tex2D.ReadPixels(new Rect(0, 0, temp.width, temp.height), 0, 0);
        tex2D.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(temp);

        return tex2D.EncodeToJPG(quality);
    }

    void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
