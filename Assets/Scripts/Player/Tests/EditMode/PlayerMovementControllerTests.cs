using NUnit.Framework;
using UnityEngine;

public class PlayerMovementControllerTests
{
    private sealed class FakeConfig : IPlayerMovementConfig
    {
        public float RunSpeed => 5f;
        public float JumpVelocity => 10f;
        public float ClimbSpeed => 3f;
        public Vector2 WallJumpVelocity => new Vector2(7f, 9f);
        public float GravityScale => 3f;
    }

    private PlayerMovementController controller;

    [SetUp]
    public void SetUp()
    {
        controller = new PlayerMovementController(new FakeConfig());
    }

    private Vector2 Step(float horizontalInput = 0f, float climbInput = 0f)
    {
        return controller.Tick(Vector2.zero, horizontalInput, climbInput);
    }

    [Test]
    public void StartsGrounded()
    {
        Assert.AreEqual(PlayerMovementState.Grounded, controller.State);
    }

    [Test]
    public void Grounded_RunInput_MovesAtRunSpeed()
    {
        controller.SetEnvironment(grounded: true, touchingClimbable: false);

        Vector2 velocity = Step(horizontalInput: 1f);

        Assert.AreEqual(5f, velocity.x, 0.0001f);
        Assert.AreEqual(PlayerMovementState.Grounded, controller.State);
    }

    [Test]
    public void Grounded_HorizontalInputIsClampedToUnit()
    {
        controller.SetEnvironment(grounded: true, touchingClimbable: false);

        Vector2 velocity = Step(horizontalInput: 5f);

        Assert.AreEqual(5f, velocity.x, 0.0001f, "Input beyond +/-1 must not scale run speed past RunSpeed.");
    }

    [Test]
    public void Grounded_QueueJump_GoesAirborneAndAppliesJumpVelocity()
    {
        controller.SetEnvironment(grounded: true, touchingClimbable: false);
        controller.QueueJump();

        Vector2 velocity = Step();

        Assert.AreEqual(PlayerMovementState.Airborne, controller.State);
        Assert.AreEqual(10f, velocity.y, 0.0001f);
    }

    [Test]
    public void GroundedToJumpingToGrounded_FullCycle()
    {
        controller.SetEnvironment(grounded: true, touchingClimbable: false);
        controller.QueueJump();
        Step();
        Assert.AreEqual(PlayerMovementState.Airborne, controller.State, "Should be airborne right after jumping.");

        controller.SetEnvironment(grounded: false, touchingClimbable: false);
        Step();
        Assert.AreEqual(PlayerMovementState.Airborne, controller.State);

        controller.SetEnvironment(grounded: true, touchingClimbable: false);
        Step();
        Assert.AreEqual(PlayerMovementState.Grounded, controller.State);
    }

    [Test]
    public void Airborne_SecondJumpQueued_DoesNotDoubleJump()
    {
        controller.SetEnvironment(grounded: true, touchingClimbable: false);
        controller.QueueJump();
        Step();

        controller.SetEnvironment(grounded: false, touchingClimbable: false);
        controller.QueueJump();
        Vector2 velocity = controller.Tick(new Vector2(0f, -2f), 0f, 0f);

        Assert.AreEqual(PlayerMovementState.Airborne, controller.State);
        Assert.AreEqual(-2f, velocity.y, 0.0001f, "Airborne jump request must be ignored, preserving current fall velocity.");
    }

    [Test]
    public void Grounded_WalkOffLedge_BecomesAirborneWithoutJump()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: false);

        Step();

        Assert.AreEqual(PlayerMovementState.Airborne, controller.State);
    }

    // ---- Climbing: entry / exit ----

    [Test]
    public void Grounded_ContactWithClimbable_EntersClimb_AndHangsWithNoInput()
    {
        controller.SetEnvironment(grounded: true, touchingClimbable: true);

        Vector2 velocity = Step();

        Assert.AreEqual(PlayerMovementState.Climbing, controller.State);
        Assert.AreEqual(0f, velocity.x, 0.0001f);
        Assert.AreEqual(0f, velocity.y, 0.0001f, "No climb input -> hang in place, no auto-ascent.");
    }

    [Test]
    public void Climbing_DisablesGravity()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: true);
        Step();

        Assert.IsFalse(controller.GravityActive);
    }

    [Test]
    public void GroundedToClimbingToGrounded_FullCycle()
    {
        controller.SetEnvironment(grounded: true, touchingClimbable: false);
        Step();
        Assert.AreEqual(PlayerMovementState.Grounded, controller.State);

        controller.SetEnvironment(grounded: false, touchingClimbable: true);
        Step();
        Assert.AreEqual(PlayerMovementState.Climbing, controller.State);

        controller.SetEnvironment(grounded: true, touchingClimbable: false);
        Step();
        Assert.AreEqual(PlayerMovementState.Grounded, controller.State);
    }

    [Test]
    public void Climbing_LeaveSurfaceInMidAir_ReturnsToAirborne()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: true);
        Step();
        Assert.AreEqual(PlayerMovementState.Climbing, controller.State);

        controller.SetEnvironment(grounded: false, touchingClimbable: false);
        Step();
        Assert.AreEqual(PlayerMovementState.Airborne, controller.State);
    }

    // ---- Climbing: drag-driven vertical movement ----

    [Test]
    public void Climbing_NoClimbInput_HangsInPlace()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: true);

        Vector2 velocity = Step(climbInput: 0f);

        Assert.AreEqual(PlayerMovementState.Climbing, controller.State);
        Assert.AreEqual(0f, velocity.x, 0.0001f);
        Assert.AreEqual(0f, velocity.y, 0.0001f);
    }

    [Test]
    public void Climbing_ClimbInputUp_AscendsAtClimbSpeed()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: true);

        Vector2 velocity = Step(climbInput: 1f);

        Assert.AreEqual(3f, velocity.y, 0.0001f);
        Assert.AreEqual(0f, velocity.x, 0.0001f);
    }

    [Test]
    public void Climbing_ClimbInputDown_DescendsAtClimbSpeed()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: true);

        Vector2 velocity = Step(climbInput: -1f);

        Assert.AreEqual(-3f, velocity.y, 0.0001f);
        Assert.AreEqual(0f, velocity.x, 0.0001f);
    }

    [Test]
    public void Climbing_ClimbInputClampedToUnit()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: true);

        Vector2 velocity = Step(climbInput: 5f);

        Assert.AreEqual(3f, velocity.y, 0.0001f, "Climb input beyond +/-1 must not scale past ClimbSpeed.");
    }

    [Test]
    public void Climbing_ReleasingClimbInput_StopsImmediately_NoResidual()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: true);
        controller.Tick(Vector2.zero, 0f, 1f); // ascending

        // Next step with the drag released, carrying the previous climb velocity in.
        Vector2 velocity = controller.Tick(new Vector2(0f, 3f), 0f, 0f);

        Assert.AreEqual(0f, velocity.x, 0.0001f);
        Assert.AreEqual(0f, velocity.y, 0.0001f, "Hold-to-move: releasing drops climb velocity to zero, no coast.");
    }

    [Test]
    public void Climbing_IgnoresRunInput_NoLateralMovementOnWall()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: true);

        Vector2 velocity = controller.Tick(new Vector2(99f, 0f), horizontalInput: 1f, climbInput: 1f);

        Assert.AreEqual(0f, velocity.x, 0.0001f, "Run input must not move the player sideways on the wall.");
        Assert.AreEqual(3f, velocity.y, 0.0001f, "Vertical movement still follows the climb input.");
    }

    // ---- Wall-jump ----

    [Test]
    public void Climbing_JumpInput_WallJumpsAwayFromAWallOnTheRight()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: true, wallDirection: 1f);
        Step();
        Assert.AreEqual(PlayerMovementState.Climbing, controller.State);

        controller.SetEnvironment(grounded: false, touchingClimbable: true, wallDirection: 1f);
        controller.QueueJump();
        Vector2 velocity = Step();

        Assert.AreEqual(PlayerMovementState.Airborne, controller.State, "Wall-jump exits Climbing into Airborne.");
        Assert.AreEqual(-7f, velocity.x, 0.0001f, "Push is away from the wall (wall on right -> push left).");
        Assert.AreEqual(9f, velocity.y, 0.0001f, "Upward impulse from WallJumpVelocity.y.");
    }

    [Test]
    public void Climbing_JumpInput_WallJumpsAwayFromAWallOnTheLeft()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: true, wallDirection: -1f);
        Step();

        controller.SetEnvironment(grounded: false, touchingClimbable: true, wallDirection: -1f);
        controller.QueueJump();
        Vector2 velocity = Step();

        Assert.AreEqual(PlayerMovementState.Airborne, controller.State);
        Assert.AreEqual(7f, velocity.x, 0.0001f, "Wall on left -> push right.");
    }

    [Test]
    public void Climbing_WallJumpWorksWhileDragging()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: true, wallDirection: 1f);
        controller.Tick(Vector2.zero, 0f, -1f); // dragging down

        controller.SetEnvironment(grounded: false, touchingClimbable: true, wallDirection: 1f);
        controller.QueueJump();
        Vector2 velocity = controller.Tick(Vector2.zero, 0f, -1f);

        Assert.AreEqual(PlayerMovementState.Airborne, controller.State);
        Assert.AreEqual(-7f, velocity.x, 0.0001f);
        Assert.AreEqual(9f, velocity.y, 0.0001f);
    }

    [Test]
    public void WallJump_DoesNotInstantlyReGrab_WhileStillTouchingAndMovingAway()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: true, wallDirection: 1f);
        Step();
        controller.QueueJump();
        Step();
        Assert.AreEqual(PlayerMovementState.Airborne, controller.State);

        controller.SetEnvironment(grounded: false, touchingClimbable: true, wallDirection: 1f);
        Vector2 velocity = controller.Tick(new Vector2(-7f, 4f), 0f, 0f);

        Assert.AreEqual(PlayerMovementState.Airborne, controller.State, "Kick-off must not be cancelled by an instant re-grab.");
        Assert.AreEqual(-7f, velocity.x, 0.0001f, "The away-from-wall push is preserved, not zeroed.");
    }

    [Test]
    public void WallJump_ReGrabsOnceMovingBackIntoTheWall()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: true, wallDirection: 1f);
        Step();
        controller.QueueJump();
        Step();

        controller.SetEnvironment(grounded: false, touchingClimbable: true, wallDirection: 1f);
        controller.Tick(new Vector2(3f, 1f), 0f, 0f);

        Assert.AreEqual(PlayerMovementState.Climbing, controller.State);
    }

    [Test]
    public void ClimbTakesPrecedenceOverAQueuedJumpFromTheGround()
    {
        controller.SetEnvironment(grounded: true, touchingClimbable: true);
        controller.QueueJump();

        Vector2 velocity = Step();

        Assert.AreEqual(PlayerMovementState.Climbing, controller.State);
        Assert.AreEqual(0f, velocity.y, 0.0001f, "The ground jump is dropped; the player just grabs the wall and hangs.");
    }
}
