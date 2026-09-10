/// <summary>
/// Player-local body state for the waffle monster's movement. This is intentionally
/// separate from the game-wide <c>GameStateManager</c> flow (Runner / KitchenApproach /
/// Arena) — it only describes what the player's body is doing right now.
/// </summary>
public enum PlayerMovementState
{
    /// <summary>Standing or running on solid ground.</summary>
    Grounded,

    /// <summary>In the air — covers both the rising "jumping" phase and falling.</summary>
    Airborne,

    /// <summary>Automatically climbing a <see cref="ClimbableSurface"/>; no input required.</summary>
    Climbing,
}
