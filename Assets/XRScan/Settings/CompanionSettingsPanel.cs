using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using XRScan.Core;

namespace XRScan.Settings
{
    /// <summary>
    /// Simple in-headset settings panel for the real backends: LLM / speech / CV server endpoints, LLM model,
    /// speech provider and the explicit cloud-speech opt-in, plus permission status. Every change is stored as a
    /// PlayerPrefs override on top of <see cref="CompanionConfig"/> (logic in <see cref="CompanionSettingsModel"/>).
    /// <para>Controls: left-controller <b>menu</b> button (F1 in the Editor) opens / closes; <b>thumbstick</b>
    /// up / down (arrow keys) selects, left / right changes a choice; <b>A / X</b> (Enter) edits or toggles;
    /// <b>B / Y</b> (Esc) closes or cancels. Endpoint fields open the system keyboard on the headset, or take text
    /// from a physical keyboard. The panel is built at runtime (world-space uGUI), so the scene only needs this
    /// component. It opens by itself at start-up while the LLM or speech server is not configured yet.</para>
    /// <para>It also carries the AI disclosure (<see cref="AiDisclosure"/>): a notice at first launch, before
    /// anything else and whatever the server settings are, closed with A / Enter, and the same line at the top of
    /// the panel whenever it is open.</para>
    /// </summary>
    public class CompanionSettingsPanel : MonoBehaviour
    {
        [SerializeField] private CompanionConfig config;
        [SerializeField] private RuntimePermissions permissions;

        [Tooltip("Disabled while the panel is open (e.g. the scan / talk input handler) so controller buttons only drive the panel.")]
        [SerializeField] private Behaviour[] disableWhileOpen = new Behaviour[0];

        [SerializeField] private float distance = 0.8f;
        [SerializeField] private float noticeSeconds = 6f;
        [SerializeField] private float autoOpenDelaySeconds = 2f;

        private const float UnitsToMeters = 0.0006f;
        private const float StickThreshold = 0.6f;
        private const float FirstRepeatDelay = 0.4f;
        private const float RepeatInterval = 0.15f;
        private const int MaxEditLength = 512;
        private static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.07f, 0.9f);

        private enum RowType { Setting, Permissions, Close }

        private sealed class PanelRow
        {
            public RowType Type;
            public SettingRow Setting;
            public string Label = string.Empty;
            public string Value = string.Empty;
            public string Detail = string.Empty;
            public bool Warning;
        }

        private CompanionSettingsModel _model;
        private readonly List<PanelRow> _rows = new List<PanelRow>();
        private readonly Dictionary<Behaviour, bool> _suspended = new Dictionary<Behaviour, bool>();
        private int _selected;
        private bool _open;

        private bool _editing;
        private SettingId _editId;
        private string _editBuffer = string.Empty;
        private TouchScreenKeyboard _systemKeyboard;
        private Keyboard _typingKeyboard;

        private string _notice;
        private float _noticeUntil;
        private bool _noticeRendered;

        private InputAction _toggle;
        private InputAction _navigate;
        private InputAction _submit;
        private InputAction _back;
        private Vector2Int _heldDirection;
        private float _nextRepeat;

        private Font _font;
        private RectTransform _panel;
        private RectTransform _toast;
        private RectTransform _aiNotice;
        private Text _body;
        private Text _footer;
        private Text _toastText;
        private Text _aiNoticeText;
        private bool _aiNoticeOpen;

        public bool IsOpen => _open;

        /// <summary>True while the first-launch AI notice is shown.</summary>
        public bool IsAiNoticeOpen => _aiNoticeOpen;

        /// <summary>Code-side wiring (the scene normally sets these).</summary>
        public void Configure(CompanionConfig companionConfig, RuntimePermissions runtimePermissions)
        {
            config = companionConfig;
            permissions = runtimePermissions;
            _model = config != null ? new CompanionSettingsModel(config) : null;
        }

        private void Awake()
        {
            // Unresolved bindings (a control a profile lacks, a missing keyboard) are ignored, so several
            // candidates just widen compatibility across the Meta Touch / Touch Plus profiles.
            _toggle = new InputAction("XRScan Settings", InputActionType.Button);
            _toggle.AddBinding("<XRController>{LeftHand}/menu");
            _toggle.AddBinding("<XRController>{LeftHand}/menuButton");
            _toggle.AddBinding("<Keyboard>/f1");

            _navigate = new InputAction("XRScan Settings Navigate", InputActionType.Value, expectedControlType: "Vector2");
            _navigate.AddBinding("<XRController>{RightHand}/thumbstick");
            _navigate.AddBinding("<XRController>{LeftHand}/thumbstick");

            _submit = new InputAction("XRScan Settings Submit", InputActionType.Button);
            _submit.AddBinding("<XRController>{RightHand}/primaryButton");
            _submit.AddBinding("<XRController>{LeftHand}/primaryButton");

            _back = new InputAction("XRScan Settings Back", InputActionType.Button);
            _back.AddBinding("<XRController>{RightHand}/secondaryButton");
            _back.AddBinding("<XRController>{LeftHand}/secondaryButton");
        }

        private void OnEnable()
        {
            _toggle?.Enable();
            _navigate?.Enable();
            _submit?.Enable();
            _back?.Enable();
            RuntimePermissions.Notice += ShowNotice;
            UserNotices.Posted += ShowNotice;
        }

        private void OnDisable()
        {
            RuntimePermissions.Notice -= ShowNotice;
            UserNotices.Posted -= ShowNotice;
            if (_open) Close();
            EndEdit();
            _toggle?.Disable();
            _navigate?.Disable();
            _submit?.Disable();
            _back?.Disable();
        }

        private void OnDestroy()
        {
            _toggle?.Dispose();
            _navigate?.Dispose();
            _submit?.Dispose();
            _back?.Dispose();
        }

        private IEnumerator Start()
        {
            if (config == null)
            {
                Debug.LogWarning("[SettingsPanel] No CompanionConfig assigned; the settings panel is disabled.");
                enabled = false;
                yield break;
            }

            if (_model == null) _model = new CompanionSettingsModel(config);

            // Give head tracking a moment so the notice and the panel appear in front of the user.
            yield return new WaitForSecondsRealtime(autoOpenDelaySeconds);

            // The AI notice comes first and does not look at the server settings.
            if (AiDisclosure.NoticeNeeded)
            {
                ShowAiNotice();
                while (_aiNoticeOpen) yield return null;
            }

            if (!config.openSettingsWhenUnconfigured) yield break;
            if (!_open && config.NeedsSetup())
            {
                Open();
                ShowNotice("Set the LLM and speech servers to get started.");
            }
        }

        public void Open()
        {
            if (_model == null || _aiNoticeOpen) return;
            EnsureUi();
            _open = true;
            _panel.gameObject.SetActive(true);
            PlaceInFront(_panel, distance, -0.05f);
            SuspendInput();
            Refresh();
        }

        public void Close()
        {
            EndEdit();
            _model?.CancelPending();
            _open = false;
            if (_panel != null) _panel.gameObject.SetActive(false);
            ResumeInput();
        }

        /// <summary>Shows the AI notice in front of the user until A / Enter closes it; scanning and talking wait.</summary>
        public void ShowAiNotice()
        {
            if (_open) Close();
            EnsureUi();
            _aiNoticeText.text = "<b>" + Clean(AiDisclosure.Line) + "</b>\n\n<size=22><color=#9aa4b2>A / Enter: OK</color></size>";
            _aiNotice.gameObject.SetActive(true);
            PlaceInFront(_aiNotice, distance, -0.05f);
            _aiNoticeOpen = true;
            SuspendInput();
        }

        /// <summary>Closes the AI notice and records that the user has seen it, so it shows only once.</summary>
        public void AcknowledgeAiNotice()
        {
            if (!_aiNoticeOpen) return;
            AiDisclosure.Acknowledge();
            _aiNoticeOpen = false;
            if (_aiNotice != null) _aiNotice.gameObject.SetActive(false);
            ResumeInput();
        }

        private void SuspendInput()
        {
            foreach (var behaviour in disableWhileOpen)
            {
                if (behaviour == null || _suspended.ContainsKey(behaviour)) continue;
                _suspended[behaviour] = behaviour.enabled;
                behaviour.enabled = false;
            }
        }

        private void ResumeInput()
        {
            if (_open || _aiNoticeOpen) return;
            foreach (var pair in _suspended)
            {
                if (pair.Key != null) pair.Key.enabled = pair.Value;
            }
            _suspended.Clear();
        }

        /// <summary>Shows a short message: in the panel footer when open, else as a head-locked toast.</summary>
        public void ShowNotice(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _notice = message;
            _noticeUntil = Time.unscaledTime + noticeSeconds;
            if (_open)
            {
                Render();
                return;
            }

            EnsureUi();
            _toastText.text = Clean(message);
            _toast.gameObject.SetActive(true);
            PlaceInFront(_toast, 1.0f, -0.25f);
        }

        private void Update()
        {
            if (_model == null) return;

            if (_aiNoticeOpen)
            {
                var keys = Keyboard.current;
                if (_submit.WasPressedThisFrame() ||
                    (keys != null && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)))
                {
                    AcknowledgeAiNotice();
                }
                else FollowHead(_aiNotice, distance, -0.05f);
            }
            else if (_editing) UpdateEditing();
            else if (_toggle.WasPressedThisFrame())
            {
                if (_open) Close();
                else Open();
            }
            else if (_open) UpdateNavigation();

            UpdateNotice();
        }

        private void UpdateNavigation()
        {
            var keyboard = Keyboard.current;
            if (_back.WasPressedThisFrame() || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
            {
                Close();
                return;
            }

            if (_submit.WasPressedThisFrame() ||
                (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)))
            {
                ActivateSelected();
                return;
            }

            var direction = ReadDirection(keyboard);
            if (direction.y != 0) MoveSelection(direction.y);
            else if (direction.x != 0) CycleSelected(direction.x);
        }

        /// <summary>Keyboard arrows step once per press; the thumbstick steps on push and auto-repeats up / down while held.</summary>
        private Vector2Int ReadDirection(Keyboard keyboard)
        {
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.wasPressedThisFrame) return new Vector2Int(0, -1);
                if (keyboard.downArrowKey.wasPressedThisFrame) return new Vector2Int(0, 1);
                if (keyboard.leftArrowKey.wasPressedThisFrame) return new Vector2Int(-1, 0);
                if (keyboard.rightArrowKey.wasPressedThisFrame) return new Vector2Int(1, 0);
            }

            Vector2 stick = _navigate.ReadValue<Vector2>();
            var held = Vector2Int.zero;
            if (Mathf.Abs(stick.y) >= StickThreshold && Mathf.Abs(stick.y) >= Mathf.Abs(stick.x)) held.y = stick.y > 0f ? -1 : 1;
            else if (Mathf.Abs(stick.x) >= StickThreshold) held.x = stick.x > 0f ? 1 : -1;

            if (held == Vector2Int.zero)
            {
                _heldDirection = Vector2Int.zero;
                return Vector2Int.zero;
            }

            float now = Time.unscaledTime;
            if (held != _heldDirection)
            {
                _heldDirection = held;
                _nextRepeat = now + FirstRepeatDelay;
                return held;
            }

            if (held.y != 0 && now >= _nextRepeat) // left / right changes a value: no auto-repeat
            {
                _nextRepeat = now + RepeatInterval;
                return held;
            }
            return Vector2Int.zero;
        }

        private void MoveSelection(int delta)
        {
            if (_rows.Count == 0) return;
            _model.CancelPending();
            _selected = (_selected + delta + _rows.Count) % _rows.Count;
            Refresh();
        }

        private void CycleSelected(int direction)
        {
            var row = SelectedRow();
            if (row?.Setting == null || row.Setting.Kind != SettingKind.Choice) return;
            ShowNotice(_model.Cycle(row.Setting.Id, direction));
            Refresh();
        }

        private void ActivateSelected()
        {
            var row = SelectedRow();
            if (row == null) return;

            switch (row.Type)
            {
                case RowType.Close:
                    Close();
                    return;
                case RowType.Permissions:
                    if (permissions != null)
                    {
                        permissions.RequestMissing();
                        ShowNotice("Checking permissions...");
                    }
                    Refresh();
                    return;
            }

            var setting = row.Setting;
            switch (setting.Kind)
            {
                case SettingKind.Endpoint:
                case SettingKind.Secret:
                    BeginEdit(setting.Id);
                    return;
                case SettingKind.Info:
                    return;
                default:
                    ShowNotice(_model.Activate(setting.Id));
                    Refresh();
                    return;
            }
        }

        private void BeginEdit(SettingId id)
        {
            _editing = true;
            _editId = id;
            _editBuffer = _model.GetEditableText(id) ?? string.Empty;

            if (TouchScreenKeyboard.isSupported)
            {
                bool secret = IsSecret(id);
                _systemKeyboard = TouchScreenKeyboard.Open(_editBuffer,
                    secret ? TouchScreenKeyboardType.Default : TouchScreenKeyboardType.URL, false, false, false, false,
                    secret ? "API token" : "https://host:port");
            }

            _typingKeyboard = Keyboard.current;
            if (_typingKeyboard != null) _typingKeyboard.onTextInput += OnTextInput;
            Refresh();
        }

        private void UpdateEditing()
        {
            if (_systemKeyboard != null)
            {
                switch (_systemKeyboard.status)
                {
                    case TouchScreenKeyboard.Status.Visible:
                        string typed = _systemKeyboard.text ?? string.Empty;
                        if (typed != _editBuffer)
                        {
                            _editBuffer = typed;
                            Refresh();
                        }
                        break;
                    case TouchScreenKeyboard.Status.Done:
                        _editBuffer = _systemKeyboard.text ?? string.Empty;
                        CommitEdit();
                        return;
                    case TouchScreenKeyboard.Status.Canceled:
                        CancelEdit();
                        return;
                    case TouchScreenKeyboard.Status.LostFocus:
                        // Keep what was typed; A saves, B cancels.
                        _editBuffer = _systemKeyboard.text ?? _editBuffer;
                        _systemKeyboard = null;
                        Refresh();
                        break;
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    CancelEdit();
                    return;
                }
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    CommitEdit();
                    return;
                }
                if (keyboard.backspaceKey.wasPressedThisFrame && _editBuffer.Length > 0)
                {
                    _editBuffer = _editBuffer.Substring(0, _editBuffer.Length - 1);
                    Refresh();
                }
            }

            if (_submit.WasPressedThisFrame()) CommitEdit();
            else if (_back.WasPressedThisFrame()) CancelEdit();
        }

        private void OnTextInput(char c)
        {
            if (!_editing || char.IsControl(c) || _editBuffer.Length >= MaxEditLength) return;
            _editBuffer += c;
            Refresh();
        }

        private static bool IsSecret(SettingId id) => id == SettingId.CvToken;

        private void CommitEdit()
        {
            string text = _editBuffer;
            var id = _editId;
            EndEdit();
            string message;
            if (IsSecret(id)) _model.TrySetSecret(id, text, out message);
            else _model.TrySetEndpoint(id, text, out message);
            ShowNotice(message);
            Refresh();
        }

        private void CancelEdit()
        {
            EndEdit();
            ShowNotice("Edit cancelled; nothing changed.");
            Refresh();
        }

        private void EndEdit()
        {
            if (!_editing) return;
            _editing = false;
            if (_systemKeyboard != null)
            {
                if (_systemKeyboard.active) _systemKeyboard.active = false;
                _systemKeyboard = null;
            }
            if (_typingKeyboard != null)
            {
                _typingKeyboard.onTextInput -= OnTextInput;
                _typingKeyboard = null;
            }
        }

        private PanelRow SelectedRow() => _selected >= 0 && _selected < _rows.Count ? _rows[_selected] : null;

        private void Refresh()
        {
            if (_model == null) return;

            _rows.Clear();
            foreach (var setting in _model.BuildRows())
            {
                _rows.Add(new PanelRow
                {
                    Type = RowType.Setting,
                    Setting = setting,
                    Label = setting.Label,
                    Value = setting.Value,
                    Detail = setting.Detail,
                    Warning = setting.IsWarning
                });
            }

            if (permissions != null)
            {
                _rows.Add(new PanelRow
                {
                    Type = RowType.Permissions,
                    Label = "Permissions",
                    Value = permissions.Summary(),
                    Detail = "A: ask again for anything missing (opens the system settings when blocked).",
                    Warning = AnyPermissionMissing()
                });
            }

            _rows.Add(new PanelRow { Type = RowType.Close, Label = "Close" });
            _selected = Mathf.Clamp(_selected, 0, _rows.Count - 1);
            if (_open) Render();
        }

        private bool AnyPermissionMissing()
        {
            foreach (var permission in new[] { AppPermissions.Microphone, AppPermissions.HeadsetCamera, AppPermissions.SpatialData })
            {
                var state = permissions.GetState(permission);
                if (state == PermissionState.Denied || state == PermissionState.DeniedPermanently) return true;
            }
            return false;
        }

        private void Render()
        {
            if (_model == null) return;
            EnsureUi();

            var sb = new StringBuilder(2048);
            sb.Append("<b>Companion settings</b>   <size=20><color=#9aa4b2>")
              .Append(_model.IsDevelopmentBuild ? "development build" : "release build")
              .Append(" - changes are saved on this headset</color></size>\n")
              .Append("<size=24><color=#c8d0da>").Append(Clean(AiDisclosure.Line)).Append("</color></size>\n\n");

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                bool selected = i == _selected;
                string value = selected && _editing
                    ? (IsSecret(_editId) ? new string('*', _editBuffer.Length) : Clean(_editBuffer)) + "<color=#ffd54a>_</color>"
                    : Clean(row.Value);

                sb.Append(selected ? "<color=#ffd54a>> " : "<color=#e8eaed>   ").Append(Clean(row.Label));
                if (value.Length > 0) sb.Append(":  ").Append(value);
                sb.Append("</color>\n");

                if (row.Detail.Length > 0 && (selected || row.Warning))
                {
                    sb.Append("<size=20><color=").Append(row.Warning ? "#ff9e80" : "#9aa4b2").Append(">      ")
                      .Append(Clean(row.Detail)).Append("</color></size>\n");
                }
            }

            _body.text = sb.ToString();

            string help = _editing
                ? (IsSecret(_editId)
                    ? "Type the token. A / Enter: save   B / Esc: cancel   (empty = remove it)"
                    : "Type the address. A / Enter: save   B / Esc: cancel   (empty = use the default)")
                : "Thumbstick: select / change   A: edit or toggle   B or menu: close";
            _noticeRendered = HasNotice();
            _footer.text = (_noticeRendered ? "<color=#ffd54a>" + Clean(_notice) + "</color>\n" : string.Empty) +
                           "<size=20><color=#9aa4b2>" + help + "</color></size>";
        }

        private bool HasNotice() => !string.IsNullOrEmpty(_notice) && Time.unscaledTime < _noticeUntil;

        private void UpdateNotice()
        {
            bool active = HasNotice();
            if (_open)
            {
                if (_noticeRendered && !active) Render(); // clear an expired notice from the footer
                if (_toast != null && _toast.gameObject.activeSelf) _toast.gameObject.SetActive(false);
                return;
            }

            if (_toast == null) return;
            if (_toast.gameObject.activeSelf != active) _toast.gameObject.SetActive(active);
            if (active) FollowHead(_toast, 1.0f, -0.25f);
        }

        private void EnsureUi()
        {
            if (_panel != null) return;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _panel = CreateCanvas("SettingsPanelCanvas", new Vector2(1100f, 1040f));
            _body = CreateText(_panel, "Body", 28, TextAnchor.UpperLeft, new Vector2(0f, 0.2f), Vector2.one, 36f);
            _footer = CreateText(_panel, "Footer", 24, TextAnchor.LowerLeft, Vector2.zero, new Vector2(1f, 0.2f), 36f);
            _panel.gameObject.SetActive(false);

            _toast = CreateCanvas("SettingsNoticeCanvas", new Vector2(1100f, 160f));
            _toastText = CreateText(_toast, "Notice", 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, 24f);
            _toast.gameObject.SetActive(false);

            _aiNotice = CreateCanvas("AiNoticeCanvas", new Vector2(1000f, 320f));
            _aiNoticeText = CreateText(_aiNotice, "Notice", 32, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, 48f);
            _aiNotice.gameObject.SetActive(false);
        }

        private RectTransform CreateCanvas(string objectName, Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 500;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f; // crisper text in the headset

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one * UnitsToMeters;

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(go.transform, false);
            var backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            var image = background.GetComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = false;
            return rect;
        }

        private Text CreateText(RectTransform parent, string objectName, int fontSize, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax, float padding)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(padding, padding * 0.5f);
            rect.offsetMax = new Vector2(-padding, -padding * 0.5f);

            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void PlaceInFront(Transform target, float meters, float heightOffset)
        {
            HeadPose(meters, heightOffset, out var position, out var rotation);
            target.SetPositionAndRotation(position, rotation);
        }

        private static void FollowHead(Transform target, float meters, float heightOffset)
        {
            HeadPose(meters, heightOffset, out var position, out var rotation);
            float t = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime);
            target.SetPositionAndRotation(Vector3.Lerp(target.position, position, t), Quaternion.Slerp(target.rotation, rotation, t));
        }

        /// <summary>A yaw-only pose in front of the user's head (UI faces the user).</summary>
        private static void HeadPose(float meters, float heightOffset, out Vector3 position, out Quaternion rotation)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                position = new Vector3(0f, 1.4f + heightOffset, meters);
                rotation = Quaternion.identity;
                return;
            }

            var head = cam.transform;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();

            position = head.position + forward * meters + Vector3.up * heightOffset;
            rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        /// <summary>Neutralizes rich-text markup in user-provided / external strings.</summary>
        private static string Clean(string text) =>
            string.IsNullOrEmpty(text) ? string.Empty : text.Replace('<', '‹').Replace('>', '›');
    }
}
