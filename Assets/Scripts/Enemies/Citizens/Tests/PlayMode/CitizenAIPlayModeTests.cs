using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

/// <summary>
/// Integration coverage for <see cref="CitizenAI"/> against real Physics2D raycasts and
/// real trigger contacts: line-of-sight starting/stopping the chase based on obstruction,
/// and catching the player actually triggering a strike.
/// </summary>
public class CitizenAIPlayModeTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private CitizenTypeData typeData;

    [SetUp]
    public void SetUp()
    {
        typeData = ScriptableObject.CreateInstance<CitizenTypeData>();
        var so = new UnityEditor.SerializedObject(typeData);
        so.FindProperty("minWalletSize").intValue = 1;
        so.FindProperty("maxWalletSize").intValue = 1;
        so.FindProperty("hitStrength").intValue = 2;
        so.FindProperty("moveSpeed").floatValue = 3f;
        so.FindProperty("detectionRange").floatValue = 10f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in spawned)
        {
            if (go != null) Object.Destroy(go);
        }
        spawned.Clear();
        Object.DestroyImmediate(typeData);
    }

    private CitizenAI CreateCitizen(Vector2 position)
    {
        var go = new GameObject("Citizen");
        go.transform.position = position;
        spawned.Add(go);

        go.AddComponent<Rigidbody2D>();
        var col = go.AddComponent<CapsuleCollider2D>();
        col.isTrigger = true;
        go.AddComponent<WaffleWallet>();

        var ai = go.AddComponent<CitizenAI>();
        var so = new UnityEditor.SerializedObject(ai);
        so.FindProperty("typeData").objectReferenceValue = typeData;
        so.ApplyModifiedPropertiesWithoutUndo();
        return ai;
    }

    private GameObject CreateTarget(Vector2 position, bool withStrikeSystem)
    {
        var go = new GameObject("Target");
        go.transform.position = position;
        spawned.Add(go);

        if (withStrikeSystem)
        {
            var wallet = go.AddComponent<WaffleWallet>();
            wallet.Initialize(10, 10);
            go.AddComponent<PlayerStrikeSystem>();
        }
        return go;
    }

    private GameObject CreateSolidObstacle(Vector2 position, Vector2 size)
    {
        var go = new GameObject("Obstacle");
        go.transform.position = position;
        spawned.Add(go);
        go.AddComponent<BoxCollider2D>().size = size;
        return go;
    }

    private static IEnumerator FixedSteps(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForFixedUpdate();
        }
    }

    [UnityTest]
    public IEnumerator ClearLineOfSight_EntersChasing()
    {
        var target = CreateTarget(new Vector2(5f, 0f), withStrikeSystem: false);
        var citizen = CreateCitizen(Vector2.zero);
        citizen.SetTarget(target.transform);

        yield return FixedSteps(3);

        Assert.AreEqual(CitizenChaseState.Chasing, citizen.State, "Nothing between citizen and target -> should be chasing.");
    }

    [UnityTest]
    public IEnumerator ObstructedLineOfSight_StaysIdle()
    {
        var target = CreateTarget(new Vector2(5f, 0f), withStrikeSystem: false);
        CreateSolidObstacle(new Vector2(2.5f, 0f), new Vector2(1f, 3f));
        var citizen = CreateCitizen(Vector2.zero);
        citizen.SetTarget(target.transform);

        yield return FixedSteps(3);

        Assert.AreEqual(CitizenChaseState.Idle, citizen.State, "A solid obstacle directly between them must block the chase from ever starting.");
    }

    [UnityTest]
    public IEnumerator LosingLineOfSightMidChase_StopsTheChase()
    {
        var target = CreateTarget(new Vector2(5f, 0f), withStrikeSystem: false);
        var obstacle = CreateSolidObstacle(new Vector2(2.5f, 0f), new Vector2(1f, 3f));
        obstacle.SetActive(false); // starts clear
        var citizen = CreateCitizen(Vector2.zero);
        citizen.SetTarget(target.transform);

        yield return FixedSteps(3);
        Assert.AreEqual(CitizenChaseState.Chasing, citizen.State, "Should start chasing while the path is clear.");

        obstacle.SetActive(true); // player ducks behind the building
        yield return FixedSteps(3);

        Assert.AreEqual(CitizenChaseState.Idle, citizen.State, "Breaking line of sight must stop the chase.");
    }

    [UnityTest]
    public IEnumerator BeyondDetectionRange_NeverChases_EvenWithClearSight()
    {
        var target = CreateTarget(new Vector2(50f, 0f), withStrikeSystem: false); // detectionRange is 10
        var citizen = CreateCitizen(Vector2.zero);
        citizen.SetTarget(target.transform);

        yield return FixedSteps(3);

        Assert.AreEqual(CitizenChaseState.Idle, citizen.State);
    }

    [UnityTest]
    public IEnumerator ChasingCitizen_CatchingThePlayer_TriggersAStrike()
    {
        var target = CreateTarget(new Vector2(1f, 0f), withStrikeSystem: true);
        var strikeSystem = target.GetComponent<PlayerStrikeSystem>();
        var citizen = CreateCitizen(Vector2.zero);
        citizen.SetTarget(target.transform);

        // Let the citizen see + walk into the target (they start well within contact range and speed > 0).
        bool caught = false;
        for (int i = 0; i < 60 && !caught; i++)
        {
            yield return new WaitForFixedUpdate();
            caught = strikeSystem.CurrentStrikeCount > 0;
        }

        Assert.IsTrue(caught, "Contact with the target while Chasing should register a strike.");
        Assert.AreEqual(CitizenChaseState.Chasing, citizen.State, "Catching the player doesn't itself change the citizen's state.");
    }

    [UnityTest]
    public IEnumerator DisengagedCitizen_TouchingThePlayer_DoesNotStrike()
    {
        var target = CreateTarget(new Vector2(0.3f, 0f), withStrikeSystem: true); // already overlapping
        var strikeSystem = target.GetComponent<PlayerStrikeSystem>();
        var citizen = CreateCitizen(Vector2.zero);
        citizen.SetTarget(target.transform);
        citizen.GetComponent<WaffleWallet>().RemoveWaffles(999); // drains the rolled 1-waffle wallet, fires Drained

        yield return FixedSteps(5);

        Assert.AreEqual(CitizenChaseState.Disengaged, citizen.State);
        Assert.AreEqual(0, strikeSystem.CurrentStrikeCount, "A disengaged (crying) citizen must not be able to catch the player.");
    }

    [UnityTest]
    public IEnumerator DrainingTheWallet_DisengagesAndPermanentlyStopsChasing()
    {
        var target = CreateTarget(new Vector2(5f, 0f), withStrikeSystem: false);
        var citizen = CreateCitizen(Vector2.zero);
        citizen.SetTarget(target.transform);

        yield return FixedSteps(3);
        Assert.AreEqual(CitizenChaseState.Chasing, citizen.State);

        citizen.GetComponent<WaffleWallet>().RemoveWaffles(999); // drains it (started with 1)
        yield return FixedSteps(3);

        Assert.AreEqual(CitizenChaseState.Disengaged, citizen.State);
    }

    [UnityTest]
    public IEnumerator AutoDiscoversPlayerController_WhenTargetNeverExplicitlySet()
    {
        var player = new GameObject("Player");
        spawned.Add(player);
        player.transform.position = new Vector2(3f, 0f);
        player.AddComponent<Rigidbody2D>();
        var wallet = player.AddComponent<WaffleWallet>();
        wallet.Initialize(10, 10);
        player.AddComponent<PlayerStrikeSystem>();
        var playerController = player.AddComponent<PlayerController>(); // the marker CitizenAI looks for
        playerController.Configure(ScriptableObject.CreateInstance<PlayerMovementConfig>()); // avoid its "no config" error log

        var citizen = CreateCitizen(Vector2.zero); // SetTarget is never called

        yield return FixedSteps(3);

        Assert.AreEqual(CitizenChaseState.Chasing, citizen.State, "Should auto-discover the PlayerController and chase it.");
    }
}
