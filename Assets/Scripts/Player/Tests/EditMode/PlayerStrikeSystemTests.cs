using NUnit.Framework;
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
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(playerObject);
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
}
