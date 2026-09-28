using UnityEngine;

/// <summary>
/// The waffle monster's movement state machine (Grounded / Airborne / Climbing) and the
/// velocity it wants each physics step. Deliberately a plain class with no MonoBehaviour,
/// no input reads and no collider queries: the owner (<see cref="PlayerController"/>) feeds
/// it environment facts and input intent, so all of the transition logic is unit-testable
/// without a running scene.
///
/// Climbing:
/// - Entering contact with a climbable surface grabs the wall automatically (no input).
/// - Vertical movement is driven entirely by <c>climbInput</c> (a signed hold-to-move
///   value: +1 up, -1 down, 0 = hang in place). There is no auto-ascent and no residual
///   velocity — release the input and the player stops. Movement is strictly vertical.
/// - Pressing JUMP wall-jumps: the player kicks off into <see cref="PlayerMovementState.Airborne"/>
///   with a horizontal push away from the wall plus an upward impulse
///   (<see cref="IPlayerMovementConfig.WallJumpVelocity"/>).
/// - Holding the run direction AWAY from the wall (opposite <c>wallDirection</c>) instead
///   detaches with no impulse at all — an unassisted fall from whatever vertical velocity
///   climbing had at that moment, gravity taking over from there. This is separate from
///   wall-jump (which still needs the jump input and still gives its horizontal kick).
/// </summary>
public sealed class PlayerMovementController
{
    // Shared threshold: "is a run direction being held" (normalised input) and
    // "is the body moving away from the wall" (world units/sec). Small enough that either
    // reading is effectively "any real value".
    private const float Deadzone = 0.01f;

    private readonly IPlayerMovementConfig config;

    private bool isGrounded = true;
    private bool isTouchingClimbable;
    private float wallDirection;
    private bool jumpQueued;

    public PlayerMovementController(IPlayerMovementConfig config)
    {
        this.config = config;
        State = PlayerMovementState.Grounded;
    }

    public PlayerMovementState State { get; private set; }

    /// <summary>Gravity should be applied by the body in every state except climbing.</summary>
    public bool GravityActive => State != PlayerMovementState.Climbing;

    /// <summary>
    /// Latest environment facts from the owner, refreshed before each <see cref="Tick"/>.
    /// <paramref name="wallDirection"/> is +1 when the climbable surface is to the player's
    /// right, -1 when it is to the left, 0 when there is no contact.
    /// </summary>
    public void SetEnvironment(bool grounded, bool touchingClimbable, float wallDirection = 0f)
    {
        isGrounded = grounded;
        isTouchingClimbable = touchingClimbable;
        this.wallDirection = wallDirection;
    }

    /// <summary>Queues a single jump. Consumed on the next <see cref="Tick"/>; not buffered across steps.</summary>
    public void QueueJump() => jumpQueued = true;

    /// <summary>
    /// Advances the state machine one physics step and returns the velocity the body
    /// should have. <paramref name="currentVelocity"/> is the body's velocity going in
    /// (used to preserve vertical velocity under gravity while airborne, and horizontal
    /// velocity while kicking away from a wall). <paramref name="horizontalInput"/> drives
    /// run/steer on the ground and in the air; <paramref name="climbInput"/> (+1 up / -1
    /// down / 0 hang) drives vertical movement while climbing. Each is ignored in the
    /// state where it does not apply.
    /// </summary>
    public Vector2 Tick(Vector2 currentVelocity, float horizontalInput, float climbInput)
    {
        horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f);
        climbInput = Mathf.Clamp(climbInput, -1f, 1f);
        bool holdingDirection = Mathf.Abs(horizontalInput) > Deadzone;

        bool groundJump = jumpQueued
            && State == PlayerMovementState.Grounded
            && isGrounded
            && !isTouchingClimbable;
        bool wallJump = jumpQueued && State == PlayerMovementState.Climbing;
        jumpQueued = false;

        State = ResolveNextState(currentVelocity, horizontalInput, groundJump, wallJump);

        switch (State)
        {
            case PlayerMovementState.Climbing:
                // Strictly vertical, hold-to-move: no climb input -> hang still (no residual).
                return new Vector2(0f, climbInput * config.ClimbSpeed);

            case PlayerMovementState.Airborne:
                if (wallJump)
                {
                    // Kick off the wall: horizontal push away from it + an upward impulse.
                    return new Vector2(-wallDirection * config.WallJumpVelocity.x, config.WallJumpVelocity.y);
                }

                // Reachable while touching a wall only when kicking away from it (a fresh
                // wall-jump) — keep that horizontal push instead of zeroing it, until the
                // player either steers or leaves the wall.
                float airX = (isTouchingClimbable && !holdingDirection)
                    ? currentVelocity.x
                    : horizontalInput * config.RunSpeed;
                float airY = groundJump ? config.JumpVelocity : currentVelocity.y;
                return new Vector2(airX, airY);

            default: // Grounded
                return new Vector2(horizontalInput * config.RunSpeed, currentVelocity.y);
        }
    }

    private PlayerMovementState ResolveNextState(Vector2 currentVelocity, float horizontalInput, bool groundJump, bool wallJump)
    {
        if (wallJump)
        {
            return PlayerMovementState.Airborne;
        }

        if (isTouchingClimbable)
        {
            // Contact grabs (and holds) the wall automatically, except:
            // - right after a wall-jump, while airborne and still moving away from it
            //   (stops an instant re-grab), or
            // - while already climbing and holding a direction away from the wall side —
            //   an intentional, unassisted let-go (no jump impulse, just release).
            bool kickingAwayFromWall = State == PlayerMovementState.Airborne
                && currentVelocity.x * wallDirection < -Deadzone;
            bool lettingGoWhileClimbing = State == PlayerMovementState.Climbing
                && horizontalInput * wallDirection < -Deadzone;

            if (!kickingAwayFromWall && !lettingGoWhileClimbing)
            {
                return PlayerMovementState.Climbing;
            }
        }

        switch (State)
        {
            case PlayerMovementState.Climbing:
                // Left the surface, let go on purpose, or climbed off the top: hand control
                // back to run/jump.
                return isGrounded ? PlayerMovementState.Grounded : PlayerMovementState.Airborne;

            case PlayerMovementState.Grounded:
                return groundJump || !isGrounded
                    ? PlayerMovementState.Airborne
                    : PlayerMovementState.Grounded;

            case PlayerMovementState.Airborne:
                return isGrounded ? PlayerMovementState.Grounded : PlayerMovementState.Airborne;

            default:
                return State;
        }
    }
}
