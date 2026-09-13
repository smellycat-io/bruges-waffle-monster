/// <summary>
/// Abstraction over "what the player is asking the monster to do", decoupled from how
/// that intent is produced (tap zones, on-screen buttons, keyboard, a test stub, ...).
/// The movement logic only ever talks to this interface, so controls can be refined
/// later without touching <see cref="PlayerMovementController"/> or <see cref="PlayerController"/>.
/// </summary>
public interface IPlayerInputSource
{
    /// <summary>
    /// Desired run direction this frame: -1 (left), 0 (idle) or 1 (right). Held for as
    /// long as the player holds the control; not an edge/impulse. Used on the ground and
    /// in the air; ignored while climbing.
    /// </summary>
    float HorizontalAxis { get; }

    /// <summary>
    /// Desired climb direction this frame: +1 (up), 0 (hang) or -1 (down). Hold-to-move —
    /// releasing returns it to 0. Only consumed while climbing.
    /// </summary>
    float ClimbAxis { get; }

    /// <summary>
    /// Returns <c>true</c> exactly once per jump tap, then clears itself. Callers should
    /// poll this every frame and forward a <c>true</c> to the movement controller.
    /// </summary>
    bool ConsumeJumpRequest();
}
