using UnityEngine;

/// <summary>
/// Picks and times random wander points for a citizen's Idle state — a plain class (no
/// MonoBehaviour, no <c>Time.x</c> reads) mirroring the split already used elsewhere in this
/// project (<c>PlayerMovementController</c>/<c>PlayerController</c>,
/// <c>CitizenBehaviorController</c>/<c>CitizenAI</c>): <see cref="CitizenAI"/> feeds it the
/// current position and elapsed time each <c>FixedUpdate</c> while Idle, and reads back
/// <see cref="CurrentTargetX"/> to actually move toward (via <c>Rigidbody2D.MovePosition</c>)
/// the same way it already does for chase/climb.
///
/// Wanders within <c>wanderRadius</c> of a fixed spawn point on the horizontal axis only —
/// citizens are grounded, gravity-less kinematic bodies (see <c>CitizenAI</c>), so a vertical
/// wander offset would leave one floating in place with nothing to pull it back down. This
/// mirrors chase movement's existing "grounded and horizontal-only" design choice.
///
/// Pauses <c>wanderPauseDuration</c> seconds on arrival before picking a new point.
/// Deactivating (leaving Idle) and reactivating (back to Idle) discards any in-progress
/// target/pause and immediately picks a fresh point from wherever the citizen currently is —
/// per design, it does not need to return to spawn first, just stay within radius of it.
/// </summary>
public sealed class CitizenWanderController
{
    // Not a gameplay-tunable value — this is float-precision slop for "close enough to say
    // CitizenAI's clamped-step movement (see WanderTowardTarget) has arrived", not a balance
    // number a designer would want exposed.
    private const float ArrivalDistance = 0.01f;

    private readonly float spawnX;
    private readonly float wanderRadius;
    private readonly float pauseDuration;

    private bool wasActive;
    private float pauseTimer;

    /// <summary>The x position currently being walked toward, or null while paused/inactive (CitizenAI should not move in that case).</summary>
    public float? CurrentTargetX { get; private set; }

    public CitizenWanderController(Vector2 spawnPosition, float wanderRadius, float pauseDuration)
    {
        spawnX = spawnPosition.x;
        this.wanderRadius = wanderRadius;
        this.pauseDuration = pauseDuration;
    }

    /// <summary>
    /// Advances wandering by one tick. Call every <c>FixedUpdate</c> regardless of state —
    /// pass <paramref name="isActive"/> as true only while the citizen is Idle, so the
    /// active→inactive and inactive→active edges (chase starting/ending) are caught even on
    /// the frame they happen.
    /// </summary>
    public void Tick(bool isActive, float currentX, float deltaTime)
    {
        if (!isActive)
        {
            wasActive = false;
            CurrentTargetX = null;
            return;
        }

        if (!wasActive)
        {
            // Just became (or started) Idle — begin wandering right away from wherever the
            // citizen is now, ignoring any target/pause left over from before.
            wasActive = true;
            pauseTimer = 0f;
            CurrentTargetX = PickNewTargetX();
            return;
        }

        if (pauseTimer > 0f)
        {
            pauseTimer -= deltaTime;
            if (pauseTimer > 0f)
            {
                return;
            }
        }

        if (!CurrentTargetX.HasValue)
        {
            CurrentTargetX = PickNewTargetX();
            return;
        }

        if (Mathf.Abs(currentX - CurrentTargetX.Value) <= ArrivalDistance)
        {
            CurrentTargetX = null;
            pauseTimer = pauseDuration;
        }
    }

    private float PickNewTargetX() => spawnX + Random.Range(-wanderRadius, wanderRadius);
}
