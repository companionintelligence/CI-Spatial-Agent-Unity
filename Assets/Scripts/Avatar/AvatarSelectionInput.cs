using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class AvatarSelectionInput : MonoBehaviour
{
    [Header("Selection")]
    [SerializeField] bool enableKeyboard = true;
    [SerializeField] bool enableXrInput = true;
    [SerializeField] float inputCooldown = 0.35f;

    [Header("Keyboard")]
    [SerializeField] KeyCode nextKey = KeyCode.RightArrow;
    [SerializeField] KeyCode prevKey = KeyCode.LeftArrow;
    [SerializeField] KeyCode randomKey = KeyCode.R;

    [Header("Animation Keys")]
    [SerializeField] KeyCode idleKey = KeyCode.Alpha1;
    [SerializeField] KeyCode locomotionKey = KeyCode.Alpha2;
    [SerializeField] KeyCode danceKey = KeyCode.Alpha3;
    [SerializeField] KeyCode expressionKey = KeyCode.Alpha4;

    AvatarBootstrap _bootstrap;
    AvatarAnimationMixer _mixer;
    float _nextInputTime;

    InputDevice _left;
    InputDevice _right;
    bool _leftPrimaryPrev;
    bool _rightPrimaryPrev;
    bool _leftSecondaryPrev;
    bool _rightSecondaryPrev;

    static AvatarSelectionInput s_Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (s_Instance != null)
            return;
        var go = new GameObject("AvatarSelectionInput");
        DontDestroyOnLoad(go);
        s_Instance = go.AddComponent<AvatarSelectionInput>();
    }

    void Start()
    {
        _bootstrap = FindObjectOfType<AvatarBootstrap>();
        if (_bootstrap != null)
        {
            _bootstrap.AvatarLoaded += OnAvatarLoaded;
            OnAvatarLoaded(_bootstrap.GetCurrentAvatar());
        }
    }

    void OnDestroy()
    {
        if (_bootstrap != null)
            _bootstrap.AvatarLoaded -= OnAvatarLoaded;
    }

    void Update()
    {
        if (Time.time < _nextInputTime)
            return;

        bool prev = false;
        bool next = false;
        bool random = false;
        bool idle = false;
        bool locomotion = false;
        bool dance = false;
        bool expression = false;

        if (enableKeyboard)
        {
            prev |= Input.GetKeyDown(prevKey);
            next |= Input.GetKeyDown(nextKey);
            random |= Input.GetKeyDown(randomKey);
            idle |= Input.GetKeyDown(idleKey);
            locomotion |= Input.GetKeyDown(locomotionKey);
            dance |= Input.GetKeyDown(danceKey);
            expression |= Input.GetKeyDown(expressionKey);
        }

        if (enableXrInput)
        {
            EnsureDevices();
            var leftPrimary = GetButton(_left, CommonUsages.primaryButton);
            var rightPrimary = GetButton(_right, CommonUsages.primaryButton);
            var leftSecondary = GetButton(_left, CommonUsages.secondaryButton);
            var rightSecondary = GetButton(_right, CommonUsages.secondaryButton);

            if (leftPrimary && !_leftPrimaryPrev)
                prev = true;
            if (rightPrimary && !_rightPrimaryPrev)
                next = true;
            if (leftSecondary && !_leftSecondaryPrev)
                expression = true;
            if (rightSecondary && !_rightSecondaryPrev)
                dance = true;

            _leftPrimaryPrev = leftPrimary;
            _rightPrimaryPrev = rightPrimary;
            _leftSecondaryPrev = leftSecondary;
            _rightSecondaryPrev = rightSecondary;
        }

        if (prev || next || random || idle || locomotion || dance || expression)
            _nextInputTime = Time.time + inputCooldown;

        if (prev) SelectOffset(-1);
        if (next) SelectOffset(1);
        if (random) SelectRandom();

        if (idle) PlayCategory(AvatarAnimCategory.Idle);
        if (locomotion) PlayCategory(AvatarAnimCategory.Locomotion);
        if (dance) PlayCategory(AvatarAnimCategory.Dance);
        if (expression) PlayCategory(AvatarAnimCategory.Expression);
    }

    void OnAvatarLoaded(GameObject avatar)
    {
        _mixer = null;
        if (avatar == null)
            return;
        _mixer = avatar.GetComponent<AvatarAnimationMixer>();
        if (_mixer == null)
            _mixer = avatar.GetComponentInChildren<AvatarAnimationMixer>();
    }

    void SelectOffset(int delta)
    {
        if (_bootstrap == null)
            _bootstrap = FindObjectOfType<AvatarBootstrap>();
        if (_bootstrap == null)
            return;

        _bootstrap.RefreshAvatars();
        int count = _bootstrap.GetAvatarCount();
        if (count <= 0)
            return;

        int nextIndex = _bootstrap.GetCurrentIndex() + delta;
        if (nextIndex < 0) nextIndex = count - 1;
        if (nextIndex >= count) nextIndex = 0;
        _bootstrap.LoadAvatarByIndex(nextIndex);
    }

    void SelectRandom()
    {
        if (_bootstrap == null)
            _bootstrap = FindObjectOfType<AvatarBootstrap>();
        if (_bootstrap == null)
            return;

        _bootstrap.RefreshAvatars();
        int count = _bootstrap.GetAvatarCount();
        if (count <= 0)
            return;

        int nextIndex = Random.Range(0, count);
        _bootstrap.LoadAvatarByIndex(nextIndex);
    }

    void PlayCategory(AvatarAnimCategory category)
    {
        if (_mixer == null)
            _mixer = FindObjectOfType<AvatarAnimationMixer>();
        if (_mixer == null)
            return;
        _mixer.PlayCategory(category);
    }

    void EnsureDevices()
    {
        if (!_left.isValid)
            _left = FindDevice(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller);
        if (!_right.isValid)
            _right = FindDevice(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller);
    }

    static InputDevice FindDevice(InputDeviceCharacteristics characteristics)
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(characteristics, devices);
        return devices.Count > 0 ? devices[0] : default;
    }

    static bool GetButton(InputDevice device, InputFeatureUsage<bool> usage)
    {
        if (!device.isValid)
            return false;
        return device.TryGetFeatureValue(usage, out var value) && value;
    }
}
