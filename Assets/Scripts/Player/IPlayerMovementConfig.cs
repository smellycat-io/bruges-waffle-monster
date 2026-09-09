using UnityEngine;

/// <summary>
/// Tunable movement numbers consumed by <see cref="PlayerMovementController"/>. Backed at
/// runtime by the <see cref="PlayerMovementConfig"/> ScriptableObject; the interface exists
/// so the movement logic can be exercised in EditMode tests without creating an asset.
///
/// Every value here is gameplay-balance data and lives in the ScriptableObject per the
/// project's "no magic numbers" rule — never hardcode these in movement code.
/// </summary>
public interface IPlayerMovementConfig
{
    /// <summary>Horizontal run speed in units/second at full stick.</summary>
    float RunSpeed { get; }

    /// <summary>Instant vertical take-off velocity applied on a jump, in units/second.</summary>
    float JumpVelocity { get; }

    /// <summary>Constant vertical speed while climbing (up, or down while a direction is held), in units/second.</summary>
    float ClimbSpeed { get; }

    /// <summary>
    /// Wall-jump kick-off velocity: <c>x</c> = horizontal push away from the wall,
    /// <c>y</c> = upward impulse. Applied when JUMP is pressed while climbing.
    /// </summary>
    Vector2 WallJumpVelocity { get; }

    /// <summary>Rigidbody2D gravity scale applied while not climbing.</summary>
    float GravityScale { get; }
}
