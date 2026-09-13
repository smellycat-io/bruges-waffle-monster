using UnityEngine;

/// <summary>
/// The one shared behavior component for every citizen enemy type (Tourist / Vendor /
/// Guard, ...). Per-type stats come entirely from the <see cref="CitizenTypeData"/> asset
/// assigned in the Inspector — a new citizen type is a new asset, never a new script.
///
/// Responsibilities kept here (the "Unity glue"): line-of-sight raycasting, chase movement,
/// catching the player (forwards to <see cref="PlayerStrikeSystem"/> rather than touching
/// the player's wallet directly), and driving its own <see cref="WaffleWallet"/> +
/// placeholder-tint visuals. The Idle/Chasing/Disengaged decision logic lives in
/// <see cref="CitizenBehaviorController"/>, mirroring the player's
/// PlayerMovementController/PlayerController split.
///
/// Climbing is opt-in per type via <see cref="CitizenTypeData.CanClimb"/> (only the Guard has
/// it, currently) rather than a type check in code — reuses the same
/// <see cref="ClimbableSurface"/> marker and <see cref="ClimbableContactTracker"/> the player
/// climbs with, just driven as a simple vertical follow instead of player input.
///
/// Expects a trigger <c>Collider2D</c> for catching the player (not required via
/// [RequireComponent] since Collider2D is abstract — see PlayerController for the same note).
/// </summary>
[RequireComponent(typeof(WaffleWallet))]
[RequireComponent(typeof(Rigidbody2D))]
public class CitizenAI : MonoBehaviour
{
    [SerializeField] private CitizenTypeData typeData;

    [Header("Perception")]
    [SerializeField]
    [Tooltip("Colliders considered sight-blocking obstacles. This citizen's own layer is always excluded automatically, so the default (Everything) is usually fine.")]
    private LayerMask sightMask = ~0;

    [Header("Placeholder visuals")]
    [SerializeField, Tooltip("Auto-discovered from a child if left unset.")]
    private SpriteRenderer spriteRenderer;
    [SerializeField, Range(0f, 1f)]
    [Tooltip("Presentation only: how much the placeholder color darkens once disengaged (sitting and crying).")]
    private float disengagedColorMultiplier = 0.5f;

    private Rigidbody2D body;
    private WaffleWallet wallet;
    private Transform target;
    private PlayerStrikeSystem targetStrikeSystem;
    private readonly CitizenBehaviorController behavior = new CitizenBehaviorController();
    private readonly ClimbableContactTracker climbable = new ClimbableContactTracker();
    private bool isWalletDrained;

    /// <summary>Current behavior state. Reports Idle before wiring completes.</summary>
    public CitizenChaseState State => behavior.State;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        // Kinematic: this citizen is always moved explicitly via MovePosition, never by
        // physics forces/gravity — a dynamic body would fight the chase movement.
        body.bodyType = RigidbodyType2D.Kinematic;

        wallet = GetComponent<WaffleWallet>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (typeData == null)
        {
            Debug.LogError($"{name}: no {nameof(CitizenTypeData)} assigned — this citizen will not act.", this);
            return;
        }

        int rolledWalletSize = typeData.RollWalletSize();
        wallet.Initialize(rolledWalletSize, rolledWalletSize);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = typeData.PlaceholderColor;
        }

        sightMask &= ~(1 << gameObject.layer); // never treat our own collider as an obstacle

        if (target == null)
        {
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                SetTarget(player.transform);
            }
        }
    }

    private void OnEnable() => wallet.Drained += HandleWalletDrained;

    private void OnDisable()
    {
        wallet.Drained -= HandleWalletDrained;
        climbable.Clear();
    }

    private void HandleWalletDrained() => isWalletDrained = true;

    /// <summary>
    /// Assigns the Transform this citizen looks for and chases. Auto-discovered via
    /// <see cref="PlayerController"/> in Awake if never called; exposed publicly so tests
    /// (and any future non-PlayerController target) can set it directly.
    /// </summary>
    public void SetTarget(Transform playerTransform)
    {
        target = playerTransform;
        targetStrikeSystem = target != null ? target.GetComponent<PlayerStrikeSystem>() : null;
    }

    private void FixedUpdate()
    {
        if (typeData == null)
        {
            return;
        }

        behavior.Tick(HasLineOfSightOnTarget(), isWalletDrained);
        ApplyState();
    }

    private void ApplyState()
    {
        switch (behavior.State)
        {
            case CitizenChaseState.Chasing:
                ChaseTarget();
                break;
            case CitizenChaseState.Disengaged:
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = typeData.PlaceholderColor * disengagedColorMultiplier;
                }
                break;
        }
    }

    private void ChaseTarget()
    {
        if (target == null)
        {
            return;
        }

        if (typeData.CanClimb && climbable.IsTouchingClimbable)
        {
            ClimbTowardTarget();
            return;
        }

        // Grounded pursuit: horizontal only, so climbing stays a real escape route for
        // citizen types that can't follow (CanClimb false). If citizens should instead close
        // in on the player's exact position outright, this is the one line to change.
        float direction = Mathf.Sign(target.position.x - transform.position.x);
        Vector2 nextPosition = body.position + new Vector2(direction * typeData.MoveSpeed * Time.fixedDeltaTime, 0f);
        body.MovePosition(nextPosition);
    }

    /// <summary>Simple vertical follow while on a ClimbableSurface — no drag input, no wall-jump, just close the height gap at MoveSpeed.</summary>
    private void ClimbTowardTarget()
    {
        float direction = Mathf.Sign(target.position.y - transform.position.y);
        Vector2 nextPosition = body.position + new Vector2(0f, direction * typeData.MoveSpeed * Time.fixedDeltaTime);
        body.MovePosition(nextPosition);
    }

    private bool HasLineOfSightOnTarget()
    {
        if (target == null)
        {
            return false;
        }

        Vector2 origin = transform.position;
        Vector2 toTarget = (Vector2)target.position - origin;
        float distance = toTarget.magnitude;

        if (distance <= 0f || distance > typeData.DetectionRange)
        {
            return false;
        }

        RaycastHit2D hit = Physics2D.Raycast(origin, toTarget / distance, distance, sightMask);
        // Nothing in the way before reaching the target, or the first thing the ray reaches
        // IS the target -> clear line of sight. Anything else first -> blocked.
        return hit.collider == null || hit.transform == target || hit.transform.IsChildOf(target);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        climbable.RegisterContact(other);
        TryCatchTarget(other);
    }

    private void OnTriggerExit2D(Collider2D other) => climbable.UnregisterContact(other);

    private void TryCatchTarget(Collider2D other)
    {
        if (behavior.State != CitizenChaseState.Chasing || target == null)
        {
            return;
        }

        if (other.transform != target && !other.transform.IsChildOf(target))
        {
            return;
        }

        if (targetStrikeSystem == null)
        {
            Debug.LogError($"{name}: caught the player but no {nameof(PlayerStrikeSystem)} was found on them.", this);
            return;
        }

        targetStrikeSystem.RegisterStrike(typeData.HitStrength);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (typeData == null)
        {
            return;
        }

        Gizmos.color = behavior.State == CitizenChaseState.Chasing ? Color.red : new Color(1f, 1f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, typeData.DetectionRange);
    }
#endif
}
