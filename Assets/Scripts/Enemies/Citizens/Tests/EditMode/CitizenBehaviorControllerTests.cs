using NUnit.Framework;

public class CitizenBehaviorControllerTests
{
    private CitizenBehaviorController controller;

    [SetUp]
    public void SetUp()
    {
        controller = new CitizenBehaviorController();
    }

    [Test]
    public void StartsIdle()
    {
        Assert.AreEqual(CitizenChaseState.Idle, controller.State);
    }

    [Test]
    public void LineOfSight_EntersChasing()
    {
        controller.Tick(hasLineOfSightOnPlayer: true, isWalletDrained: false);

        Assert.AreEqual(CitizenChaseState.Chasing, controller.State);
    }

    [Test]
    public void NoLineOfSight_StaysIdle()
    {
        controller.Tick(hasLineOfSightOnPlayer: false, isWalletDrained: false);

        Assert.AreEqual(CitizenChaseState.Idle, controller.State);
    }

    [Test]
    public void LosingLineOfSight_StopsTheChase()
    {
        controller.Tick(hasLineOfSightOnPlayer: true, isWalletDrained: false);
        Assert.AreEqual(CitizenChaseState.Chasing, controller.State);

        controller.Tick(hasLineOfSightOnPlayer: false, isWalletDrained: false);

        Assert.AreEqual(CitizenChaseState.Idle, controller.State, "Losing line-of-sight must stop the chase immediately, no lingering pursuit.");
    }

    [Test]
    public void RegainingLineOfSight_ResumesChasing()
    {
        controller.Tick(true, false);
        controller.Tick(false, false);

        controller.Tick(true, false);

        Assert.AreEqual(CitizenChaseState.Chasing, controller.State);
    }

    [Test]
    public void WalletDrained_WhileIdle_Disengages()
    {
        controller.Tick(hasLineOfSightOnPlayer: false, isWalletDrained: true);

        Assert.AreEqual(CitizenChaseState.Disengaged, controller.State);
    }

    [Test]
    public void WalletDrained_WhileChasing_Disengages()
    {
        controller.Tick(true, false);
        Assert.AreEqual(CitizenChaseState.Chasing, controller.State);

        controller.Tick(true, true);

        Assert.AreEqual(CitizenChaseState.Disengaged, controller.State, "Draining the wallet must override an active chase.");
    }

    [Test]
    public void Disengaged_IsPermanent_RegainingLineOfSightDoesNotResumeChasing()
    {
        controller.Tick(true, true); // drained while seeing the player
        Assert.AreEqual(CitizenChaseState.Disengaged, controller.State);

        controller.Tick(hasLineOfSightOnPlayer: true, isWalletDrained: true);
        controller.Tick(hasLineOfSightOnPlayer: true, isWalletDrained: true);

        Assert.AreEqual(CitizenChaseState.Disengaged, controller.State, "Disengaged is a one-way latch for the rest of the level.");
    }

    [Test]
    public void Disengaged_IsPermanent_EvenIfDrainedFlagLaterReadsFalse()
    {
        // Defensive: isWalletDrained should never actually go back to false in real usage
        // (WaffleWallet.Drained only fires once, going 0), but the state machine must not
        // depend on the caller upholding that to stay disengaged.
        controller.Tick(true, true);
        Assert.AreEqual(CitizenChaseState.Disengaged, controller.State);

        controller.Tick(true, false);

        Assert.AreEqual(CitizenChaseState.Disengaged, controller.State);
    }
}
