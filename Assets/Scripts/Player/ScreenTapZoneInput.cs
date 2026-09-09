using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

/// <summary>
/// Full-screen tap-zone controls: hold the left half of the screen to run left, the right
/// half to run right, and a quick tap on either half to jump. No visible UI.
///
/// This only reads pointer position + duration and forwards intent to a <see cref="TapPlayerInput"/>
/// through its existing Press/Release hooks — it does not touch TapPlayerInput or the
/// movement state machine. All of the zone/timing logic lives in <see cref="TapZoneInterpreter"/>
/// (unit-tested); this class is just the device adapter.
/// </summary>
public class ScreenTapZoneInput : MonoBehaviour, TapZoneInterpreter.ITarget
{
    private const int MousePointerId = -42;

    [SerializeField]
    [Tooltip("Receives the interpreted PressLeft/ReleaseLeft/... calls.")]
    private TapPlayerInput target;

    [SerializeField, Min(0f)]
    [Tooltip("A touch released faster than this (seconds) counts as a jump tap rather than a run hold.")]
    private float tapMaxDuration = 0.18f;

    [SerializeField, Range(0.1f, 0.9f)]
    [Tooltip("Fraction of the screen width that is the LEFT run zone; the remainder is the RIGHT zone.")]
    private float leftZoneFraction = 0.5f;

    private TapZoneInterpreter interpreter;

    private void Awake()
    {
        interpreter = new TapZoneInterpreter(this, tapMaxDuration, leftZoneFraction);
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        EnhancedTouchSupport.Enable();
#endif
    }

    private void OnDisable()
    {
        interpreter?.Reset();
#if ENABLE_INPUT_SYSTEM
        EnhancedTouchSupport.Disable();
#endif
    }

    private void Update()
    {
        if (interpreter == null || target == null)
        {
            return;
        }

        float time = Time.unscaledTime;
        float width = Screen.width;

#if ENABLE_INPUT_SYSTEM
        foreach (ETouch touch in ETouch.activeTouches)
        {
            switch (touch.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    interpreter.PointerDown(touch.touchId, touch.screenPosition.x, width, time);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Ended:
                    interpreter.PointerUp(touch.touchId, time);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    interpreter.PointerCancelled(touch.touchId);
                    break;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                interpreter.PointerDown(MousePointerId, mouse.position.ReadValue().x, width, time);
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                interpreter.PointerUp(MousePointerId, time);
            }
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == UnityEngine.TouchPhase.Began)
            {
                interpreter.PointerDown(touch.fingerId, touch.position.x, width, time);
            }
            else if (touch.phase == UnityEngine.TouchPhase.Ended)
            {
                interpreter.PointerUp(touch.fingerId, time);
            }
            else if (touch.phase == UnityEngine.TouchPhase.Canceled)
            {
                interpreter.PointerCancelled(touch.fingerId);
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            interpreter.PointerDown(MousePointerId, Input.mousePosition.x, width, time);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            interpreter.PointerUp(MousePointerId, time);
        }
#endif
    }

    void TapZoneInterpreter.ITarget.RunLeftPressed() => target.PressLeft();
    void TapZoneInterpreter.ITarget.RunLeftReleased() => target.ReleaseLeft();
    void TapZoneInterpreter.ITarget.RunRightPressed() => target.PressRight();
    void TapZoneInterpreter.ITarget.RunRightReleased() => target.ReleaseRight();
    void TapZoneInterpreter.ITarget.JumpTapped() => target.PressJump();
}
