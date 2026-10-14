using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

/// <summary>
/// Covers <see cref="GameStateManager"/>'s debug 1/2/3 state-key handling against a real,
/// simulated keyboard device (<see cref="InputTestFixture"/> — no physical hardware
/// involved). This is a regression guard for a real bug: HandleDebugStateKeys used to read
/// the legacy <c>UnityEngine.Input</c> class directly, which throws
/// <see cref="System.InvalidOperationException"/> the instant it's touched whenever this
/// project's Active Input Handling (Project Settings ▸ Player) is set to "Input System
/// Package (New)" only — which it is. These tests would have caught that immediately.
/// </summary>
public class GameStateManagerPlayModeTests : InputTestFixture
{
    private GameObject managerObject;
    private Keyboard keyboard;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
    }

    public override void TearDown()
    {
        if (managerObject != null)
        {
            Object.Destroy(managerObject);
        }
        base.TearDown();
    }

    private GameStateManager CreateManager()
    {
        managerObject = new GameObject("GameStateManager");
        return managerObject.AddComponent<GameStateManager>();
    }

    private IEnumerator PressAndRelease(Key key)
    {
        Press(keyboard[key]);
        yield return null;
        Release(keyboard[key]);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Update_WithDebugKeysEnabled_DoesNotThrow_UnderThisProjectsInputHandling()
    {
        CreateManager();

        // The bug this guards against threw on the very first Update() — no key press needed.
        yield return null;
        yield return null;

        Assert.Pass();
    }

    [UnityTest]
    public IEnumerator PressingDigit2_TransitionsToKitchenApproach()
    {
        var manager = CreateManager();
        yield return null; // let Start() run (TransitionToRunner)

        yield return PressAndRelease(Key.Digit2);

        Assert.AreEqual(nameof(KitchenApproachState), manager.CurrentStateName);
    }

    [UnityTest]
    public IEnumerator PressingDigit3_TransitionsToArena()
    {
        var manager = CreateManager();
        yield return null;

        yield return PressAndRelease(Key.Digit3);

        Assert.AreEqual(nameof(ArenaState), manager.CurrentStateName);
    }

    [UnityTest]
    public IEnumerator PressingDigit1_AfterAnotherState_TransitionsBackToRunner()
    {
        var manager = CreateManager();
        yield return null;
        yield return PressAndRelease(Key.Digit3);
        Assert.AreEqual(nameof(ArenaState), manager.CurrentStateName);

        yield return PressAndRelease(Key.Digit1);

        Assert.AreEqual(nameof(RunnerState), manager.CurrentStateName);
    }
}
