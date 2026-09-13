using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player-local movement for the waffle monster: run, jump, and automatic wall-climb,
/// driven by <see cref="PlayerMovementController"/> (a small Grounded / Airborne / Climbing
/// state machine).
///
/// This is deliberately NOT the game-wide <c>GameStateManager</c> (Runner /
/// KitchenApproach / Arena). That tracks where the player is in the level flow; this tracks
/// what the player's body is doing right now.
///
/// Responsibilities kept here (the "Unity glue"): reading input via
/// <see cref="IPlayerInputSource"/>, a feet ground-check, feeding climbable-contact
/// callbacks to <see cref="ClimbableContactTracker"/>, writing the resulting velocity and
/// gravity to the <see cref="Rigidbody2D"/>, and reporting completed falls (via
/// <see cref="FallTracker"/>) to <c>PlayerStrikeSystem</c>, if one is present, so fall damage
/// reuses the exact same strike escalation as a citizen catch. All decision logic lives in
/// the plain classes it composes.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerMovementConfig config;

    [SerializeField]
    [Tooltip("Any component implementing IPlayerInputSource (e.g. TapPlayerInput). Can also be set at runtime via SetInputSource.")]
    private MonoBehaviour inputSourceBehaviour;

    [Header("Ground check")]
    [SerializeField]
    [Tooltip("Empty child at the monster's feet. Falls back to this transform's origin if unset.")]
    private Transform groundCheck;
    [SerializeField, Min(0f)] private float groundCheckRadius = 0.15f;
    [SerializeField]
    [Tooltip("Which layers count as standable ground.")]
    private LayerMask groundLayers = ~0;

    private Rigidbody2D body;
    private IPlayerInputSource inputSource;
    private PlayerMovementController movement;
    private readonly ClimbableContactTracker climbable = new ClimbableContactTracker();
    private readonly FallTracker fallTracker = new FallTracker();

    // Optional: reports completed falls to it (see FixedUpdate). Discovered rather than a
    // required/serialized reference — movement shouldn't hard-depend on the strike system
    // existing (e.g. a movement-only test rig).
    private PlayerStrikeSystem strikeSystem;

    private ContactFilter2D groundFilter;
    private readonly List<Collider2D> groundHits = new List<Collider2D>();

    // This controller is entirely velocity-driven — FixedUpdate re-applies horizontal
    // velocity every step regardless of collisions (see below). With Unity's default
    // (non-zero) collider friction, holding a direction into ANY solid surface keeps
    // re-creating a contact force against it, and friction then uses that same contact to
    // resist the player's vertical sliding too — the player would hang frozen against a
    // wall mid-air instead of continuing to fall. Confirmed by direct physics testing.
    // Zero friction here restores normal free-fall on contact with non-climbable geometry;
    // climbing itself is driven entirely by explicit velocity, not friction, so this has no
    // effect on the climb feel.
    //
    // Lazily created (not a field initializer): Unity does not allow constructing a
    // PhysicsMaterial2D outside Awake/Start/an instance method — a static field initializer
    // runs as part of the type's static constructor, which Unity explicitly rejects
    // ("Create_Internal is not allowed to be called from a MonoBehaviour constructor or
    // instance field initializer"), throwing a TypeInitializationException that then poisons
    // every subsequent use of this type for the rest of the domain's lifetime.
    private static PhysicsMaterial2D noFrictionMaterial;

    /// <summary>Current body state. Reports <see cref="PlayerMovementState.Grounded"/> before wiring completes.</summary>
    public PlayerMovementState State => movement?.State ?? PlayerMovementState.Grounded;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        strikeSystem = GetComponent<PlayerStrikeSystem>();

        if (noFrictionMaterial == null)
        {
            noFrictionMaterial = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
        }

        foreach (Collider2D playerCollider in GetComponents<Collider2D>())
        {
            playerCollider.sharedMaterial = noFrictionMaterial;
        }

        groundFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        groundFilter.SetLayerMask(groundLayers);

        EnsureInputSource();
        EnsureMovement();
    }

    private void OnDisable() => climbable.Clear();

    private void Update()
    {
        if (movement != null && inputSource != null && inputSource.ConsumeJumpRequest())
        {
            movement.QueueJump();
        }
    }

    private void FixedUpdate()
    {
        if (movement == null)
        {
            return;
        }

        bool touchingClimbable = climbable.IsTouchingClimbable;
        float wallDirection = touchingClimbable ? climbable.GetWallDirection(transform.position) : 0f;
        movement.SetEnvironment(IsGrounded(), touchingClimbable, wallDirection);

        float horizontal = inputSource?.HorizontalAxis ?? 0f;
        float climb = inputSource?.ClimbAxis ?? 0f;
        body.linearVelocity = movement.Tick(body.linearVelocity, horizontal, climb);
        body.gravityScale = movement.GravityActive ? config.GravityScale : 0f;

        float? completedFall = fallTracker.Tick(transform.position.y, movement.State);
        if (completedFall.HasValue)
        {
            strikeSystem?.RegisterFall(completedFall.Value);
        }
    }

    /// <summary>Swap the movement config at runtime (also used by tests). Rebuilds the state machine.</summary>
    public void Configure(PlayerMovementConfig movementConfig)
    {
        config = movementConfig;
        movement = config != null ? new PlayerMovementController(config) : null;
    }

    /// <summary>Swap the input source at runtime (also used by tests). Pass null to freeze input.</summary>
    public void SetInputSource(IPlayerInputSource source) => inputSource = source;

    private void EnsureInputSource()
    {
        if (inputSource != null)
        {
            return;
        }

        inputSource = inputSourceBehaviour as IPlayerInputSource;
        if (inputSourceBehaviour != null && inputSource == null)
        {
            Debug.LogError($"{name}: '{inputSourceBehaviour.GetType().Name}' does not implement {nameof(IPlayerInputSource)}.", this);
        }
    }

    private void EnsureMovement()
    {
        if (movement != null)
        {
            return;
        }

        if (config == null)
        {
            Debug.LogError($"{name}: no {nameof(PlayerMovementConfig)} assigned — the player will not move.", this);
            return;
        }

        movement = new PlayerMovementController(config);
    }

    private bool IsGrounded()
    {
        // While rising through a jump the feet probe can still clip the take-off surface;
        // don't count that as standing on anything.
        if (body.linearVelocity.y > 0.01f)
        {
            return false;
        }

        Vector2 origin = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;
        int hitCount = Physics2D.OverlapCircle(origin, groundCheckRadius, groundFilter, groundHits);
        for (int i = 0; i < hitCount; i++)
        {
            // Ignore our own colliders so the feet probe never reports the player as its own ground.
            if (groundHits[i].attachedRigidbody != body)
            {
                return true;
            }
        }
        return false;
    }

    private void OnCollisionEnter2D(Collision2D collision) => climbable.RegisterContact(collision.collider);
    private void OnCollisionExit2D(Collision2D collision) => climbable.UnregisterContact(collision.collider);
    private void OnTriggerEnter2D(Collider2D other) => climbable.RegisterContact(other);
    private void OnTriggerExit2D(Collider2D other) => climbable.UnregisterContact(other);

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Transform probe = groundCheck != null ? groundCheck : transform;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(probe.position, groundCheckRadius);
    }
#endif
}
