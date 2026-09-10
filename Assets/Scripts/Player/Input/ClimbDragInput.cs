using UnityEngine;

/// <summary>
/// Drag-to-climb driver: while the player is on a wall, touch anywhere and drag up/down to
/// move up/down it; release to stop; no touch = hang. It just pushes a climb axis onto
/// <see cref="TapPlayerInput"/> every frame — the movement state machine only acts on that
/// axis while in the Climbing state, so this component needs no knowledge of game state and
/// runs alongside <see cref="ScreenTapZoneInput"/> without conflicting (run/jump come from
/// there, the climb axis from here).
///
/// The drag model lives in the unit-tested <see cref="ClimbDragInterpreter"/>; this class
/// is only the device adapter (via <see cref="PointerSampler"/>).
/// </summary>
public class ClimbDragInput : MonoBehaviour, PointerSampler.IHandler
{
    [SerializeField]
    [Tooltip("Receives the climb axis via SetClimbAxis.")]
    private TapPlayerInput target;

    [SerializeField, Min(0f)]
    [Tooltip("Screen-pixel drag distance from the touch-down point before climbing starts.")]
    private float dragDeadzonePixels = 14f;

    private readonly PointerSampler sampler = new PointerSampler();
    private ClimbDragInterpreter interpreter;

    private void Awake()
    {
        interpreter = new ClimbDragInterpreter(dragDeadzonePixels);
    }

    private void OnEnable() => sampler.Enable();

    private void OnDisable()
    {
        interpreter?.Reset();
        if (target != null)
        {
            target.SetClimbAxis(0f);
        }
        sampler.Disable();
    }

    private void Update()
    {
        if (interpreter == null || target == null)
        {
            return;
        }

        sampler.Sample(this);
        target.SetClimbAxis(interpreter.ClimbAxis);
    }

    void PointerSampler.IHandler.PointerDown(int pointerId, Vector2 screenPosition)
        => interpreter.PointerDown(pointerId, screenPosition.y);

    void PointerSampler.IHandler.PointerMove(int pointerId, Vector2 screenPosition)
        => interpreter.PointerMoved(pointerId, screenPosition.y);

    void PointerSampler.IHandler.PointerUp(int pointerId, bool cancelled)
        => interpreter.PointerUp(pointerId);
}
