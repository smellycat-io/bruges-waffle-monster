/// <summary>Behavior state for a citizen enemy, driven by <see cref="CitizenBehaviorController"/>.</summary>
public enum CitizenChaseState
{
    /// <summary>Hasn't spotted the player (or lost sight of them). Not moving toward the player.</summary>
    Idle,

    /// <summary>Has line-of-sight on the player and is closing in.</summary>
    Chasing,

    /// <summary>Wallet drained by the player — sitting and crying. Permanent for the rest of the level.</summary>
    Disengaged,
}
