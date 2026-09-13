using System;
using UnityEngine;

/// <summary>
/// Handles the player getting caught by a citizen: the escalating strike penalty, the
/// respawn-in-place-but-keep-remaining-stash flow, and a brief post-respawn invincibility
/// window (with a flicker tell) so a citizen standing on the spawn point can't immediately
/// re-strike. A citizen's <c>CitizenAI</c> calls <see cref="RegisterStrike"/> on contact —
/// it never touches the player's <see cref="WaffleWallet"/> directly and knows nothing about
/// invincibility; all of that stays here, next to the respawn logic it protects.
///
/// Deliberately NOT part of <see cref="PlayerController"/> (a different responsibility —
/// movement vs. encounter consequences) and deliberately NOT the Chef's instant-loss system:
/// a strike here always keeps whatever remains in the stash after the loss and respawns the
/// player, it never fully resets the level. Do not conflate the two.
/// </summary>
[RequireComponent(typeof(WaffleWallet))]
public class PlayerStrikeSystem : MonoBehaviour
{
    [Header("Respawn invincibility (PLACEHOLDER — needs feel-testing)")]
    [SerializeField, Min(0f)]
    [Tooltip("How long the player is immune to citizen catches after respawning from a strike.")]
    private float invincibilityDuration = 1.5f;

    [Header("Invincibility visual tell")]
    [SerializeField, Tooltip("Auto-discovered from a child if left unset.")]
    private SpriteRenderer spriteRenderer;
    [SerializeField, Min(0.01f)]
    [Tooltip("Presentation only: how long each flicker on/off phase lasts while invincible.")]
    private float flickerInterval = 0.12f;

    private WaffleWallet wallet;
    private Rigidbody2D body; // optional: zeroed on respawn if present, so no fall/jump momentum carries over
    private Vector3 spawnPosition;
    private float invincibilityRemaining;
    private bool initialized;

    /// <summary>Strikes taken this level attempt. Only <see cref="ResetStrikes"/> (a full level restart) clears it — a respawn after a strike does not.</summary>
    public int CurrentStrikeCount { get; private set; }

    /// <summary>True for <see cref="invincibilityDuration"/> seconds after a respawn — catches are ignored entirely while this is true.</summary>
    public bool IsInvincible => invincibilityRemaining > 0f;

    /// <summary>Fired after a strike is applied and the player has respawned, with the new strike count.</summary>
    public event Action<int> Struck;

    /// <summary>
    /// Fired on a strike-3-or-later total wipe, after the waffle wallet has been drained.
    /// There is no topping/stash system yet (see Assets/Scripts/Toppings, Cooking) — this is
    /// the hook for it to also clear itself once it exists; don't invent that logic here.
    /// </summary>
    public event Action FullStashWiped;

    private void Awake() => EnsureInitialized();

    /// <summary>
    /// Captures the WaffleWallet/Rigidbody2D/SpriteRenderer references and the spawn position.
    /// Idempotent — only the first call does anything.
    ///
    /// Called automatically from Awake. Also called defensively from <see cref="RegisterStrike"/>
    /// and <see cref="Tick"/> because a synchronous EditMode [Test] never pumps a frame between
    /// AddComponent and the test body — there's no "next frame" for a deferred Awake to run on,
    /// so AddComponent alone does not reliably invoke Awake() in that context (confirmed against
    /// the real Test Runner; PlayMode tests don't need this, they run inside actual Play mode).
    /// </summary>
    public void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }
        initialized = true;

        wallet = GetComponent<WaffleWallet>();
        body = GetComponent<Rigidbody2D>();
        spawnPosition = transform.position;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void Update() => Tick(Time.deltaTime);

    /// <summary>
    /// Advances the invincibility countdown and its flicker tell by <paramref name="deltaTime"/>.
    /// Called every frame from Update with Time.deltaTime; exposed publicly so tests can drive
    /// it with synthetic time instead of waiting on real frames (EditMode has no frame loop).
    /// </summary>
    public void Tick(float deltaTime)
    {
        EnsureInitialized();

        if (invincibilityRemaining > 0f)
        {
            invincibilityRemaining = Mathf.Max(0f, invincibilityRemaining - deltaTime);
        }
        UpdateInvincibilityVisual();
    }

    /// <summary>
    /// Registers one catch and applies its penalty: strike 1 loses <paramref name="hitStrength"/>
    /// waffles, strike 2 loses <c>hitStrength * 2</c>, strike 3 (and any further catch this
    /// attempt) wipes the stash entirely. Always respawns the player afterward, keeping
    /// whatever remains. Entirely ignored while <see cref="IsInvincible"/> — no count, no
    /// loss, no respawn, as if the catch never happened.
    /// </summary>
    public void RegisterStrike(int hitStrength)
    {
        EnsureInitialized();

        if (IsInvincible)
        {
            return;
        }

        CurrentStrikeCount++;
        ApplyStrikeLoss(hitStrength);
        Struck?.Invoke(CurrentStrikeCount);
        Respawn();
    }

    /// <summary>Clears the strike count for a fresh level attempt. Not called by anything yet — wire this up alongside the Chef's instant-loss / level-restart once that system exists.</summary>
    public void ResetStrikes() => CurrentStrikeCount = 0;

    private void ApplyStrikeLoss(int hitStrength)
    {
        switch (CurrentStrikeCount)
        {
            case 1:
                wallet.RemoveWaffles(hitStrength);
                break;
            case 2:
                wallet.RemoveWaffles(hitStrength * 2);
                break;
            default: // 3rd catch this attempt, and any beyond it: total wipe.
                wallet.RemoveWaffles(wallet.CurrentWaffleCount);
                FullStashWiped?.Invoke();
                break;
        }
    }

    private void Respawn()
    {
        transform.position = spawnPosition;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
        invincibilityRemaining = invincibilityDuration;
    }

    private void UpdateInvincibilityVisual()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (!IsInvincible)
        {
            spriteRenderer.enabled = true; // never get stuck invisible once the window ends
            return;
        }

        int phase = Mathf.FloorToInt(invincibilityRemaining / flickerInterval);
        spriteRenderer.enabled = phase % 2 == 0;
    }
}
