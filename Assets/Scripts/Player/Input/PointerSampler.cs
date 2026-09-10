using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

/// <summary>
/// One place that reads active touches + the mouse each frame and dispatches
/// down / move / up events, so the input-backend <c>#if</c> maze lives here instead of in
/// every driver. <see cref="ScreenTapZoneInput"/> and <see cref="ClimbDragInput"/> both
/// consume it. Call <see cref="Enable"/>/<see cref="Disable"/> from the owner's
/// OnEnable/OnDisable and <see cref="Sample"/> from its Update.
/// </summary>
public sealed class PointerSampler
{
    /// <summary>Synthetic pointer id used for the mouse (real touch ids are non-negative).</summary>
    public const int MousePointerId = -1;

    public interface IHandler
    {
        void PointerDown(int pointerId, Vector2 screenPosition);
        void PointerMove(int pointerId, Vector2 screenPosition);
        void PointerUp(int pointerId, bool cancelled);
    }

    public void Enable()
    {
#if ENABLE_INPUT_SYSTEM
        EnhancedTouchSupport.Enable();
#endif
    }

    public void Disable()
    {
#if ENABLE_INPUT_SYSTEM
        EnhancedTouchSupport.Disable();
#endif
    }

    public void Sample(IHandler handler)
    {
#if ENABLE_INPUT_SYSTEM
        foreach (ETouch touch in ETouch.activeTouches)
        {
            switch (touch.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    handler.PointerDown(touch.touchId, touch.screenPosition);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Moved:
                case UnityEngine.InputSystem.TouchPhase.Stationary:
                    handler.PointerMove(touch.touchId, touch.screenPosition);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Ended:
                    handler.PointerUp(touch.touchId, cancelled: false);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    handler.PointerUp(touch.touchId, cancelled: true);
                    break;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                handler.PointerDown(MousePointerId, mouse.position.ReadValue());
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                handler.PointerUp(MousePointerId, cancelled: false);
            }
            else if (mouse.leftButton.isPressed)
            {
                handler.PointerMove(MousePointerId, mouse.position.ReadValue());
            }
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            switch (touch.phase)
            {
                case UnityEngine.TouchPhase.Began:
                    handler.PointerDown(touch.fingerId, touch.position);
                    break;
                case UnityEngine.TouchPhase.Moved:
                case UnityEngine.TouchPhase.Stationary:
                    handler.PointerMove(touch.fingerId, touch.position);
                    break;
                case UnityEngine.TouchPhase.Ended:
                    handler.PointerUp(touch.fingerId, cancelled: false);
                    break;
                case UnityEngine.TouchPhase.Canceled:
                    handler.PointerUp(touch.fingerId, cancelled: true);
                    break;
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            handler.PointerDown(MousePointerId, Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            handler.PointerUp(MousePointerId, cancelled: false);
        }
        else if (Input.GetMouseButton(0))
        {
            handler.PointerMove(MousePointerId, Input.mousePosition);
        }
#endif
    }
}
