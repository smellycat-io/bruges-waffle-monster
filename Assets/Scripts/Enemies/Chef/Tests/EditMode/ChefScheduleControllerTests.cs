using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Pure EditMode coverage for <see cref="ChefScheduleController"/> — the away/return/present/
/// bedtime state machine, fake-out vs. real-return distinction, and the extend-on-arena-entry
/// behavior. No GameObjects/scene needed; randomness is injected so every test is deterministic.
/// </summary>
public class ChefScheduleControllerTests
{
    private static ChefScheduleConfig MakeConfig(
        List<Vector2> knownAwayDurationRanges = null,
        float randomEarlyReturnProbabilityPerSecond = 0f,
        float bedtimeStartTime = 100000f,
        float bedtimeDuration = 30f,
        float returnWarningLeadTime = 2f,
        float presentDuration = 5f,
        float fakeOutFrequencyPerMinute = 0f,
        float fakeOutRampDuration = 1f,
        Vector2? fakeOutPeakRange = null)
    {
        return new ChefScheduleConfig(
            knownAwayDurationRanges ?? new List<Vector2> { new Vector2(10f, 10f) },
            randomEarlyReturnProbabilityPerSecond,
            bedtimeStartTime,
            bedtimeDuration,
            returnWarningLeadTime,
            presentDuration,
            fakeOutFrequencyPerMinute,
            fakeOutRampDuration,
            fakeOutPeakRange ?? new Vector2(0.5f, 0.5f));
    }

    [Test]
    public void StartsAway()
    {
        var schedule = new ChefScheduleController(MakeConfig());

        Assert.AreEqual(ChefPresenceState.Away, schedule.CurrentState);
        Assert.IsFalse(schedule.IsRealReturnCountdownActive);
    }

    [Test]
    public void ScheduledAwayDuration_ElapsingMinusLeadTime_StartsRealReturnCountdown()
    {
        var config = MakeConfig(new List<Vector2> { new Vector2(4f, 4f) }, returnWarningLeadTime: 4f);
        var schedule = new ChefScheduleController(config, () => 1f); // 1f: never wins any probability roll

        schedule.Tick(0.1f); // awayPhaseElapsed(0.1) >= target(4) - lead(4) = 0 -> countdown starts immediately

        Assert.IsTrue(schedule.IsRealReturnCountdownActive, "Lead time equal to the full away duration should start the countdown on the very first tick.");
    }

    [Test]
    public void RealReturnCountdown_Completing_TransitionsToPresent_AndFiresChefReturned()
    {
        var config = MakeConfig(new List<Vector2> { new Vector2(2f, 2f) }, returnWarningLeadTime: 0f);
        var schedule = new ChefScheduleController(config, () => 1f);
        int returnedCount = 0;
        schedule.ChefReturned += () => returnedCount++;

        for (int i = 0; i < 50; i++)
        {
            schedule.Tick(0.1f); // 5s total, well past the 2s away duration
        }

        Assert.AreEqual(ChefPresenceState.Present, schedule.CurrentState);
        Assert.AreEqual(1, returnedCount);
    }

    [Test]
    public void RealReturnMidpoint_FiresExactlyOnce_BeforeBecomingPresent()
    {
        var config = MakeConfig(new List<Vector2> { new Vector2(4f, 4f) }, returnWarningLeadTime: 4f);
        var schedule = new ChefScheduleController(config, () => 1f);
        int midpointCount = 0;
        schedule.RealReturnMidpointReached += () => midpointCount++;

        for (int i = 0; i < 50; i++)
        {
            schedule.Tick(0.1f); // 5s total, past the ~3.9s countdown
        }

        Assert.AreEqual(1, midpointCount, "Midpoint must fire exactly once for a real return countdown.");
        Assert.AreEqual(ChefPresenceState.Present, schedule.CurrentState);
    }

    [Test]
    public void RandomEarlyReturn_WhenRollSucceeds_StartsCountdownImmediately_RegardlessOfSchedule()
    {
        // Long scheduled away duration (would never naturally return this fast on its own).
        var config = MakeConfig(new List<Vector2> { new Vector2(1000f, 1000f) }, randomEarlyReturnProbabilityPerSecond: 1f);
        var schedule = new ChefScheduleController(config, () => 0f); // 0f always wins any probability roll

        schedule.Tick(1f);

        Assert.IsTrue(schedule.IsRealReturnCountdownActive, "A successful random-early-return roll should start the countdown immediately.");
    }

    [Test]
    public void RandomEarlyReturn_WhenProbabilityIsZero_NeverTriggersEarly()
    {
        var config = MakeConfig(new List<Vector2> { new Vector2(1000f, 1000f) }, randomEarlyReturnProbabilityPerSecond: 0f);
        var schedule = new ChefScheduleController(config, () => 0f); // would always win if probability were > 0

        for (int i = 0; i < 100; i++)
        {
            schedule.Tick(1f);
        }

        Assert.IsFalse(schedule.IsRealReturnCountdownActive);
        Assert.AreEqual(ChefPresenceState.Away, schedule.CurrentState);
    }

    [Test]
    public void FakeOut_RampsBlinkProgress_ButNeverActivatesARealCountdownOrFiresMidpoint()
    {
        var config = MakeConfig(
            new List<Vector2> { new Vector2(1000f, 1000f) },
            fakeOutFrequencyPerMinute: 6000f, // effectively guaranteed to roll true with randomSource() == 0
            fakeOutRampDuration: 1f,
            fakeOutPeakRange: new Vector2(0.5f, 0.5f));
        var schedule = new ChefScheduleController(config, () => 0f);
        int midpointCount = 0;
        schedule.RealReturnMidpointReached += () => midpointCount++;

        bool sawRampedProgress = false;
        for (int i = 0; i < 30; i++)
        {
            schedule.Tick(0.1f);
            if (schedule.BlinkRampProgress01 > 0f)
            {
                sawRampedProgress = true;
            }
            Assert.IsFalse(schedule.IsRealReturnCountdownActive, "A fake-out must never set the real countdown flag.");
        }

        Assert.IsTrue(sawRampedProgress, "The blink ramp should visibly rise during a fake-out.");
        Assert.AreEqual(0, midpointCount, "A fake-out must never fire the (real-return-only) midpoint signal.");
        Assert.AreEqual(ChefPresenceState.Away, schedule.CurrentState, "A fake-out must never actually bring the Chef back.");
    }

    [Test]
    public void FakeOut_ResetsToZeroProgress_AfterItsRampCompletes()
    {
        var config = MakeConfig(
            new List<Vector2> { new Vector2(1000f, 1000f) },
            fakeOutFrequencyPerMinute: 6000f,
            fakeOutRampDuration: 0.5f,
            fakeOutPeakRange: new Vector2(1f, 1f));
        var schedule = new ChefScheduleController(config, () => 0f);

        schedule.Tick(0.1f); // starts a fake-out (progress is legitimately 0 right at the instant it starts)
        schedule.Tick(0.1f); // now partway through the ramp
        Assert.Greater(schedule.BlinkRampProgress01, 0f);

        schedule.Tick(0.5f); // past fakeOutRampDuration -> should reset/stop, not hold at peak

        Assert.AreEqual(0f, schedule.BlinkRampProgress01, 0.001f, "A completed fake-out ramp must reset to idle, not hold at its peak.");
    }

    [Test]
    public void Present_AfterPresentDuration_DepartsBackToAway_AndFiresChefDeparted()
    {
        var config = MakeConfig(new List<Vector2> { new Vector2(1f, 1f) }, returnWarningLeadTime: 0f, presentDuration: 2f);
        var schedule = new ChefScheduleController(config, () => 1f);
        int departedCount = 0;
        schedule.ChefDeparted += () => departedCount++;

        // 8 ticks (4s): ~1s to return, then 2s Present, then departs back to Away around t=3.5s —
        // stopping here (before a second cycle's countdown could start around t=4.5s) keeps this
        // test isolated to exactly one departure rather than however many fit in a longer window.
        for (int i = 0; i < 8; i++)
        {
            schedule.Tick(0.5f);
        }

        Assert.AreEqual(1, departedCount);
        Assert.AreEqual(ChefPresenceState.Away, schedule.CurrentState);
    }

    [Test]
    public void Bedtime_TriggersAtThreshold_AndFiresOnce()
    {
        var config = MakeConfig(new List<Vector2> { new Vector2(1000f, 1000f) }, bedtimeStartTime: 5f, bedtimeDuration: 50f);
        var schedule = new ChefScheduleController(config, () => 1f);
        int bedtimeStartedCount = 0;
        schedule.BedtimeStarted += () => bedtimeStartedCount++;

        for (int i = 0; i < 60; i++)
        {
            schedule.Tick(0.1f); // 6s total, past the 5s bedtime threshold
        }

        Assert.AreEqual(ChefPresenceState.Bedtime, schedule.CurrentState);
        Assert.AreEqual(1, bedtimeStartedCount);
    }

    [Test]
    public void Bedtime_Ends_ResumesNormalCycling_RatherThanStayingSafeForever()
    {
        // Per product decision: after bedtime, the away/return cycle resumes (does not stay away permanently).
        var config = MakeConfig(new List<Vector2> { new Vector2(1000f, 1000f) }, bedtimeStartTime: 1f, bedtimeDuration: 2f);
        var schedule = new ChefScheduleController(config, () => 1f);

        for (int i = 0; i < 15; i++)
        {
            schedule.Tick(0.5f); // 7.5s total: bedtime starts at 1s, lasts 2s, so should resume well before this ends
        }

        Assert.AreEqual(ChefPresenceState.Away, schedule.CurrentState, "Bedtime must end and resume normal Away cycling, not stay Bedtime forever.");
    }

    [Test]
    public void ExtendReturnCountdown_WhileActive_PushesBackBothRemainingAndDuration()
    {
        var config = MakeConfig(new List<Vector2> { new Vector2(10f, 10f) }, returnWarningLeadTime: 10f);
        var schedule = new ChefScheduleController(config, () => 1f);
        schedule.Tick(0.1f); // countdown starts immediately (lead time == full duration)
        Assert.IsTrue(schedule.IsRealReturnCountdownActive);

        float remainingBefore = schedule.RealReturnCountdownRemaining;
        float durationBefore = schedule.RealReturnCountdownDuration;

        schedule.ExtendReturnCountdown(5f);

        Assert.AreEqual(remainingBefore + 5f, schedule.RealReturnCountdownRemaining, 0.001f);
        Assert.AreEqual(durationBefore + 5f, schedule.RealReturnCountdownDuration, 0.001f);
    }

    [Test]
    public void ExtendReturnCountdown_WhileSafelyAway_DelaysWhenTheCountdownWouldStart()
    {
        var config = MakeConfig(new List<Vector2> { new Vector2(10f, 10f) }, returnWarningLeadTime: 2f);
        var schedule = new ChefScheduleController(config, () => 1f);

        schedule.Tick(1f); // awayPhaseElapsed = 1, well before the original warning start at 8
        schedule.ExtendReturnCountdown(5f); // targetAwayDuration 10 -> 15, new warning start at 13

        for (int i = 0; i < 11; i++)
        {
            schedule.Tick(1f); // awayPhaseElapsed goes from 1 to 12 — still short of the extended threshold (13)
        }
        Assert.IsFalse(schedule.IsRealReturnCountdownActive, "The extension should have pushed the countdown start past the original (unextended) threshold.");

        schedule.Tick(1f); // awayPhaseElapsed = 13 -> now past the extended threshold
        Assert.IsTrue(schedule.IsRealReturnCountdownActive);
    }

    [Test]
    public void EmptyKnownAwayDurationRanges_FallsBackRatherThanThrowing()
    {
        var config = MakeConfig(new List<Vector2>(), returnWarningLeadTime: 0f);

        Assert.DoesNotThrow(() =>
        {
            var schedule = new ChefScheduleController(config, () => 1f);
            for (int i = 0; i < 200; i++)
            {
                schedule.Tick(0.1f);
            }
        });
    }
}
