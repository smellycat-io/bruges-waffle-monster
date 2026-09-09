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

    [Test]
    public void HoldInLeftHalf_PressesRunLeft()
    {
        interpreter.PointerDown(1, screenX: 100f, ScreenWidth, time: 0f);

        CollectionAssert.AreEqual(new[] { "L+" }, target.Calls);
    }

    [Test]
    public void HoldInRightHalf_PressesRunRight()
    {
        interpreter.PointerDown(1, screenX: 900f, ScreenWidth, time: 0f);

        CollectionAssert.AreEqual(new[] { "R+" }, target.Calls);
    }

    [Test]
    public void ExactMidpoint_CountsAsRightHalf()
    {
        // x == width * 0.5 is NOT strictly less than the split -> right zone.
        interpreter.PointerDown(1, screenX: ScreenWidth * 0.5f, ScreenWidth, time: 0f);

        CollectionAssert.AreEqual(new[] { "R+" }, target.Calls);
    }

    [Test]
    public void CustomLeftZoneFraction_MovesTheSplit()
    {
        var wideLeft = new TapZoneInterpreter(target, TapMax, leftZoneFraction: 0.75f);

        wideLeft.PointerDown(1, screenX: 700f, ScreenWidth, time: 0f); // left of 750 -> left zone

        CollectionAssert.AreEqual(new[] { "L+" }, target.Calls);
    }

    [Test]
    public void QuickRelease_FiresJump_AfterReleasingTheRun()
    {
        interpreter.PointerDown(1, screenX: 100f, ScreenWidth, time: 0f);
        interpreter.PointerUp(1, time: 0.1f); // 0.10s <= 0.18s

        CollectionAssert.AreEqual(new[] { "L+", "L-", "JUMP" }, target.Calls);
    }

    [Test]
    public void ReleaseExactlyAtThreshold_StillCountsAsTap()
    {
        interpreter.PointerDown(1, screenX: 900f, ScreenWidth, time: 5f);
        interpreter.PointerUp(1, time: 5f + TapMax);

        CollectionAssert.AreEqual(new[] { "R+", "R-", "JUMP" }, target.Calls);
    }

    [Test]
    public void SustainedHold_ReleasesRun_ButDoesNotJump()
    {
        interpreter.PointerDown(1, screenX: 100f, ScreenWidth, time: 0f);
        interpreter.PointerUp(1, time: 0.5f); // well past the tap threshold

        CollectionAssert.AreEqual(new[] { "L+", "L-" }, target.Calls);
    }

    [Test]
    public void SecondFingerSameHalf_DoesNotRePress_AndHoldLastsUntilBothLift()
    {
        interpreter.PointerDown(1, screenX: 100f, ScreenWidth, time: 0f);
        interpreter.PointerDown(2, screenX: 200f, ScreenWidth, time: 0.05f);
        Assert.AreEqual(1, CountOf(target.Calls, "L+"), "Run-left should be pressed once, not twice.");

        interpreter.PointerUp(1, time: 1f);
        Assert.AreEqual(0, CountOf(target.Calls, "L-"), "Still held by the second finger.");

        interpreter.PointerUp(2, time: 2f);
        Assert.AreEqual(1, CountOf(target.Calls, "L-"), "Released once both fingers are up.");
    }

    [Test]
    public void HoldOneHalf_TapTheOther_RunsAndJumps()
    {
        interpreter.PointerDown(1, screenX: 100f, ScreenWidth, time: 0f);   // hold left
        interpreter.PointerDown(2, screenX: 900f, ScreenWidth, time: 1f);   // tap right...
        interpreter.PointerUp(2, time: 1.05f);

        CollectionAssert.AreEqual(new[] { "L+", "R+", "R-", "JUMP" }, target.Calls);
    }

    [Test]
    public void Cancelled_ReleasesRun_WithoutJump()
    {
        interpreter.PointerDown(1, screenX: 100f, ScreenWidth, time: 0f);
        interpreter.PointerCancelled(1);

        CollectionAssert.AreEqual(new[] { "L+", "L-" }, target.Calls);
    }

    [Test]
    public void Reset_ReleasesEveryHeldDirectionOnce()
    {
        interpreter.PointerDown(1, screenX: 100f, ScreenWidth, time: 0f);
        interpreter.PointerDown(2, screenX: 900f, ScreenWidth, time: 0f);
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

    private static int CountOf(List<string> calls, string token)
    {
        int n = 0;
        foreach (var c in calls)
        {
            if (c == token) n++;
        }
        return n;
    }
}
