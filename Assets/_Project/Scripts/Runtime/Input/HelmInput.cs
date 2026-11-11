using UnityEngine;
using UnityEngine.InputSystem;

namespace Offshore
{
    /// <summary>
    /// Keyboard and mouse controls for the boat and camera. Other components read the values; nothing here
    /// moves anything, so tests can bypass it and drive the boat directly.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class HelmInput : MonoBehaviour
    {
        InputAction throttle;
        InputAction steer;
        InputAction anchor;
        InputAction zoomScroll;
        InputAction zoomKeys;
        InputAction pause;
        InputAction fish;
        InputAction holdPanel;
        InputAction devEmptyHold;

        /// <summary>-1 (S) to 1 (W) while held.</summary>
        public float Throttle => GamePause.IsPaused ? 0f : throttle.ReadValue<float>();

        /// <summary>-1 (A, port) to 1 (D, starboard) while held.</summary>
        public float Steer => GamePause.IsPaused ? 0f : steer.ReadValue<float>();

        public bool AnchorPressed => !GamePause.IsPaused && anchor.WasPressedThisFrame();

        /// <summary>Mouse wheel notches this frame: positive = zoom in.</summary>
        public float ZoomSteps
        {
            get
            {
                float value = zoomScroll.ReadValue<float>();
                return Mathf.Abs(value) > 0.01f ? Mathf.Sign(value) : 0f;
            }
        }

        /// <summary>+/- keys held: positive = zoom in.</summary>
        public float ZoomHeld => zoomKeys.ReadValue<float>();

        public bool PausePressed => pause.WasPressedThisFrame();

        /// <summary>F: start or stop fishing. Ignored while paused.</summary>
        public bool FishTogglePressed => !GamePause.IsPaused && fish.WasPressedThisFrame();

        /// <summary>Tab: show or hide the hold contents.</summary>
        public bool HoldPanelPressed => holdPanel.WasPressedThisFrame();

        /// <summary>F9, DEVELOPMENT ONLY: empty the hold. Only honoured in the editor and development builds.</summary>
        public bool DevEmptyHoldPressed => devEmptyHold.WasPressedThisFrame();

        void Awake()
        {
            throttle = new InputAction("Throttle", InputActionType.Value);
            throttle.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/s").With("Positive", "<Keyboard>/w");
            throttle.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/downArrow").With("Positive", "<Keyboard>/upArrow");

            steer = new InputAction("Steer", InputActionType.Value);
            steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");

            anchor = new InputAction("Anchor", InputActionType.Button, "<Keyboard>/space");

            zoomScroll = new InputAction("Zoom", InputActionType.Value, "<Mouse>/scroll/y");
            zoomKeys = new InputAction("ZoomKeys", InputActionType.Value);
            zoomKeys.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/minus").With("Positive", "<Keyboard>/equals");
            zoomKeys.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/numpadMinus").With("Positive", "<Keyboard>/numpadPlus");

            pause = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            pause.AddBinding("<Keyboard>/p");

            fish = new InputAction("Fish", InputActionType.Button, "<Keyboard>/f");
            holdPanel = new InputAction("Hold", InputActionType.Button, "<Keyboard>/tab");
            devEmptyHold = new InputAction("DevEmptyHold", InputActionType.Button, "<Keyboard>/f9");
        }

        InputAction[] All => new[] { throttle, steer, anchor, zoomScroll, zoomKeys, pause, fish, holdPanel, devEmptyHold };

        void OnEnable()
        {
            foreach (var action in All) action.Enable();
        }

        void OnDisable()
        {
            foreach (var action in All) action.Disable();
        }

        void OnDestroy()
        {
            foreach (var action in All) action.Dispose();
        }
    }
}
