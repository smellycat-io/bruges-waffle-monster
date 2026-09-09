/// <summary>
/// Turns a single touch/drag into a climb axis (+1 up / 0 hang / -1 down), independent of
/// the input backend. Plain class (no MonoBehaviour, no device reads) so the drag model is
/// unit-testable; <see cref="ClimbDragInput"/> is the thin adapter that feeds it real touches.
///
/// Model: the first touch sets an anchor. The axis is the sign of (current Y − anchor Y)
/// once it passes a deadzone — so you can hold a finger above the anchor and keep
/// ascending, drag back below it to descend, and return near the anchor to hang. Releasing
/// the touch drops the axis straight to 0 (hold-to-move, no momentum). Extra fingers while
/// one is active are ignored. Deliberately shares nothing with
/// <see cref="TapZoneInterpreter"/> — different interaction model.
/// </summary>
public sealed class ClimbDragInterpreter
{
    private readonly float deadzone;

    private bool active;
    private int activePointerId;
    private float anchorY;

    /// <param name="deadzone">Drag distance from the anchor (same units as the Y values passed in) before the axis leaves 0.</param>
    public ClimbDragInterpreter(float deadzone)
    {
        this.deadzone = deadzone;
    }

    /// <summary>+1 up, -1 down, 0 hang / no touch.</summary>
    public float ClimbAxis { get; private set; }

    public void PointerDown(int pointerId, float screenY)
    {
        if (active)
        {
            return; // first touch owns the climb until it lifts
        }

        active = true;
        activePointerId = pointerId;
        anchorY = screenY;
        ClimbAxis = 0f;
    }

    public void PointerMoved(int pointerId, float screenY)
    {
        if (!active || pointerId != activePointerId)
        {
            return;
        }

        float delta = screenY - anchorY;
        ClimbAxis = delta > deadzone ? 1f : delta < -deadzone ? -1f : 0f;
    }

    public void PointerUp(int pointerId)
    {
        if (!active || pointerId != activePointerId)
        {
            return;
        }

        active = false;
        ClimbAxis = 0f;
    }

    /// <summary>Drops the touch and the axis (e.g. component disabled).</summary>
    public void Reset()
    {
        active = false;
        ClimbAxis = 0f;
    }
}
