using UnityEngine;

/// <summary>
/// Unity-facing glue for the Chef's schedule: owns a <see cref="ChefScheduleController"/>
/// (the actual, unit-testable state machine) built from the assigned <see cref="ChefScheduleData"/>,
/// and ticks it every frame. Deliberately NOT a singleton — CLAUDE.md reserves that pattern for
/// <c>GameStateManager</c> — other components find it via <c>FindAnyObjectByType</c>, same as
/// <c>CitizenAI</c> finds <c>PlayerController</c>.
///
/// This runs independently of <c>GameStateManager</c>'s current state: the schedule keeps
/// ticking in Runner, Kitchen-Approach, and Arena alike (see <c>ArenaState</c>, which extends
/// rather than pauses the countdown on entry — pausing would make that extension meaningless).
///
/// <see cref="Schedule"/> is guaranteed non-null once <see cref="EnsureInitialized"/> has run
/// (falling back to <see cref="ChefScheduleConfig.Fallback"/> if no <see cref="ChefScheduleData"/>
/// is assigned) — so warning/encounter components never need their own null-check, just their
/// own <see cref="EnsureInitialized"/> call before touching it.
/// </summary>
public class ChefController : MonoBehaviour
{
    [SerializeField] private ChefScheduleData scheduleData;

    private bool initialized;

    /// <summary>The underlying schedule state machine. Null until <see cref="EnsureInitialized"/> has run — always non-null after.</summary>
    public ChefScheduleController Schedule { get; private set; }

    /// <summary>Exposed so warning components (e.g. the blink curve) can read the same tunable data without a second reference. Can be null (see <see cref="Schedule"/>'s fallback).</summary>
    public ChefScheduleData ScheduleData => scheduleData;

    public ChefPresenceState CurrentState { get { EnsureInitialized(); return Schedule.CurrentState; } }
    public bool IsPresent => CurrentState == ChefPresenceState.Present;
    public bool IsRealReturnCountdownActive { get { EnsureInitialized(); return Schedule.IsRealReturnCountdownActive; } }
    public float RealReturnCountdownRemaining { get { EnsureInitialized(); return Schedule.RealReturnCountdownRemaining; } }
    public float RealReturnCountdownDuration { get { EnsureInitialized(); return Schedule.RealReturnCountdownDuration; } }
    public float BlinkRampProgress01 { get { EnsureInitialized(); return Schedule.BlinkRampProgress01; } }

    private void Awake() => EnsureInitialized();

    /// <summary>
    /// Builds <see cref="Schedule"/> from <see cref="scheduleData"/> (or a harmless fallback if
    /// unassigned). Idempotent — only the first call does anything. Called from Awake, and
    /// defensively from every other public member for the same EditMode-test AddComponent
    /// timing reason as PlayerStrikeSystem.EnsureInitialized.
    /// </summary>
    public void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }
        initialized = true;

        if (scheduleData == null)
        {
            Debug.LogError($"{name}: no {nameof(ChefScheduleData)} assigned — the Chef will use a harmless fallback schedule.", this);
        }

        ChefScheduleConfig config = scheduleData != null ? scheduleData.BuildConfig() : ChefScheduleConfig.Fallback();
        Schedule = new ChefScheduleController(config);
    }

    private void Update() => Tick(Time.deltaTime);

    /// <summary>Advances the schedule by <paramref name="deltaTime"/>. Exposed publicly so tests can drive it with synthetic time.</summary>
    public void Tick(float deltaTime)
    {
        EnsureInitialized();
        Schedule.Tick(deltaTime);
    }

    /// <summary>See <see cref="ChefScheduleController.ExtendReturnCountdown"/> — called by ArenaState on entry.</summary>
    public void ExtendReturnCountdown(float bufferSeconds)
    {
        EnsureInitialized();
        Schedule.ExtendReturnCountdown(bufferSeconds);
    }
}
