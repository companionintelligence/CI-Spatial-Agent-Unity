using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AvatarWorldSpaceUI : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] Vector2 panelSize = new Vector2(600, 420);
    [SerializeField] float distanceFromCamera = 1.5f;
    [SerializeField] Vector3 panelOffset = new Vector3(0.4f, -0.15f, 0f);
    [SerializeField] bool followCamera = true;
    [SerializeField] float followLerp = 8f;

    [Header("Grid")]
    [SerializeField] int columns = 3;
    [SerializeField] float thumbnailSize = 120f;
    [SerializeField] float thumbnailPadding = 8f;
    [SerializeField] float labelHeight = 22f;

    [Header("Controls")]
    [SerializeField] bool disableShortcuts = true;

    AvatarBootstrap _bootstrap;
    RectTransform _panel;
    RectTransform _gridContent;
    ScrollRect _scrollRect;
    List<Button> _buttons = new List<Button>();
    string[] _avatarPaths = Array.Empty<string>();
    Texture2D[] _thumbs = Array.Empty<Texture2D>();
    int _selectedIndex = -1;

    static AvatarWorldSpaceUI s_Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (s_Instance != null)
            return;
        var go = new GameObject("AvatarWorldSpaceUI");
        DontDestroyOnLoad(go);
        s_Instance = go.AddComponent<AvatarWorldSpaceUI>();
        AvatarGalleryUI.SuppressOnGui = true;
    }

    void Start()
    {
        _bootstrap = FindObjectOfType<AvatarBootstrap>();
        if (_bootstrap != null)
        {
            _bootstrap.AvatarLoaded += OnAvatarLoaded;
            _selectedIndex = _bootstrap.GetCurrentIndex();
        }

        if (disableShortcuts)
        {
            var shortcuts = FindObjectOfType<AvatarSelectionInput>();
            shortcuts?.SetShortcutEnabled(false, false);
        }

        EnsureEventSystem();
        BuildUi();
        Refresh();
    }

    void OnDestroy()
    {
        if (_bootstrap != null)
            _bootstrap.AvatarLoaded -= OnAvatarLoaded;
    }

    void Update()
    {
        if (!followCamera)
            return;

        var cam = Camera.main;
        if (cam == null || _panel == null)
            return;

        var targetPos = cam.transform.position + cam.transform.forward * distanceFromCamera;
        targetPos += cam.transform.right * panelOffset.x + cam.transform.up * panelOffset.y;
        var targetRot = Quaternion.LookRotation(_panel.position - cam.transform.position, Vector3.up);
        _panel.position = Vector3.Lerp(_panel.position, targetPos, Time.deltaTime * followLerp);
        _panel.rotation = Quaternion.Slerp(_panel.rotation, targetRot, Time.deltaTime * followLerp);
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        var eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        TryAddXrInputModule(eventSystem.gameObject);
    }

    void TryAddXrInputModule(GameObject target)
    {
        if (target == null)
            return;
        var xrModuleType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");
        var trackedRaycasterType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
        if (xrModuleType != null && target.GetComponent(xrModuleType) == null)
        {
            var standalone = target.GetComponent<StandaloneInputModule>();
            if (standalone != null)
                Destroy(standalone);
            target.AddComponent(xrModuleType);
        }

        if (trackedRaycasterType != null)
        {
            var canvas = _panel != null ? _panel.GetComponentInParent<Canvas>() : null;
            if (canvas != null && canvas.GetComponent(trackedRaycasterType) == null)
                canvas.gameObject.AddComponent(trackedRaycasterType);
        }
    }

    void BuildUi()
    {
        var canvasGo = new GameObject("AvatarWorldCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        scaler.referencePixelsPerUnit = 100f;

        _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        _panel.SetParent(canvasGo.transform, false);
        _panel.sizeDelta = panelSize;
        _panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        PositionPanel();

        var header = BuildHeader(_panel);
        _scrollRect = BuildScrollView(_panel, header);
        _gridContent = _scrollRect.content;

        TryAddXrInputModule(EventSystem.current != null ? EventSystem.current.gameObject : null);
    }

    void PositionPanel()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            _panel.position = new Vector3(0, 1.4f, 1.5f);
            _panel.rotation = Quaternion.identity;
            return;
        }
        var targetPos = cam.transform.position + cam.transform.forward * distanceFromCamera;
        targetPos += cam.transform.right * panelOffset.x + cam.transform.up * panelOffset.y;
        _panel.position = targetPos;
        _panel.rotation = Quaternion.LookRotation(_panel.position - cam.transform.position, Vector3.up);
    }

    RectTransform BuildHeader(RectTransform parent)
    {
        var header = new GameObject("Header", typeof(RectTransform)).GetComponent<RectTransform>();
        header.SetParent(parent, false);
        header.anchorMin = new Vector2(0, 1);
        header.anchorMax = new Vector2(1, 1);
        header.pivot = new Vector2(0.5f, 1f);
        header.anchoredPosition = Vector2.zero;
        header.sizeDelta = new Vector2(0, 64);

        var title = CreateText(header, "Avatar Selector", 18, TextAnchor.MiddleLeft);
        title.rectTransform.anchorMin = new Vector2(0, 0);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1);
        title.rectTransform.offsetMin = new Vector2(12, 0);
        title.rectTransform.offsetMax = new Vector2(-12, 0);

        var buttons = new GameObject("Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
        buttons.SetParent(header, false);
        buttons.anchorMin = new Vector2(0.5f, 0);
        buttons.anchorMax = new Vector2(1, 1);
        buttons.offsetMin = new Vector2(0, 8);
        buttons.offsetMax = new Vector2(-8, -8);

        var layout = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;

        AddButton(buttons, "Prev", () => SelectOffset(-1));
        AddButton(buttons, "Next", () => SelectOffset(1));
        AddButton(buttons, "Random", SelectRandom);
        AddButton(buttons, "Idle", () => PlayCategory(AvatarAnimCategory.Idle));
        AddButton(buttons, "Walk", () => PlayCategory(AvatarAnimCategory.Locomotion));
        AddButton(buttons, "Dance", () => PlayCategory(AvatarAnimCategory.Dance));
        AddButton(buttons, "Expr", () => PlayCategory(AvatarAnimCategory.Expression));

        return header;
    }

    ScrollRect BuildScrollView(RectTransform parent, RectTransform header)
    {
        var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.SetParent(parent, false);
        scrollRt.anchorMin = new Vector2(0, 0);
        scrollRt.anchorMax = new Vector2(1, 1);
        scrollRt.offsetMin = new Vector2(10, 10);
        scrollRt.offsetMax = new Vector2(-10, -header.sizeDelta.y - 10);
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
        var viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.SetParent(scrollRt, false);
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0.05f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewportRt, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(0, 1);
        content.pivot = new Vector2(0, 1);

        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(thumbnailSize, thumbnailSize + labelHeight);
        grid.spacing = new Vector2(thumbnailPadding, thumbnailPadding);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
        grid.childAlignment = TextAnchor.UpperLeft;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewportRt;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;

        return scroll;
    }

    void Refresh()
    {
        if (_bootstrap == null)
            _bootstrap = FindObjectOfType<AvatarBootstrap>();
        if (_bootstrap == null)
            return;

        _bootstrap.RefreshAvatars();
        _avatarPaths = _bootstrap.GetAvatarPaths();
        BuildThumbnails();
        RebuildButtons();
    }

    void BuildThumbnails()
    {
        if (_avatarPaths == null)
        {
            _thumbs = Array.Empty<Texture2D>();
            return;
        }

        var folder = _avatarPaths.Length > 0 ? Path.GetDirectoryName(_avatarPaths[0]) : ResolveAvatarFolder();
        var thumbPaths = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.png") : Array.Empty<string>();
        _thumbs = new Texture2D[_avatarPaths.Length];

        for (int i = 0; i < _avatarPaths.Length; i++)
        {
            var glbName = Path.GetFileNameWithoutExtension(_avatarPaths[i]).ToLowerInvariant();
            var match = thumbPaths.FirstOrDefault(p =>
                glbName.Contains(Path.GetFileNameWithoutExtension(p).ToLowerInvariant()) ||
                Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains(glbName));

            if (!string.IsNullOrEmpty(match))
                _thumbs[i] = LoadThumbnail(match);
        }
    }

    void RebuildButtons()
    {
        foreach (Transform child in _gridContent)
            Destroy(child.gameObject);
        _buttons.Clear();

        if (_avatarPaths == null)
            return;

        for (int i = 0; i < _avatarPaths.Length; i++)
        {
            var button = CreateAvatarButton(_gridContent, i);
            _buttons.Add(button);
        }

        UpdateSelectionHighlight();
    }

    Button CreateAvatarButton(Transform parent, int index)
    {
        var go = new GameObject($"AvatarButton_{index}", typeof(RectTransform), typeof(Button), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

        var button = go.GetComponent<Button>();
        button.onClick.AddListener(() => SelectIndex(index));

        var thumbGo = new GameObject("Thumbnail", typeof(RectTransform), typeof(RawImage));
        var thumbRt = thumbGo.GetComponent<RectTransform>();
        thumbRt.SetParent(rt, false);
        thumbRt.anchorMin = new Vector2(0, 1);
        thumbRt.anchorMax = new Vector2(1, 1);
        thumbRt.pivot = new Vector2(0.5f, 1f);
        thumbRt.offsetMin = new Vector2(6, -thumbnailSize - 6);
        thumbRt.offsetMax = new Vector2(-6, -6);
        var raw = thumbGo.GetComponent<RawImage>();
        raw.texture = index < _thumbs.Length ? _thumbs[index] : null;
        raw.color = raw.texture == null ? new Color(0.3f, 0.3f, 0.3f, 0.8f) : Color.white;

        var label = CreateText(rt, Path.GetFileNameWithoutExtension(_avatarPaths[index]), 12, TextAnchor.UpperCenter);
        label.rectTransform.anchorMin = new Vector2(0, 0);
        label.rectTransform.anchorMax = new Vector2(1, 0);
        label.rectTransform.pivot = new Vector2(0.5f, 0);
        label.rectTransform.offsetMin = new Vector2(4, 2);
        label.rectTransform.offsetMax = new Vector2(-4, 2 + labelHeight);

        return button;
    }

    Text CreateText(Transform parent, string value, int size, TextAnchor anchor)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = anchor;
        text.text = value;
        text.rectTransform.SetParent(parent, false);
        return text;
    }

    Button AddButton(Transform parent, string label, Action onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(70, 28);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.2f, 0.5f, 0.8f, 0.9f);
        var button = go.GetComponent<Button>();
        button.onClick.AddListener(() => onClick?.Invoke());

        var text = CreateText(rt, label, 12, TextAnchor.MiddleCenter);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    void SelectIndex(int index)
    {
        if (_bootstrap == null)
            _bootstrap = FindObjectOfType<AvatarBootstrap>();
        if (_bootstrap == null)
            return;
        _selectedIndex = index;
        _bootstrap.LoadAvatarByIndex(index);
        UpdateSelectionHighlight();
    }

    void SelectOffset(int delta)
    {
        if (_avatarPaths == null || _avatarPaths.Length == 0)
            return;
        int next = _selectedIndex + delta;
        if (next < 0) next = _avatarPaths.Length - 1;
        if (next >= _avatarPaths.Length) next = 0;
        SelectIndex(next);
    }

    void SelectRandom()
    {
        if (_avatarPaths == null || _avatarPaths.Length == 0)
            return;
        int next = UnityEngine.Random.Range(0, _avatarPaths.Length);
        SelectIndex(next);
    }

    void PlayCategory(AvatarAnimCategory category)
    {
        var mixer = FindObjectOfType<AvatarAnimationMixer>();
        mixer?.PlayCategory(category);
    }

    void OnAvatarLoaded(GameObject avatar)
    {
        _selectedIndex = _bootstrap != null ? _bootstrap.GetCurrentIndex() : _selectedIndex;
        UpdateSelectionHighlight();
    }

    void UpdateSelectionHighlight()
    {
        if (_buttons == null || _buttons.Count == 0)
            return;
        for (int i = 0; i < _buttons.Count; i++)
        {
            var colors = _buttons[i].colors;
            colors.normalColor = i == _selectedIndex ? new Color(0.2f, 0.8f, 0.3f, 0.9f) : new Color(0.15f, 0.15f, 0.15f, 0.8f);
            _buttons[i].colors = colors;
        }
    }

    static Texture2D LoadThumbnail(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(bytes);
        tex.name = Path.GetFileNameWithoutExtension(path);
        return tex;
    }

    static string ResolveAvatarFolder()
    {
        var envPath = Environment.GetEnvironmentVariable("CI_AVATAR_PATH");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            return envPath;

#if UNITY_EDITOR
        var assetsAvatars = Path.Combine(Application.dataPath, "Avatars");
        if (Directory.Exists(assetsAvatars))
            return assetsAvatars;

        var drivePath = "/Users/liam/Library/CloudStorage/GoogleDrive-liam@lifescope.io/My Drive/CI Main Drive/AVATAR/20250301 - Embodied Avatars Geometry Art/Avatars";
        if (Directory.Exists(drivePath))
            return drivePath;
#endif

        return Path.Combine(Application.streamingAssetsPath, "Avatars");
    }
}
