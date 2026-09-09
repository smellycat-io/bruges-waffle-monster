using System.Collections.Generic;
using NUnit.Framework;

public class TapZoneInterpreterTests
{
    private sealed class RecordingTarget : TapZoneInterpreter.ITarget
    {
        public readonly List<string> Calls = new List<string>();
        public void RunLeftPressed() => Calls.Add("L+");
        public void RunLeftReleased() => Calls.Add("L-");
        public void RunRightPressed() => Calls.Add("R+");
        public void RunRightReleased() => Calls.Add("R-");
        public void JumpTapped() => Calls.Add("JUMP");
    }

    private const float TapMax = 0.18f;
    private const float ScreenWidth = 1000f;

    private RecordingTarget target;
    private TapZoneInterpreter interpreter;

    [SetUp]
    public void SetUp()
    {
        target = new RecordingTarget();
        interpreter = new TapZoneInterpreter(target, TapMax);
    }

    private static int CountOf(List<string> calls, string token)
    {
        int n = 0;
        foreach (var c in calls)
        {
            if (c == token) n++;
        }
        return n;
    }

    // ---- deferred run-start ----

    [Test]
    public void PressAlone_DoesNothingYet()
    {
        interpreter.PointerDown(1, screenX: 100f, ScreenWidth, time: 0f);

        CollectionAssert.IsEmpty(target.Calls, "Run must not start on touch-down — only after the hold threshold.");
    }

    [Test]
    public void HeldPastThreshold_StartsRunning()
    {
        interpreter.PointerDown(1, screenX: 100f, ScreenWidth, time: 0f);

        interpreter.Tick(0.1f);
        CollectionAssert.IsEmpty(target.Calls, "Still within the tap window.");

        interpreter.Tick(TapMax);
        CollectionAssert.AreEqual(new[] { "L+" }, target.Calls, "Promoted to a run hold at the threshold.");
    }

    [Test]
    public void QuickRelease_FiresJump_WithNoRunDrift()
    {
        interpreter.PointerDown(1, screenX: 100f, ScreenWidth, time: 0f);
        interpreter.Tick(0.1f);            // frame passes, still within tap window
        interpreter.PointerUp(1, 0.12f);

        CollectionAssert.AreEqual(new[] { "JUMP" }, target.Calls, "A quick tap jumps and never runs.");
    }

    [Test]
    public void ReleaseJustUnderThreshold_CountsAsTap()
    {
        interpreter.PointerDown(1, screenX: 900f, ScreenWidth, time: 5f);
        interpreter.PointerUp(1, 5f + TapMax - 0.001f);

        CollectionAssert.AreEqual(new[] { "JUMP" }, target.Calls);
    }

    [Test]
    public void HeldThenReleased_RunsThenStops_NoJump()
    {
        interpreter.PointerDown(1, screenX: 100f, ScreenWidth, time: 0f);
        interpreter.Tick(0.3f);
        interpreter.PointerUp(1, 0.5f);

        CollectionAssert.AreEqual(new[] { "L+", "L-" }, target.Calls);
    }

    // ---- zone detection ----

    [Test]
    public void HeldInRightHalf_RunsRight()
    {
        interpreter.PointerDown(1, screenX: 900f, ScreenWidth, time: 0f);
        interpreter.Tick(TapMax);

        CollectionAssert.AreEqual(new[] { "R+" }, target.Calls);
    }

    [Test]
    public void ExactMidpoint_CountsAsRightHalf()
    {
        interpreter.PointerDown(1, screenX: ScreenWidth * 0.5f, ScreenWidth, time: 0f);
        interpreter.Tick(TapMax);

        CollectionAssert.AreEqual(new[] { "R+" }, target.Calls);
    }

    [Test]
    public void CustomLeftZoneFraction_MovesTheSplit()
    {
        var wideLeft = new TapZoneInterpreter(target, TapMax, leftZoneFraction: 0.75f);

        wideLeft.PointerDown(1, screenX: 700f, ScreenWidth, time: 0f); // left of 750
        wideLeft.Tick(TapMax);

        CollectionAssert.AreEqual(new[] { "L+" }, target.Calls);
    }

    // ---- multi-touch ----

    [Test]
    public void SecondFingerSameHalf_DoesNotRePress_AndHoldLastsUntilBothLift()
    {
        interpreter.PointerDown(1, screenX: 100f, ScreenWidth, time: 0f);
        interpreter.PointerDown(2, screenX: 200f, ScreenWidth, time: 0.05f);
        interpreter.Tick(0.3f); // both promoted
        Assert.AreEqual(1, CountOf(target.Calls, "L+"), "Run-left pressed once, not twice.");

        interpreter.PointerUp(1, 1f);
        Assert.AreEqual(0, CountOf(target.Calls, "L-"), "Still held by the second finger.");

        interpreter.PointerUp(2, 2f);
        Assert.AreEqual(1, CountOf(target.Calls, "L-"), "Released once both fingers are up.");
    }

    [Test]
    public void HoldOneHalf_QuickTapTheOther_RunsAndJumps()
    {
        interpreter.PointerDown(1, screenX: 100f, ScreenWidth, time: 0f); // hold left
        interpreter.Tick(0.3f);                                           // -> L+
        interpreter.PointerDown(2, screenX: 900f, ScreenWidth, time: 1f); // quick tap right
        interpreter.Tick(1.0f);
        interpreter.PointerUp(2, time: 1.05f);

        CollectionAssert.AreEqual(new[] { "L+", "JUMP" }, target.Calls, "The opposite-side tap jumps without ever running right.");
    }

    // ---- cancel / reset ----

    [Test]
    public void CancelledBeforePromotion_DoesNothing()
    {
        interpreter.PointerDown(1, screenX: 100f, ScreenWidth, time: 0f);
        interpreter.PointerCancelled(1);

        CollectionAssert.IsEmpty(target.Calls);
    }

    [Test]
    public void CancelledAfterPromotion_ReleasesRun_WithoutJump()
    {
        interpreter.PointerDown(1, screenX: 100f, ScreenWidth, time: 0f);
        interpreter.Tick(0.3f);
        interpreter.PointerCancelled(1);

        CollectionAssert.AreEqual(new[] { "L+", "L-" }, target.Calls);
    }

    [Test]
    public void Reset_ReleasesEveryHeldDirectionOnce()
    {
        interpreter.PointerDown(1, screenX: 100f, ScreenWidth, time: 0f);
        interpreter.PointerDown(2, screenX: 900f, ScreenWidth, time: 0f);
        interpreter.Tick(0.3f);
        target.Calls.Clear();

        interpreter.Reset();

        CollectionAssert.AreEquivalent(new[] { "L-", "R-" }, target.Calls);
    }

    [Test]
    public void UnknownPointerUp_IsIgnored()
    {
        interpreter.PointerUp(99, time: 0f);

        CollectionAssert.IsEmpty(target.Calls);
    }
}
