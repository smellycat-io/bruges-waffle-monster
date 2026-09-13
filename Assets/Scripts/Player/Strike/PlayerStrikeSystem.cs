using System;
using UnityEngine;

/// <summary>
/// Handles the player getting caught by a citizen: the escalating strike penalty and the
/// respawn-in-place-but-keep-remaining-stash flow. A citizen's <c>CitizenAI</c> calls
/// <see cref="RegisterStrike"/> on contact — it never touches the player's
/// <see cref="WaffleWallet"/> directly, keeping "you got caught" (here) separate from
/// "how hard did they hit you" (the citizen) and from "how much does a wallet lose" (WaffleWallet).
///
/// Deliberately NOT part of <see cref="PlayerController"/> (a different responsibility —
/// movement vs. encounter consequences) and deliberately NOT the Chef's instant-loss system:
/// a strike here always keeps whatever remains in the stash after the loss and respawns the
/// player, it never fully resets the level. Do not conflate the two.
/// </summary>
[RequireComponent(typeof(WaffleWallet))]
public class PlayerStrikeSystem : MonoBehaviour
{
    private WaffleWallet wallet;
    private Rigidbody2D body; // optional: zeroed on respawn if present, so no fall/jump momentum carries over
    private Vector3 spawnPosition;

    /// <summary>Strikes taken this level attempt. Only <see cref="ResetStrikes"/> (a full level restart) clears it — a respawn after a strike does not.</summary>
    public int CurrentStrikeCount { get; private set; }

    /// <summary>Fired after a strike is applied and the player has respawned, with the new strike count.</summary>
    public event Action<int> Struck;

    /// <summary>
    /// Fired on a strike-3-or-later total wipe, after the waffle wallet has been drained.
    /// There is no topping/stash system yet (see Assets/Scripts/Toppings, Cooking) — this is
    /// the hook for it to also clear itself once it exists; don't invent that logic here.
    /// </summary>
    public event Action FullStashWiped;

    private void Awake()
    {
        wallet = GetComponent<WaffleWallet>();
        body = GetComponent<Rigidbody2D>();
        spawnPosition = transform.position;
    }

    /// <summary>
    /// Registers one catch and applies its penalty: strike 1 loses <paramref name="hitStrength"/>
    /// waffles, strike 2 loses <c>hitStrength * 2</c>, strike 3 (and any further catch this
    /// attempt) wipes the stash entirely. Always respawns the player afterward, keeping
    /// whatever remains.
    /// </summary>
    public void RegisterStrike(int hitStrength)
    {
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
    }
}
