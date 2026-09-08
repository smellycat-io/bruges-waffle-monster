using UnityEngine;

/// <summary>
/// The waffle monster's movement state machine (Grounded / Airborne / Climbing) and the
/// velocity it wants each physics step. Deliberately a plain class with no MonoBehaviour,
/// no input reads and no collider queries: the owner (<see cref="PlayerController"/>) feeds
/// it environment facts and input intent, so all of the transition logic is unit-testable
/// without a running scene.
///
/// Climbing is fully automatic — while the owner reports contact with a climbable surface
/// this stays in <see cref="PlayerMovementState.Climbing"/> and drives the body straight up
/// at <see cref="IPlayerMovementConfig.ClimbSpeed"/>; no jump or directional input is needed
/// to start, continue or (by leaving contact) end the climb.
/// </summary>
public sealed class PlayerMovementController
{
    private readonly IPlayerMovementConfig config;

    private bool isGrounded = true;
    private bool isTouchingClimbable;
    private bool jumpQueued;

    public PlayerMovementController(IPlayerMovementConfig config)
    {
        this.config = config;
        State = PlayerMovementState.Grounded;
    }

    public PlayerMovementState State { get; private set; }

    /// <summary>Gravity should be applied by the body in every state except climbing.</summary>
    public bool GravityActive => State != PlayerMovementState.Climbing;

    /// <summary>Latest environment facts from the owner, refreshed before each <see cref="Tick"/>.</summary>
    public void SetEnvironment(bool grounded, bool touchingClimbable)
    {
        isGrounded = grounded;
        isTouchingClimbable = touchingClimbable;
    }

    /// <summary>Queues a single jump. Consumed on the next <see cref="Tick"/>; not buffered across steps.</summary>
    public void QueueJump() => jumpQueued = true;

    /// <summary>
    /// Advances the state machine one physics step and returns the velocity the body
    /// should have. <paramref name="currentVelocity"/> is the body's velocity going in
    /// (used to preserve vertical velocity under gravity while airborne).
    /// </summary>
    public Vector2 Tick(Vector2 currentVelocity, float horizontalInput)
    {
        horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f);

        // A jump only takes effect from a grounded stance that isn't being overridden by a climb.
        bool jumpingThisStep = jumpQueued
            && State == PlayerMovementState.Grounded
            && isGrounded
            && !isTouchingClimbable;
        jumpQueued = false;

        State = ResolveNextState(jumpingThisStep);

        switch (State)
        {
            case PlayerMovementState.Climbing:
                // Vertical only — no sideways drift or dismount while climbing (see design doc).
                return new Vector2(0f, config.ClimbSpeed);

            case PlayerMovementState.Airborne:
                float verticalVelocity = jumpingThisStep ? config.JumpVelocity : currentVelocity.y;
                return new Vector2(horizontalInput * config.RunSpeed, verticalVelocity);

            default: // Grounded
                return new Vector2(horizontalInput * config.RunSpeed, currentVelocity.y);
        }
    }

    private PlayerMovementState ResolveNextState(bool jumpingThisStep)
    {
        // Contact with a climbable surface always wins, from any state, with no input.
        if (isTouchingClimbable)
        {
            return PlayerMovementState.Climbing;
        }

        switch (State)
        {
            case PlayerMovementState.Climbing:
                // Left the surface or climbed off the top: hand control back to run/jump.
                return isGrounded ? PlayerMovementState.Grounded : PlayerMovementState.Airborne;

            case PlayerMovementState.Grounded:
                return jumpingThisStep || !isGrounded
                    ? PlayerMovementState.Airborne
                    : PlayerMovementState.Grounded;

            case PlayerMovementState.Airborne:
                return isGrounded ? PlayerMovementState.Grounded : PlayerMovementState.Airborne;

            default:
                return State;
        }
    }
}
