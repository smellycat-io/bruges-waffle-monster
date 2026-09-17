using NUnit.Framework;
using UnityEngine;

public class CitizenWanderControllerTests
{
    private const float SpawnX = 10f;

    private static CitizenWanderController NewController(float radius = 3f, float pauseDuration = 1f)
        => new CitizenWanderController(new Vector2(SpawnX, 0f), radius, pauseDuration);

    [Test]
    public void Inactive_NeverProducesATarget()
    {
        var controller = NewController();

        controller.Tick(isActive: false, currentX: SpawnX, deltaTime: 0.02f);

        Assert.IsNull(controller.CurrentTargetX);
    }

    [Test]
    public void BecomingActive_ImmediatelyPicksATarget()
    {
        var controller = NewController();

        controller.Tick(isActive: true, currentX: SpawnX, deltaTime: 0.02f);

        Assert.IsNotNull(controller.CurrentTargetX, "Wandering should begin the moment Idle starts, not after an initial pause.");
    }

    [Test]
    public void WanderTargets_AreAlwaysWithinRadiusOfSpawn()
    {
        for (int i = 0; i < 200; i++)
        {
            var controller = NewController(radius: 3f);
            controller.Tick(isActive: true, currentX: SpawnX, deltaTime: 0.02f);

            float target = controller.CurrentTargetX.Value;
            Assert.GreaterOrEqual(target, SpawnX - 3f);
            Assert.LessOrEqual(target, SpawnX + 3f);
        }
    }

    [Test]
    public void WanderTargets_VaryOnBothSidesOfSpawn_AcrossManyTrials()
    {
        bool sawLeftOfSpawn = false;
        bool sawRightOfSpawn = false;

        for (int i = 0; i < 200; i++)
        {
            var controller = NewController(radius: 3f);
            controller.Tick(isActive: true, currentX: SpawnX, deltaTime: 0.02f);

            float target = controller.CurrentTargetX.Value;
            if (target < SpawnX) sawLeftOfSpawn = true;
            if (target > SpawnX) sawRightOfSpawn = true;
        }

        Assert.IsTrue(sawLeftOfSpawn, "Wander targets should sometimes land left of spawn.");
        Assert.IsTrue(sawRightOfSpawn, "Wander targets should sometimes land right of spawn.");
    }

    [Test]
    public void GoingInactiveMidWander_ImmediatelyClearsTheTarget()
    {
        var controller = NewController();
        controller.Tick(isActive: true, currentX: SpawnX, deltaTime: 0.02f);
        Assert.IsNotNull(controller.CurrentTargetX);

        controller.Tick(isActive: false, currentX: SpawnX, deltaTime: 0.02f); // e.g. line-of-sight found -> Chasing

        Assert.IsNull(controller.CurrentTargetX, "Wandering must stop the instant it's no longer Idle — must never delay/interfere with chasing.");
    }

    [Test]
    public void ArrivingAtTarget_ClearsTheTargetAndPauses()
    {
        var controller = NewController();
        controller.Tick(isActive: true, currentX: SpawnX, deltaTime: 0.02f);
        float target = controller.CurrentTargetX.Value;

        controller.Tick(isActive: true, currentX: target, deltaTime: 0.02f); // arrived exactly

        Assert.IsNull(controller.CurrentTargetX, "Arriving should pause (no target) rather than instantly picking a new one.");
    }

    [Test]
    public void DuringThePause_NoNewTargetIsPickedUntilTheDurationElapses()
    {
        var controller = NewController(pauseDuration: 1f);
        controller.Tick(isActive: true, currentX: SpawnX, deltaTime: 0.02f);
        float target = controller.CurrentTargetX.Value;
        controller.Tick(isActive: true, currentX: target, deltaTime: 0.02f); // arrived -> pausing

        controller.Tick(isActive: true, currentX: target, deltaTime: 0.5f); // still within the 1s pause

        Assert.IsNull(controller.CurrentTargetX, "Should still be pausing at 0.5s of a 1s pause.");
    }

    [Test]
    public void AfterThePauseElapses_ANewTargetIsPickedOnTheTickThatCrossesTheDuration()
    {
        var controller = NewController(pauseDuration: 1f);
        controller.Tick(isActive: true, currentX: SpawnX, deltaTime: 0.02f);
        float firstTarget = controller.CurrentTargetX.Value;
        controller.Tick(isActive: true, currentX: firstTarget, deltaTime: 0.02f); // arrived -> pausing

        controller.Tick(isActive: true, currentX: firstTarget, deltaTime: 0.5f); // 0.5s elapsed, still pausing
        controller.Tick(isActive: true, currentX: firstTarget, deltaTime: 0.6f); // 1.1s elapsed -> crosses the 1s pause

        Assert.IsNotNull(controller.CurrentTargetX, "A new target should be picked on the same tick the pause duration is crossed, not one tick later.");
    }

    [Test]
    public void ZeroPauseDuration_StillPausesForOneTickBeforePickingTheNextTarget()
    {
        // Documents the behavior explicitly rather than assuming instant re-pick on the exact
        // same tick as arrival (which would make CitizenAI briefly read a target equal to the
        // position it just arrived at) — 0 is a valid "no pause" placeholder, same convention
        // as PlayerStrikeSystem treating a 0 invincibility duration as "off".
        var controller = NewController(pauseDuration: 0f);
        controller.Tick(isActive: true, currentX: SpawnX, deltaTime: 0.02f);
        float target = controller.CurrentTargetX.Value;

        controller.Tick(isActive: true, currentX: target, deltaTime: 0.02f); // arrived
        Assert.IsNull(controller.CurrentTargetX, "Arrival always clears the target for at least one tick, even with a zero pause.");

        controller.Tick(isActive: true, currentX: target, deltaTime: 0.02f);
        Assert.IsNotNull(controller.CurrentTargetX);
    }

    [Test]
    public void ReactivatingAfterAChase_PicksAFreshTarget_FromWhereverTheCitizenCurrentlyIs()
    {
        var controller = NewController(radius: 3f);
        controller.Tick(isActive: true, currentX: SpawnX, deltaTime: 0.02f);
        controller.Tick(isActive: false, currentX: SpawnX, deltaTime: 0.02f); // chase starts

        // Chase carries the citizen well outside the wander radius of spawn...
        float farAwayX = SpawnX + 50f;
        controller.Tick(isActive: false, currentX: farAwayX, deltaTime: 0.02f);

        // ...and losing the player resumes wandering from there, not from spawn.
        controller.Tick(isActive: true, currentX: farAwayX, deltaTime: 0.02f);

        Assert.IsNotNull(controller.CurrentTargetX, "Should resume wandering immediately, not wait out a stale pause.");
        Assert.GreaterOrEqual(controller.CurrentTargetX.Value, SpawnX - 3f, "The new target is still bounded by radius of the ORIGINAL spawn point, not the citizen's current (chased-to) position.");
        Assert.LessOrEqual(controller.CurrentTargetX.Value, SpawnX + 3f);
    }
}
