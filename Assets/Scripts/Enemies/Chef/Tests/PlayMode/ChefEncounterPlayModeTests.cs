using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

/// <summary>
/// Integration coverage for the kitchen hot-zone gating: a real Chef Present state actually
/// causing a restart trigger through real Physics2D trigger contacts, and reaching the door
/// while Away actually transitioning GameStateManager into ArenaState. The schedule's own
/// timing/fake-out logic is covered exhaustively in EditMode by ChefScheduleControllerTests —
/// this file only checks the physics-dependent wiring between real components.
///
/// SceneManager.LoadScene is never actually exercised here (ChefEncounter.reloadSceneOnRestart
/// is disabled) — reloading the active scene mid-test would disrupt the Test Runner itself.
/// </summary>
public class ChefEncounterPlayModeTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<Object> scriptableObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in spawned)
        {
            if (go != null) Object.Destroy(go);
        }
        spawned.Clear();

        foreach (var so in scriptableObjects)
        {
            if (so != null) Object.DestroyImmediate(so);
        }
        scriptableObjects.Clear();
    }

    private ChefScheduleData MakeScheduleData(Vector2 awayRange, float leadTime, float bedtimeStart = 100000f)
    {
        var data = ScriptableObject.CreateInstance<ChefScheduleData>();
        scriptableObjects.Add(data);

        var so = new SerializedObject(data);
        so.FindProperty("knownReturnWindows").arraySize = 1;
        so.FindProperty("knownReturnWindows").GetArrayElementAtIndex(0).vector2Value = awayRange;
        so.FindProperty("returnWarningLeadTime").floatValue = leadTime;
        so.FindProperty("presentDuration").floatValue = 1000f;
        so.FindProperty("bedtime").floatValue = bedtimeStart;
        so.FindProperty("randomReturnProbability").floatValue = 0f;
        so.FindProperty("fakeOutFrequency").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return data;
    }

    private GameObject CreatePlayer(Vector2 position)
    {
        var config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        scriptableObjects.Add(config);
        var configSo = new SerializedObject(config);
        configSo.FindProperty("gravityScale").floatValue = 0f; // keep the player stationary for trigger-overlap assertions
        configSo.ApplyModifiedPropertiesWithoutUndo();

        var go = new GameObject("Player");
        go.transform.position = position;
        spawned.Add(go);

        go.SetActive(false); // PlayerController.Awake() needs the config set before it runs
        go.AddComponent<Rigidbody2D>();
        go.AddComponent<CapsuleCollider2D>();
        var controller = go.AddComponent<PlayerController>();
        controller.Configure(config);
        go.SetActive(true);

        return go;
    }

    private class ChefRig
    {
        public ChefController ChefController;
        public ChefEncounter Encounter;
    }

    private ChefRig CreateChef(ChefScheduleData data)
    {
        var go = new GameObject("Chef");
        spawned.Add(go);

        // Inactive while wiring up: ChefController.Awake() runs the instant AddComponent is
        // called (real Play mode), so scheduleData must already be set before that or Awake
        // logs an error and never retries — same reasoning as CitizenAIPlayModeTests.CreateCitizen.
        go.SetActive(false);

        var chefController = go.AddComponent<ChefController>();
        var chefSo = new SerializedObject(chefController);
        chefSo.FindProperty("scheduleData").objectReferenceValue = data;
        chefSo.ApplyModifiedPropertiesWithoutUndo();

        var encounter = go.AddComponent<ChefEncounter>();
        var encounterSo = new SerializedObject(encounter);
        encounterSo.FindProperty("chefController").objectReferenceValue = chefController;
        encounterSo.FindProperty("reloadSceneOnRestart").boolValue = false; // don't actually reload the Test Runner's scene
        encounterSo.ApplyModifiedPropertiesWithoutUndo();

        go.SetActive(true);

        return new ChefRig { ChefController = chefController, Encounter = encounter };
    }

    private GameObject CreateHotZone(Vector2 position, Vector2 size, ChefEncounter encounter)
    {
        var go = new GameObject("HotZone");
        go.transform.position = position;
        spawned.Add(go);

        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        col.isTrigger = true;

        var zone = go.AddComponent<KitchenHotZone>();
        var so = new SerializedObject(zone);
        so.FindProperty("chefEncounter").objectReferenceValue = encounter;
        so.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }

    private GameObject CreateDoor(Vector2 position, Vector2 size, ChefController chefController)
    {
        var go = new GameObject("Door");
        go.transform.position = position;
        spawned.Add(go);

        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        col.isTrigger = true;

        var door = go.AddComponent<KitchenDoorTrigger>();
        var so = new SerializedObject(door);
        so.FindProperty("chefController").objectReferenceValue = chefController;
        so.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }

    private GameObject CreateGameStateManager()
    {
        var go = new GameObject("GameStateManager");
        spawned.Add(go);

        // Inactive while wiring up, same reasoning as CreateChef: disable the legacy-Input-based
        // debug state keys before Update ever runs. Pre-existing, unrelated bug — GameStateManager
        // reads UnityEngine.Input.GetKeyDown but this project's Active Input Handling is set to
        // the new Input System package, which throws on any legacy Input call; flagged separately
        // rather than fixed here (out of scope for the Chef warning system).
        go.SetActive(false);
        var manager = go.AddComponent<GameStateManager>();
        var so = new SerializedObject(manager);
        so.FindProperty("enableDebugStateKeys").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        go.SetActive(true);

        return go;
    }

    private static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            // WaitForFixedUpdate (not a plain null yield) so real Physics2D trigger contacts are
            // guaranteed to have a physics step to be detected on — same reasoning as
            // CitizenAIPlayModeTests' FixedSteps helper. ChefController itself ticks from
            // Update, which still runs at least once per rendered frame regardless.
            yield return new WaitForFixedUpdate();
        }
    }

    [UnityTest]
    public IEnumerator PlayerInHotZone_ChefBecomingPresent_TriggersRestart()
    {
        var data = MakeScheduleData(awayRange: Vector2.zero, leadTime: 0f); // returns almost immediately
        var chef = CreateChef(data);
        CreateHotZone(Vector2.zero, new Vector2(3f, 3f), chef.Encounter);
        CreatePlayer(Vector2.zero); // already overlapping the hot zone

        bool restarted = false;
        chef.Encounter.RestartTriggered += () => restarted = true;

        yield return Frames(10);

        Assert.IsTrue(restarted, "Being inside the hot zone when the Chef is/becomes Present must trigger a restart.");
        Assert.AreEqual(ChefPresenceState.Present, chef.ChefController.CurrentState);
    }

    [UnityTest]
    public IEnumerator PlayerNeverEntersHotZone_ChefBecomingPresent_DoesNotTriggerRestart()
    {
        var data = MakeScheduleData(awayRange: Vector2.zero, leadTime: 0f);
        var chef = CreateChef(data);
        CreateHotZone(new Vector2(50f, 50f), new Vector2(3f, 3f), chef.Encounter); // far away
        CreatePlayer(Vector2.zero);

        bool restarted = false;
        chef.Encounter.RestartTriggered += () => restarted = true;

        yield return Frames(10);

        Assert.AreEqual(ChefPresenceState.Present, chef.ChefController.CurrentState, "Sanity check: the Chef should still have returned.");
        Assert.IsFalse(restarted, "Never having entered the hot zone must never trigger a restart, even once the Chef is Present.");
    }

    [UnityTest]
    public IEnumerator PlayerReachesDoor_WhileChefAway_TransitionsToArena()
    {
        var data = MakeScheduleData(awayRange: new Vector2(1000f, 1000f), leadTime: 2f); // stays Away for the whole test
        var chef = CreateChef(data);
        CreateGameStateManager();
        CreateDoor(Vector2.zero, new Vector2(3f, 3f), chef.ChefController);
        CreatePlayer(Vector2.zero); // already overlapping the door

        yield return Frames(5);

        Assert.AreEqual(ChefPresenceState.Away, chef.ChefController.CurrentState, "Sanity check: the Chef should still be Away.");
        Assert.AreEqual(nameof(ArenaState), GameStateManager.Instance.CurrentStateName);
    }

    [UnityTest]
    public IEnumerator PlayerReachesDoor_WhileChefPresent_DoesNotTransitionToArena()
    {
        var data = MakeScheduleData(awayRange: Vector2.zero, leadTime: 0f); // returns almost immediately
        var chef = CreateChef(data);
        var stateManager = CreateGameStateManager();
        CreateDoor(Vector2.zero, new Vector2(3f, 3f), chef.ChefController);
        CreatePlayer(Vector2.zero);

        yield return Frames(10);

        Assert.AreEqual(ChefPresenceState.Present, chef.ChefController.CurrentState, "Sanity check: the Chef should have returned by now.");
        Assert.AreNotEqual(nameof(ArenaState), stateManager.GetComponent<GameStateManager>().CurrentStateName,
            "Reaching the door while the Chef is Present must never hand off to the Arena.");
    }
}
