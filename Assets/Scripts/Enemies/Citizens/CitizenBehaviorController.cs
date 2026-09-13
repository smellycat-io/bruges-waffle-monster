/// <summary>
/// The citizen behavior state machine (Idle / Chasing / Disengaged) — a plain class with no
/// MonoBehaviour, no raycasts and no movement: the owner (<see cref="CitizenAI"/>) feeds it
/// sensor facts each tick, so the transition logic is unit-testable without a running scene.
/// Mirrors the split already used for the player (<c>PlayerMovementController</c> /
/// <c>PlayerController</c>).
///
/// - Line-of-sight on the player -> automatic chase, no other trigger needed.
/// - Losing line-of-sight -> immediately back to Idle (no lingering pursuit / last-known-position
///   memory — losing sight stops the chase outright, per design).
/// - Wallet drained -> Disengaged. This is a one-way latch: once Disengaged, nothing (not even
///   regaining line-of-sight) brings the citizen back into the chase for the rest of the level.
/// </summary>
public sealed class CitizenBehaviorController
{
    public CitizenChaseState State { get; private set; } = CitizenChaseState.Idle;

    /// <summary>
    /// Advances the state machine one tick from fresh sensor facts. Call every frame/physics
    /// step with the citizen's current line-of-sight and wallet-drained readings.
    /// </summary>
    public void Tick(bool hasLineOfSightOnPlayer, bool isWalletDrained)
    {
        if (State == CitizenChaseState.Disengaged)
        {
            return; // permanent for the rest of the level
        }

        if (isWalletDrained)
        {
            State = CitizenChaseState.Disengaged;
            return;
        }

        State = hasLineOfSightOnPlayer ? CitizenChaseState.Chasing : CitizenChaseState.Idle;
    }
}
