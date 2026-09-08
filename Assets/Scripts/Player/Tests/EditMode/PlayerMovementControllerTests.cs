using NUnit.Framework;
using UnityEngine;

public class PlayerMovementControllerTests
{
    private sealed class FakeConfig : IPlayerMovementConfig
    {
        public float RunSpeed => 5f;
        public float JumpVelocity => 10f;
        public float ClimbSpeed => 3f;
        public float GravityScale => 3f;
    }

    private PlayerMovementController controller;

    [SetUp]
    public void SetUp()
    {
        controller = new PlayerMovementController(new FakeConfig());
    }

    private Vector2 Step(float horizontalInput = 0f)
    {
        return controller.Tick(Vector2.zero, horizontalInput);
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

        // Still rising / in the air.
        controller.SetEnvironment(grounded: false, touchingClimbable: false);
        Step();
        Assert.AreEqual(PlayerMovementState.Airborne, controller.State);

        // Landed.
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
        Vector2 velocity = controller.Tick(new Vector2(0f, -2f), 0f);

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

    [Test]
    public void Grounded_ContactWithClimbable_EntersClimb()
    {
        controller.SetEnvironment(grounded: true, touchingClimbable: true);

        Vector2 velocity = Step();

        Assert.AreEqual(PlayerMovementState.Climbing, controller.State);
        Assert.AreEqual(3f, velocity.y, 0.0001f);
        Assert.AreEqual(0f, velocity.x, 0.0001f);
    }

    [Test]
    public void Climbing_DisablesGravity()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: true);
        Step();

        Assert.IsFalse(controller.GravityActive);
    }

    [Test]
    public void Climbing_LocksHorizontalVelocityEvenWithRunInput()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: true);

        Vector2 velocity = controller.Tick(new Vector2(99f, 0f), horizontalInput: 1f);

        Assert.AreEqual(0f, velocity.x, 0.0001f);
    }

    [Test]
    public void GroundedToClimbingToGrounded_FullCycle()
    {
        controller.SetEnvironment(grounded: true, touchingClimbable: false);
        Step();
        Assert.AreEqual(PlayerMovementState.Grounded, controller.State);

        // Touch a wall -> auto climb, no input.
        controller.SetEnvironment(grounded: false, touchingClimbable: true);
        Step();
        Assert.AreEqual(PlayerMovementState.Climbing, controller.State);

        // Climb off the top onto a roof.
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

    [Test]
    public void Climbing_JumpRequestIsIgnoredWhileOnSurface()
    {
        controller.SetEnvironment(grounded: false, touchingClimbable: true);
        Step();

        controller.QueueJump();
        Vector2 velocity = Step();

        Assert.AreEqual(PlayerMovementState.Climbing, controller.State);
        Assert.AreEqual(3f, velocity.y, 0.0001f, "Climb speed must hold; the jump must not fire.");
    }

    [Test]
    public void ClimbTakesPrecedenceOverAQueuedJumpFromTheGround()
    {
        controller.SetEnvironment(grounded: true, touchingClimbable: true);
        controller.QueueJump();

        Vector2 velocity = Step();

        Assert.AreEqual(PlayerMovementState.Climbing, controller.State);
        Assert.AreEqual(3f, velocity.y, 0.0001f);
    }
}
