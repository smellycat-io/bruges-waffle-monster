using NUnit.Framework;

public class FallTrackerTests
{
    private FallTracker tracker;

    [SetUp]
    public void SetUp()
    {
        tracker = new FallTracker();
    }

    [Test]
    public void StayingGrounded_NeverReports()
    {
        Assert.IsNull(tracker.Tick(0f, PlayerMovementState.Grounded));
        Assert.IsNull(tracker.Tick(0f, PlayerMovementState.Grounded));
    }

    [Test]
    public void LeavingTheGround_DoesNotReportYet()
    {
        tracker.Tick(0f, PlayerMovementState.Grounded);

        Assert.IsNull(tracker.Tick(0f, PlayerMovementState.Airborne));
    }

    [Test]
    public void SimpleJumpAndLand_ReportsPeakMinusLandingHeight()
    {
        tracker.Tick(0f, PlayerMovementState.Grounded);
        tracker.Tick(2f, PlayerMovementState.Airborne);  // rising
        tracker.Tick(5f, PlayerMovementState.Airborne);  // peak
        tracker.Tick(3f, PlayerMovementState.Airborne);  // falling back past the peak

        float? fall = tracker.Tick(0f, PlayerMovementState.Grounded); // lands back at the start height

        Assert.AreEqual(5f, fall);
    }

    [Test]
    public void WalkingOffALedgeWithNoJump_MeasuresFromTheLeaveHeight()
    {
        tracker.Tick(10f, PlayerMovementState.Grounded);
        tracker.Tick(10f, PlayerMovementState.Airborne); // never rises above the leave height
        tracker.Tick(6f, PlayerMovementState.Airborne);

        float? fall = tracker.Tick(2f, PlayerMovementState.Grounded);

        Assert.AreEqual(8f, fall);
    }

    [Test]
    public void FallDistance_IsNeverNegative_EvenIfLandingIsHigherThanTheTrackedPeak()
    {
        tracker.Tick(0f, PlayerMovementState.Grounded);
        tracker.Tick(0f, PlayerMovementState.Airborne);

        // Landing "detected" above the recorded peak shouldn't happen in practice, but the
        // math must not produce a negative fall distance if it ever does.
        float? fall = tracker.Tick(5f, PlayerMovementState.Grounded);

        Assert.AreEqual(0f, fall);
    }

    [Test]
    public void RemainingAirborne_KeepsExtendingThePeak_NotJustTheFirstHeight()
    {
        tracker.Tick(0f, PlayerMovementState.Grounded);
        tracker.Tick(1f, PlayerMovementState.Airborne);
        tracker.Tick(4f, PlayerMovementState.Airborne); // still rising -> new peak
        tracker.Tick(9f, PlayerMovementState.Airborne); // still rising -> new peak

        float? fall = tracker.Tick(1f, PlayerMovementState.Grounded);

        Assert.AreEqual(8f, fall, "Peak must track the highest point reached, not just the first airborne reading.");
    }

    [Test]
    public void ClimbingUpThenBackDownSafely_NeverCountsAsAFall()
    {
        tracker.Tick(0f, PlayerMovementState.Grounded);
        tracker.Tick(20f, PlayerMovementState.Climbing); // climbed to the top of a tall wall

        float? landedAtTop = tracker.Tick(20f, PlayerMovementState.Grounded); // stepped onto a roof
        Assert.IsNull(landedAtTop, "Climbing (even to a great height) is controlled movement, not falling.");
    }

    [Test]
    public void ClimbingAllTheWayBackToTheGround_NeverCountsAsAFall()
    {
        tracker.Tick(0f, PlayerMovementState.Grounded);
        tracker.Tick(20f, PlayerMovementState.Climbing);

        float? landed = tracker.Tick(0f, PlayerMovementState.Grounded); // climbed all the way back down

        Assert.IsNull(landed, "Deliberately climbing down and stepping off at the bottom is not a fall.");
    }

    [Test]
    public void GrabbingAWallMidFall_BreaksTheFallWithoutPenalty()
    {
        tracker.Tick(20f, PlayerMovementState.Grounded);
        tracker.Tick(20f, PlayerMovementState.Airborne); // falls off
        tracker.Tick(15f, PlayerMovementState.Airborne);

        float? caughtByGrab = tracker.Tick(10f, PlayerMovementState.Climbing); // grabs a wall at y=10
        Assert.IsNull(caughtByGrab, "Grabbing on mid-fall is a catch, not a landing.");
    }

    [Test]
    public void LettingGoAfterAMidFallGrab_OnlyCountsTheFallSincePickingItBackUp()
    {
        tracker.Tick(20f, PlayerMovementState.Grounded);
        tracker.Tick(20f, PlayerMovementState.Airborne);
        tracker.Tick(15f, PlayerMovementState.Airborne);
        tracker.Tick(10f, PlayerMovementState.Climbing); // grabbed at y=10 -> breaks the fall
        tracker.Tick(10f, PlayerMovementState.Airborne); // lets go right there

        float? fall = tracker.Tick(3f, PlayerMovementState.Grounded);

        Assert.AreEqual(7f, fall, "Only the fall from the re-detach point (10) should count, not the original drop from 20.");
    }

    [Test]
    public void ConsecutiveFalls_EachMeasuredIndependently()
    {
        tracker.Tick(0f, PlayerMovementState.Grounded);
        tracker.Tick(0f, PlayerMovementState.Airborne);
        float? firstFall = tracker.Tick(0f, PlayerMovementState.Grounded); // never actually rose -> 0
        Assert.AreEqual(0f, firstFall);

        tracker.Tick(0f, PlayerMovementState.Airborne);
        tracker.Tick(6f, PlayerMovementState.Airborne);
        float? secondFall = tracker.Tick(1f, PlayerMovementState.Grounded);

        Assert.AreEqual(5f, secondFall, "The second fall's peak must not be contaminated by the first fall's tracking.");
    }
}
