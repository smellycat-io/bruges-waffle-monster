using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Integration coverage for <see cref="PlayerController"/> against real Rigidbody2D physics
/// and real collider contacts: the state machine, the feet ground-check and the
/// climbable-only climb trigger all wired together.
/// </summary>
public class PlayerControllerPlayModeTests
{
    private sealed class StubInputSource : MonoBehaviour, IPlayerInputSource
    {
        public float horizontal;
        private bool jump;

        public float HorizontalAxis => horizontal;

        public void PressJump() => jump = true;

        public bool ConsumeJumpRequest()
        {
            if (!jump)
            {
                return false;
            }

            jump = false;
            return true;
        }
    }

    private readonly List<GameObject> spawned = new List<GameObject>();
    private PlayerMovementConfig config;

    [SetUp]
    public void SetUp()
    {
        config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        Physics2D.gravity = new Vector2(0f, -9.81f);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in spawned)
        {
            if (go != null)
            {
                Object.Destroy(go);
            }
        }
        spawned.Clear();
        Object.DestroyImmediate(config);
    }

    private PlayerController CreatePlayer(Vector2 position, out StubInputSource input)
    {
        var go = new GameObject("Player");
        go.SetActive(false);
        go.transform.position = position;
        spawned.Add(go);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;

        var feet = new GameObject("GroundCheck");
        feet.transform.SetParent(go.transform);
        feet.transform.localPosition = new Vector3(0f, -0.5f, 0f);

        input = go.AddComponent<StubInputSource>();

        var player = go.AddComponent<PlayerController>();
        player.Configure(config);
        player.SetInputSource(input);
        typeof(PlayerController)
            .GetField("groundCheck", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(player, feet.transform);

        go.SetActive(true);
        return player;
    }

    private GameObject CreateSurface(string name, Vector2 center, Vector2 size, bool climbable, bool asTrigger)
    {
        var go = new GameObject(name);
        go.transform.position = center;
        spawned.Add(go);

        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        col.isTrigger = asTrigger;

        if (climbable)
        {
            go.AddComponent<ClimbableSurface>();
        }

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
    public IEnumerator RestingOnPlainGround_IsGroundedAndNeverClimbs()
    {
        CreateSurface("Ground", new Vector2(0f, -1f), new Vector2(20f, 1f), climbable: false, asTrigger: false);
        var player = CreatePlayer(new Vector2(0f, 0.1f), out _);

        for (int i = 0; i < 60; i++)
        {
            Assert.AreNotEqual(PlayerMovementState.Climbing, player.State,
                "Contact with a plain (untagged) surface must never start a climb.");
            yield return new WaitForFixedUpdate();
        }

        Assert.AreEqual(PlayerMovementState.Grounded, player.State);
    }

    [UnityTest]
    public IEnumerator TouchingClimbableWall_EntersClimbAndMovesUp()
    {
        CreateSurface("ClimbWall", new Vector2(0.6f, 2f), new Vector2(0.6f, 10f), climbable: true, asTrigger: true);
        var player = CreatePlayer(new Vector2(0f, 0.1f), out _);

        yield return FixedSteps(10);
        Assert.AreEqual(PlayerMovementState.Climbing, player.State, "Contact with a ClimbableSurface should auto-enter the climb.");

        float startY = player.transform.position.y;
        yield return FixedSteps(30);

        Assert.AreEqual(PlayerMovementState.Climbing, player.State);
        Assert.Greater(player.transform.position.y, startY + 0.5f, "Climb should carry the player upward with no input.");
    }

    [UnityTest]
    public IEnumerator LeavingClimbableContact_ReturnsToNormalMovement()
    {
        var wall = CreateSurface("ClimbWall", new Vector2(0.6f, 2f), new Vector2(0.6f, 10f), climbable: true, asTrigger: true);
        var player = CreatePlayer(new Vector2(0f, 0.1f), out _);

        yield return FixedSteps(10);
        Assert.AreEqual(PlayerMovementState.Climbing, player.State);

        wall.SetActive(false); // simulate reaching the top / losing contact
        yield return FixedSteps(10);

        Assert.AreNotEqual(PlayerMovementState.Climbing, player.State, "Losing contact must hand control back to run/jump.");
    }

    [UnityTest]
    public IEnumerator JumpFromGround_GoesAirborneThenLandsGroundedAgain()
    {
        CreateSurface("Ground", new Vector2(0f, -1f), new Vector2(20f, 1f), climbable: false, asTrigger: false);
        var player = CreatePlayer(new Vector2(0f, 0.1f), out var input);

        yield return FixedSteps(40);
        Assert.AreEqual(PlayerMovementState.Grounded, player.State, "Player should settle on the ground first.");

        input.PressJump();
        yield return null;                     // PlayerController.Update consumes the jump request
        yield return new WaitForFixedUpdate();  // ...and FixedUpdate applies it

        Assert.AreEqual(PlayerMovementState.Airborne, player.State);
        Assert.Greater(player.GetComponent<Rigidbody2D>().linearVelocity.y, 0f, "Jump should give upward velocity.");

        bool landed = false;
        for (int i = 0; i < 200 && !landed; i++)
        {
            yield return new WaitForFixedUpdate();
            landed = player.State == PlayerMovementState.Grounded;
        }

        Assert.IsTrue(landed, "Player should land and return to Grounded after the jump arc.");
    }
}
