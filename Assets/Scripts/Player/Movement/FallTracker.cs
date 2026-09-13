using UnityEngine;

/// <summary>
/// Tracks how far the player free-falls, reporting the total fall distance the instant a
/// fall ends by landing. Plain class (no MonoBehaviour, no physics reads beyond the Y value
/// and state it's given each tick) so the peak-tracking logic is unit-testable;
/// <see cref="PlayerController"/> feeds it its Y position and <see cref="PlayerMovementState"/>
/// every FixedUpdate and forwards a completed fall to <c>PlayerStrikeSystem.RegisterFall</c>.
///
/// Only <see cref="PlayerMovementState.Airborne"/> counts as falling:
/// - Grounded, or Climbing (including climbing back down under full control), never
///   accumulates fall distance — deliberately climbing down a tall wall and stepping off at
///   the bottom is not a fall.
/// - Grabbing a <c>ClimbableSurface</c> mid-fall (Airborne -&gt; Climbing) breaks the fall
///   without penalty, like a catch — it only resumes counting (from that height) if the
///   player later lets go or wall-jumps back into Airborne.
/// - Only an Airborne -&gt; Grounded transition is a "landing" that reports a fall distance.
/// </summary>
public sealed class FallTracker
{
    private float peakHeight;
    private PlayerMovementState previousState = PlayerMovementState.Grounded;

    /// <summary>
    /// Advances the tracker one tick. Returns the completed fall distance (always &gt;= 0)
    /// the instant a free-fall ends by landing; <c>null</c> on every other tick.
    /// </summary>
    public float? Tick(float currentY, PlayerMovementState state)
    {
        float? completedFallDistance = null;

        if (state == PlayerMovementState.Airborne)
        {
            // (Re)start tracking from here if we just became airborne (left the ground, or
            // let go of / fell off a climb); otherwise extend the peak upward as we rise.
            peakHeight = previousState == PlayerMovementState.Airborne
                ? Mathf.Max(peakHeight, currentY)
                : currentY;
        }
        else if (state == PlayerMovementState.Grounded && previousState == PlayerMovementState.Airborne)
        {
            completedFallDistance = Mathf.Max(0f, peakHeight - currentY);
        }
        // Climbing, or staying Grounded: no fall bookkeeping at all.

        previousState = state;
        return completedFallDistance;
    }
}
