using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// On-screen control that reports press-and-hold rather than a completed click, so it can
/// drive "hold to run" input. <see cref="OnPress"/> fires on pointer-down; <see cref="OnRelease"/>
/// fires on pointer-up or when the pointer leaves the button (or the button is disabled),
/// so the run direction can't get stuck on.
///
/// Deliberately generic — small buttons, full-screen tap zones, or future on-screen
/// controls can all reuse it.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public UnityEvent OnPress;
    public UnityEvent OnRelease;

    private bool held;

    public void OnPointerDown(PointerEventData eventData) => SetHeld(true);
    public void OnPointerUp(PointerEventData eventData) => SetHeld(false);
    public void OnPointerExit(PointerEventData eventData) => SetHeld(false);

    private void OnDisable() => SetHeld(false);

    private void SetHeld(bool value)
    {
        if (held == value)
        {
            return;
        }

        held = value;
        (value ? OnPress : OnRelease)?.Invoke();
    }
}
