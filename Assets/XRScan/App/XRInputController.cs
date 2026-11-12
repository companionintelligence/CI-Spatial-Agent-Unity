using UnityEngine;
using UnityEngine.InputSystem;

namespace XRScan.App
{
    /// <summary>
    /// Maps XR controller buttons to the two app actions: a controller <b>trigger</b> fires
    /// <see cref="AppBootstrap.Scan"/> (place a pin) and a controller <b>grip</b> toggles the voice
    /// Q&amp;A loop via <see cref="AppBootstrap.ToggleListen"/>. Bindings are defined in code against the
    /// generic <c>&lt;XRController&gt;</c> layout so they resolve across the Oculus/Meta Touch and PICO
    /// OpenXR interaction profiles without an <c>.inputactions</c> asset. Both hands are bound so the
    /// user needn't remember a side; keyboard fallbacks (Space / L) keep it drivable in the Editor.
    /// </summary>
    public class XRInputController : MonoBehaviour
    {
        [SerializeField] private AppBootstrap bootstrap;

        private InputAction _scan;
        private InputAction _listen;

        private void Awake()
        {
            if (bootstrap == null) bootstrap = FindAnyObjectByType<AppBootstrap>();

            // Button-type actions fire "performed" once when a bound control crosses the press point.
            // Unresolved bindings (e.g. a control a given profile lacks, or the keyboard on-device) are
            // silently ignored, so listing several candidates just widens compatibility.
            _scan = new InputAction("Scan", InputActionType.Button);
            _scan.AddBinding("<XRController>{RightHand}/trigger");
            _scan.AddBinding("<XRController>{LeftHand}/trigger");
            _scan.AddBinding("<XRController>{RightHand}/triggerPressed");
            _scan.AddBinding("<XRController>{LeftHand}/triggerPressed");
            _scan.AddBinding("<Keyboard>/space");

            _listen = new InputAction("Listen", InputActionType.Button);
            _listen.AddBinding("<XRController>{LeftHand}/grip");
            _listen.AddBinding("<XRController>{RightHand}/grip");
            _listen.AddBinding("<XRController>{LeftHand}/gripPressed");
            _listen.AddBinding("<XRController>{RightHand}/gripPressed");
            _listen.AddBinding("<Keyboard>/l");

            _scan.performed += OnScan;
            _listen.performed += OnListen;
        }

        private void OnEnable()
        {
            _scan?.Enable();
            _listen?.Enable();
        }

        private void OnDisable()
        {
            _scan?.Disable();
            _listen?.Disable();
        }

        private void OnDestroy()
        {
            if (_scan != null) { _scan.performed -= OnScan; _scan.Dispose(); }
            if (_listen != null) { _listen.performed -= OnListen; _listen.Dispose(); }
        }

        private void OnScan(InputAction.CallbackContext _)
        {
            if (bootstrap != null) bootstrap.Scan();
        }

        private void OnListen(InputAction.CallbackContext _)
        {
            if (bootstrap != null) bootstrap.ToggleListen();
        }
    }
}
