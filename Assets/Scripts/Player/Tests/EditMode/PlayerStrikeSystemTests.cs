using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class PlayerStrikeSystemTests
{
    private GameObject playerObject;
    private WaffleWallet wallet;
    private PlayerStrikeSystem strikeSystem;

    [SetUp]
    public void SetUp()
    {
        playerObject = new GameObject("Player");
        wallet = playerObject.AddComponent<WaffleWallet>();
        wallet.Initialize(20, 20);
        strikeSystem = playerObject.AddComponent<PlayerStrikeSystem>();

        // A synchronous EditMode [Test] never pumps a frame between AddComponent and the test
        // body, so Awake() does not reliably run on its own here (confirmed against the real
        // Test Runner) — force it now, before anything below might move the transform, so
        // spawnPosition is captured at the true starting position.
        strikeSystem.EnsureInitialized();

        // Escalation-math tests below fire several strikes back-to-back with no time between
        // them; invincibility is covered separately, so it's off by default here.
        SetInvincibilityDuration(0f);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(playerObject);
    }

    private void SetInvincibilityDuration(float seconds)
    {
        var so = new SerializedObject(strikeSystem);
        so.FindProperty("invincibilityDuration").floatValue = seconds;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [Test]
    public void FirstStrike_RemovesExactlyHitStrengthWaffles()
    {
        strikeSystem.RegisterStrike(hitStrength: 2);

        Assert.AreEqual(1, strikeSystem.CurrentStrikeCount);
        Assert.AreEqual(18, wallet.CurrentWaffleCount);
    }

    [Test]
    public void SecondStrike_RemovesDoubleHitStrength()
    {
        strikeSystem.RegisterStrike(hitStrength: 2); // -2 -> 18
        strikeSystem.RegisterStrike(hitStrength: 3); // -6 -> 12

        Assert.AreEqual(2, strikeSystem.CurrentStrikeCount);
        Assert.AreEqual(12, wallet.CurrentWaffleCount);
    }

    [Test]
    public void ThirdStrike_WipesTheWalletEntirely_RegardlessOfHitStrength()
    {
        strikeSystem.RegisterStrike(hitStrength: 1); // -1  -> 19
        strikeSystem.RegisterStrike(hitStrength: 1); // -2  -> 17
        strikeSystem.RegisterStrike(hitStrength: 1); // wipe -> 0

        Assert.AreEqual(3, strikeSystem.CurrentStrikeCount);
        Assert.AreEqual(0, wallet.CurrentWaffleCount);
    }

    [Test]
    public void ThirdStrike_FiresFullStashWipedEvent()
    {
        int wipeCount = 0;
        strikeSystem.FullStashWiped += () => wipeCount++;

        strikeSystem.RegisterStrike(1);
        strikeSystem.RegisterStrike(1);
        Assert.AreEqual(0, wipeCount, "Strikes 1 and 2 must not fire the full-wipe hook.");

        strikeSystem.RegisterStrike(1);

        Assert.AreEqual(1, wipeCount);
    }

    [Test]
    public void StrikesBeyondThird_AlsoWipe_RegardlessOfCitizenType()
    {
        strikeSystem.RegisterStrike(1);
        strikeSystem.RegisterStrike(1);
        strikeSystem.RegisterStrike(1); // wiped by strike 3

        strikeSystem.RegisterStrike(3); // a Guard catches them post-wipe

        Assert.AreEqual(4, strikeSystem.CurrentStrikeCount);
        Assert.AreEqual(0, wallet.CurrentWaffleCount, "Nothing left to lose, but this must not throw or go negative.");
    }

    [Test]
    public void StrikeEscalation_IsIndependentOfWhichCitizenTypeCausedEachStrike()
    {
        // Tourist (1), then Guard (3), then Vendor (2) — the escalation (x1, x2, wipe) must
        // depend only on strike NUMBER, never on which type caused it.
        strikeSystem.RegisterStrike(1); // strike 1: -1  -> 19
        strikeSystem.RegisterStrike(3); // strike 2: -6  -> 13
        strikeSystem.RegisterStrike(2); // strike 3: wipe -> 0

        Assert.AreEqual(0, wallet.CurrentWaffleCount);
    }

    [Test]
    public void RemovingMoreWafflesThanRemaining_ClampsAtZero_NeverNegative()
    {
        strikeSystem.RegisterStrike(hitStrength: 50); // strike 1, but only 20 waffles exist

        Assert.AreEqual(0, wallet.CurrentWaffleCount);
    }

    [Test]
    public void RegisterStrike_RespawnsAtTheRecordedStartPosition()
    {
        playerObject.transform.position = new Vector3(3f, 1f, 0f); // simulate having walked off from spawn
        // spawnPosition was captured in Awake at the original (0,0,0) position, before the move above.

        strikeSystem.RegisterStrike(1);

        Assert.AreEqual(Vector3.zero, playerObject.transform.position, "Respawn must return to the level start position.");
    }

    [Test]
    public void RegisterStrike_KeepsWhateverRemainsInTheStash_DoesNotFullyWipeOnStrike1Or2()
    {
        strikeSystem.RegisterStrike(hitStrength: 2);

        Assert.AreEqual(18, wallet.CurrentWaffleCount, "A strike-1/2 catch must leave the remainder, unlike a full wipe.");
    }

    [Test]
    public void ResetStrikes_ClearsTheCountForANewLevelAttempt()
    {
        strikeSystem.RegisterStrike(1);
        strikeSystem.RegisterStrike(1);

        strikeSystem.ResetStrikes();

        Assert.AreEqual(0, strikeSystem.CurrentStrikeCount);
    }

    // ---- Respawn invincibility ----

    [Test]
    public void NotInvincible_BeforeAnyStrike()
    {
        Assert.IsFalse(strikeSystem.IsInvincible);
    }

    [Test]
    public void BecomesInvincible_ImmediatelyAfterAStrike()
    {
        SetInvincibilityDuration(1.5f);

        strikeSystem.RegisterStrike(1);

        Assert.IsTrue(strikeSystem.IsInvincible);
    }

    [Test]
    public void DuringInvincibilityWindow_AFurtherCatchIsIgnoredEntirely()
    {
        SetInvincibilityDuration(1.5f);
        strikeSystem.RegisterStrike(2); // strike 1: -2 -> 18

        strikeSystem.RegisterStrike(3); // should be fully ignored — still invincible

        Assert.AreEqual(1, strikeSystem.CurrentStrikeCount, "A catch during invincibility must not count as a strike.");
        Assert.AreEqual(18, wallet.CurrentWaffleCount, "...and must not remove any more waffles.");
    }

    [Test]
    public void DuringInvincibilityWindow_IgnoredCatchDoesNotFireStruckOrRespawnAgain()
    {
        SetInvincibilityDuration(1.5f);
        strikeSystem.RegisterStrike(2);
        playerObject.transform.position = new Vector3(4f, 4f, 0f); // move after the (ignored) respawn point

        int struckCalls = 0;
        strikeSystem.Struck += _ => struckCalls++;

        strikeSystem.RegisterStrike(3); // ignored: no event, no re-teleport to spawn

        Assert.AreEqual(0, struckCalls);
        Assert.AreEqual(new Vector3(4f, 4f, 0f), playerObject.transform.position, "An ignored catch must not respawn the player.");
    }

    [Test]
    public void AfterInvincibilityExpires_ACatchStrikesAgain()
    {
        SetInvincibilityDuration(1.5f);
        strikeSystem.RegisterStrike(2); // strike 1: -2 -> 18

        strikeSystem.Tick(1.51f); // advance past the window
        Assert.IsFalse(strikeSystem.IsInvincible);

        strikeSystem.RegisterStrike(3); // strike 2: -6 -> 12

        Assert.AreEqual(2, strikeSystem.CurrentStrikeCount);
        Assert.AreEqual(12, wallet.CurrentWaffleCount);
    }

    [Test]
    public void Invincibility_CountsDownGraduallyAcrossMultipleTicks()
    {
        SetInvincibilityDuration(1.0f);
        strikeSystem.RegisterStrike(1);

        strikeSystem.Tick(0.6f);
        Assert.IsTrue(strikeSystem.IsInvincible, "0.6s of 1.0s elapsed — still within the window.");

        strikeSystem.Tick(0.5f); // total 1.1s
        Assert.IsFalse(strikeSystem.IsInvincible);
    }

    [Test]
    public void ZeroDurationConfigured_NeverBlocksAnyCatch()
    {
        // SetUp already configures 0 — this documents that 0 is a valid "off" setting, not a bug.
        strikeSystem.RegisterStrike(1);

        Assert.IsFalse(strikeSystem.IsInvincible);
    }
}
