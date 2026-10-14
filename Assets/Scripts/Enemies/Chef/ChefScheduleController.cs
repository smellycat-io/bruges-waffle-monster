using System;
using UnityEngine;

/// <summary>
/// The Chef's away/present/bedtime schedule and its return-countdown/fake-out timing — a
/// plain class with no MonoBehaviour, fed explicit delta-time each tick, so the whole
/// schedule is unit-testable without a running scene (mirrors <c>CitizenBehaviorController</c>).
/// <see cref="ChefController"/> owns one of these and is the only thing that ticks it.
///
/// Design decisions made while implementing (flagged, not assumed silently — see chat/PR
/// description for the full list this session):
/// - A real return countdown is always exactly <see cref="ChefScheduleConfig.ReturnWarningLeadTime"/>
///   seconds (barring <see cref="ExtendReturnCountdown"/> calls), whether it started because the
///   scheduled away duration ran down to that much time left, or because of a random early
///   trigger — either way the player gets the same warning window.
/// - Bedtime is a one-time, elapsed-level-time threshold. It can preempt a long, still-safe
///   stretch of an Away period immediately, but never interrupts an already-active real return
///   countdown (that one always finishes normally — Present, then departs — before bedtime is
///   applied to the away period that follows). After bedtimeDuration elapses, normal cycling
///   resumes (per product decision — declined the alternative of staying safe permanently).
/// - A fake-out never starts during an active real return countdown, Present, or Bedtime —
///   only while safely Away and not already counting down.
/// </summary>
public sealed class ChefScheduleController
{
    /// <summary>Used only if <see cref="ChefScheduleConfig.KnownAwayDurationRanges"/> is empty — flag if this is ever actually hit.</summary>
    private const float FallbackAwayDuration = 10f;

    private readonly ChefScheduleConfig config;
    private readonly Func<float> randomSource;

    private int awayRangeIndex;
    private float awayPhaseElapsed;
    private float targetAwayDuration;
    private float presentElapsed;
    private float bedtimeRemaining;
    private float elapsedLevelTime;
    private bool bedtimeHasHappened;
    private bool realReturnMidpointFired;

    private bool fakeOutActive;
    private float fakeOutElapsed;
    private float fakeOutPeak;

    public ChefPresenceState CurrentState { get; private set; }
    public bool IsRealReturnCountdownActive { get; private set; }
    public float RealReturnCountdownRemaining { get; private set; }
    public float RealReturnCountdownDuration { get; private set; }

    /// <summary>
    /// 0..1 blink-urgency signal for the Security Monitor. Identical shape whether the current
    /// ramp is a real return countdown or a fake-out — that indistinguishability is the point
    /// (see SecurityMonitorWarning). 0 whenever neither is active.
    /// </summary>
    public float BlinkRampProgress01
    {
        get
        {
            if (IsRealReturnCountdownActive && RealReturnCountdownDuration > 0f)
            {
                return Mathf.Clamp01(1f - RealReturnCountdownRemaining / RealReturnCountdownDuration);
            }

            if (fakeOutActive && config.FakeOutRampDuration > 0f)
            {
                return Mathf.Clamp01(fakeOutElapsed / config.FakeOutRampDuration) * fakeOutPeak;
            }

            return 0f;
        }
    }

    /// <summary>Fired exactly once per real return countdown, at its midpoint. Never fires for a fake-out — the Silhouette's only signal.</summary>
    public event Action RealReturnMidpointReached;

    /// <summary>Fired the instant the Chef becomes Present (a real return countdown completing).</summary>
    public event Action ChefReturned;

    /// <summary>Fired when the Chef leaves Present and a fresh Away (or Bedtime) period begins.</summary>
    public event Action ChefDeparted;

    /// <summary>Fired once, when the schedule enters the extended bedtime safe window.</summary>
    public event Action BedtimeStarted;

    public ChefScheduleController(ChefScheduleConfig config, Func<float> randomSource = null)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.randomSource = randomSource ?? (() => UnityEngine.Random.value);

        BeginAwayPeriod();
    }

    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        elapsedLevelTime += deltaTime;

        switch (CurrentState)
        {
            case ChefPresenceState.Away:
                TickAway(deltaTime);
                break;
            case ChefPresenceState.Present:
                TickPresent(deltaTime);
                break;
            case ChefPresenceState.Bedtime:
                TickBedtime(deltaTime);
                break;
        }
    }

    /// <summary>
    /// Pushes back whatever the next real return currently is by <paramref name="bufferSeconds"/>
    /// — used for the Arena-entry breathing-room buffer. Never pauses/removes the threat
    /// outright, per design: it just buys more time before it can happen.
    /// </summary>
    public void ExtendReturnCountdown(float bufferSeconds)
    {
        if (bufferSeconds <= 0f)
        {
            return;
        }

        if (IsRealReturnCountdownActive)
        {
            RealReturnCountdownRemaining += bufferSeconds;
            RealReturnCountdownDuration += bufferSeconds;
            return;
        }

        switch (CurrentState)
        {
            case ChefPresenceState.Away:
                targetAwayDuration += bufferSeconds;
                break;
            case ChefPresenceState.Bedtime:
                bedtimeRemaining += bufferSeconds;
                break;
            // Present: nothing to extend. Arena entry is only supposed to happen while Away
            // (see KitchenDoorTrigger), so this case should be unreachable in practice.
        }
    }

    private void TickAway(float deltaTime)
    {
        if (IsRealReturnCountdownActive)
        {
            TickRealReturnCountdown(deltaTime);
            return;
        }

        // Bedtime can preempt a still-safe stretch of Away immediately, but (by construction)
        // never an active countdown — that branch already returned above.
        if (!bedtimeHasHappened && elapsedLevelTime >= config.BedtimeStartTime)
        {
            EnterBedtime();
            return;
        }

        awayPhaseElapsed += deltaTime;
        TickFakeOut(deltaTime);

        float earlyReturnProbability = PerTickProbability(config.RandomEarlyReturnProbabilityPerSecond, deltaTime);
        if (earlyReturnProbability > 0f && randomSource() < earlyReturnProbability)
        {
            StartRealReturnCountdown(config.ReturnWarningLeadTime);
            return;
        }

        float warningStart = targetAwayDuration - config.ReturnWarningLeadTime;
        if (awayPhaseElapsed >= warningStart)
        {
            float remaining = Mathf.Max(0.0001f, targetAwayDuration - awayPhaseElapsed);
            StartRealReturnCountdown(remaining);
        }
    }

    private void TickRealReturnCountdown(float deltaTime)
    {
        RealReturnCountdownRemaining -= deltaTime;

        if (!realReturnMidpointFired && RealReturnCountdownRemaining <= RealReturnCountdownDuration / 2f)
        {
            realReturnMidpointFired = true;
            RealReturnMidpointReached?.Invoke();
        }

        if (RealReturnCountdownRemaining <= 0f)
        {
            IsRealReturnCountdownActive = false;
            CurrentState = ChefPresenceState.Present;
            presentElapsed = 0f;
            ChefReturned?.Invoke();
        }
    }

    private void TickPresent(float deltaTime)
    {
        presentElapsed += deltaTime;
        if (presentElapsed >= config.PresentDuration)
        {
            ChefDeparted?.Invoke();
            BeginAwayPeriod();
        }
    }

    private void TickBedtime(float deltaTime)
    {
        bedtimeRemaining -= deltaTime;
        if (bedtimeRemaining <= 0f)
        {
            BeginAwayPeriod(); // bedtimeHasHappened is already true, so this resumes normal cycling
        }
    }

    /// <summary>Starts a fresh safe period — either a normal Away period, or Bedtime if the threshold has just been crossed and hasn't fired yet.</summary>
    private void BeginAwayPeriod()
    {
        if (!bedtimeHasHappened && elapsedLevelTime >= config.BedtimeStartTime)
        {
            EnterBedtime();
            return;
        }

        CurrentState = ChefPresenceState.Away;
        awayPhaseElapsed = 0f;
        realReturnMidpointFired = false;
        IsRealReturnCountdownActive = false;
        targetAwayDuration = RollNextAwayDuration();
    }

    private void EnterBedtime()
    {
        bedtimeHasHappened = true;
        CurrentState = ChefPresenceState.Bedtime;
        bedtimeRemaining = Mathf.Max(0f, config.BedtimeDuration);
        fakeOutActive = false;
        BedtimeStarted?.Invoke();
    }

    private void StartRealReturnCountdown(float duration)
    {
        IsRealReturnCountdownActive = true;
        RealReturnCountdownDuration = Mathf.Max(duration, 0.0001f);
        RealReturnCountdownRemaining = RealReturnCountdownDuration;
        realReturnMidpointFired = false;
        fakeOutActive = false; // a real countdown always takes over from any in-progress fake-out
    }

    private void TickFakeOut(float deltaTime)
    {
        if (fakeOutActive)
        {
            fakeOutElapsed += deltaTime;
            if (fakeOutElapsed >= config.FakeOutRampDuration)
            {
                fakeOutActive = false;
                fakeOutElapsed = 0f;
            }
            return;
        }

        float perSecondProbability = config.FakeOutFrequencyPerMinute / 60f;
        float probability = PerTickProbability(perSecondProbability, deltaTime);
        if (probability > 0f && randomSource() < probability)
        {
            fakeOutActive = true;
            fakeOutElapsed = 0f;
            fakeOutPeak = Mathf.Lerp(config.FakeOutPeakRange.x, config.FakeOutPeakRange.y, randomSource());
        }
    }

    private float RollNextAwayDuration()
    {
        if (config.KnownAwayDurationRanges == null || config.KnownAwayDurationRanges.Count == 0)
        {
            return FallbackAwayDuration;
        }

        Vector2 range = config.KnownAwayDurationRanges[awayRangeIndex];
        awayRangeIndex = (awayRangeIndex + 1) % config.KnownAwayDurationRanges.Count;
        return Mathf.Max(0f, Mathf.Lerp(range.x, range.y, randomSource()));
    }

    private static float PerTickProbability(float perSecondProbability, float deltaTime)
    {
        if (perSecondProbability <= 0f)
        {
            return 0f;
        }

        perSecondProbability = Mathf.Clamp01(perSecondProbability);
        return 1f - Mathf.Pow(1f - perSecondProbability, deltaTime);
    }
}
