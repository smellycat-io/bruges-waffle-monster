using UnityEngine;

/// <summary>
/// Full-screen tap-zone controls: hold the left half of the screen to run left, the right
/// half to run right, and a quick tap on either half to jump. No visible UI.
///
/// This only reads pointer position + duration (via <see cref="PointerSampler"/>) and
/// forwards intent to a <see cref="TapPlayerInput"/> through its existing Press/Release
/// hooks — it does not touch TapPlayerInput or the movement state machine. The zone/timing
/// logic lives in the unit-tested <see cref="TapZoneInterpreter"/>; this class is just the
/// device adapter.
/// </summary>
public class ScreenTapZoneInput : MonoBehaviour, TapZoneInterpreter.ITarget, PointerSampler.IHandler
{
    [SerializeField]
    [Tooltip("Receives the interpreted PressLeft/ReleaseLeft/... calls.")]
    private TapPlayerInput target;

    [SerializeField, Min(0f)]
    [Tooltip("A touch released faster than this (seconds) counts as a jump tap rather than a run hold.")]
    private float tapMaxDuration = 0.18f;

    [SerializeField, Range(0.1f, 0.9f)]
    [Tooltip("Fraction of the screen width that is the LEFT run zone; the remainder is the RIGHT zone.")]
    private float leftZoneFraction = 0.5f;

    private readonly PointerSampler sampler = new PointerSampler();
    private TapZoneInterpreter interpreter;
    private float frameTime;

    private void Awake()
    {
        interpreter = new TapZoneInterpreter(this, tapMaxDuration, leftZoneFraction);
    }

    private void OnEnable() => sampler.Enable();

    private void OnDisable()
    {
        interpreter?.Reset();
        sampler.Disable();
    }

    private void Update()
    {
        if (interpreter == null || target == null)
        {
            return;
        }

        frameTime = Time.unscaledTime;
        sampler.Sample(this);
        // Promote any press held past the tap threshold into a run hold.
        interpreter.Tick(frameTime);
    }

    void PointerSampler.IHandler.PointerDown(int pointerId, Vector2 screenPosition)
        => interpreter.PointerDown(pointerId, screenPosition.x, Screen.width, frameTime);

    void PointerSampler.IHandler.PointerMove(int pointerId, Vector2 screenPosition) { /* zones only care about down/up */ }

    void PointerSampler.IHandler.PointerUp(int pointerId, bool cancelled)
    {
        if (cancelled)
        {
            interpreter.PointerCancelled(pointerId);
        }
        else
        {
            interpreter.PointerUp(pointerId, frameTime);
        }
    }

    void TapZoneInterpreter.ITarget.RunLeftPressed() => target.PressLeft();
    void TapZoneInterpreter.ITarget.RunLeftReleased() => target.ReleaseLeft();
    void TapZoneInterpreter.ITarget.RunRightPressed() => target.PressRight();
    void TapZoneInterpreter.ITarget.RunRightReleased() => target.ReleaseRight();
    void TapZoneInterpreter.ITarget.JumpTapped() => target.PressJump();
}
