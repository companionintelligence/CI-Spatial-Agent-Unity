using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ZeroShotBootstrap : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] Vector2 previewSize = new Vector2(640, 360);
    [SerializeField] Vector2 previewPosition = new Vector2(10, -10);
    [SerializeField] ZeroShotBackendConfig backendConfig;

    void Start()
    {
        EnsureEventSystem();
        if (backendConfig == null)
            backendConfig = Resources.Load<ZeroShotBackendConfig>("ZeroShotBackendConfig");
        var canvas = BuildCanvas();
        var preview = BuildPreview(canvas.transform);
        var boundingContainer = BuildBoundingContainer(preview.transform);
        var promptInput = BuildPromptInput(canvas.transform);
        var sendButton = BuildSendButton(canvas.transform);
        var statusText = BuildStatusText(canvas.transform);

        var cameraSource = gameObject.AddComponent<ZeroShotCameraSource>();
        cameraSource.SendMessage("StartCamera", SendMessageOptions.DontRequireReceiver);
        cameraSource.GetType().GetField("previewImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(cameraSource, preview.GetComponent<RawImage>());

        var detector = gameObject.AddComponent<ZeroShotDetector>();
        detector.SendMessage("", SendMessageOptions.DontRequireReceiver);

        SetField(detector, "cameraSource", cameraSource);
        SetField(detector, "previewImage", preview.GetComponent<RawImage>());
        SetField(detector, "boundingBoxContainer", boundingContainer);
        SetField(detector, "promptInput", promptInput);
        SetField(detector, "sendButton", sendButton);
        SetField(detector, "statusText", statusText);
        SetField(detector, "worldCamera", Camera.main);
        if (backendConfig != null)
            detector.ApplyConfig(backendConfig);
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(go);
    }

    Canvas BuildCanvas()
    {
        var canvasGO = new GameObject("ZeroShotCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        return canvas;
    }

    GameObject BuildPreview(Transform parent)
    {
        var go = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = previewPosition;
        rt.sizeDelta = previewSize;
        return go;
    }

    RectTransform BuildBoundingContainer(Transform parent)
    {
        var go = new GameObject("BoundingBoxes", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    InputField BuildPromptInput(Transform parent)
    {
        var defaultPrompt = backendConfig != null && !string.IsNullOrEmpty(backendConfig.defaultPrompt)
            ? backendConfig.defaultPrompt
            : "a person";
        var go = new GameObject("PromptInput", typeof(RectTransform), typeof(Image), typeof(InputField));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(10, -390);
        rt.sizeDelta = new Vector2(400, 30);
        var img = go.GetComponent<Image>();
        img.color = new Color(0, 0, 0, 0.5f);

        var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.SetParent(go.transform, false);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10, 6);
        textRT.offsetMax = new Vector2(-10, -6);
        var text = textGO.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = defaultPrompt;
        text.color = Color.white;

        var placeholderGO = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        var placeholderRT = placeholderGO.GetComponent<RectTransform>();
        placeholderRT.SetParent(go.transform, false);
        placeholderRT.anchorMin = Vector2.zero;
        placeholderRT.anchorMax = Vector2.one;
        placeholderRT.offsetMin = new Vector2(10, 6);
        placeholderRT.offsetMax = new Vector2(-10, -6);
        var placeholder = placeholderGO.GetComponent<Text>();
        placeholder.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        placeholder.text = "Prompt";
        placeholder.color = new Color(1, 1, 1, 0.4f);

        var input = go.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.text = defaultPrompt;
        return input;
    }

    Button BuildSendButton(Transform parent)
    {
        var go = new GameObject("SendButton", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(420, -390);
        rt.sizeDelta = new Vector2(120, 30);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.2f, 0.6f, 0.2f, 0.9f);

        var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.SetParent(go.transform, false);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        var text = textGO.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = "Detect";
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        return go.GetComponent<Button>();
    }

    Text BuildStatusText(Transform parent)
    {
        var go = new GameObject("StatusText", typeof(RectTransform), typeof(Text));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(10, -430);
        rt.sizeDelta = new Vector2(600, 30);
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = "Status";
        text.color = Color.white;
        return text;
    }

    void SetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
            field.SetValue(target, value);
    }
}
