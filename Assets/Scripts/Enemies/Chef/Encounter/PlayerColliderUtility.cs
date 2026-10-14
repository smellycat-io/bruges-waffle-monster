using UnityEngine;

/// <summary>
/// Shared "is this collider the player" check for the kitchen encounter triggers
/// (<see cref="KitchenHotZone"/>, <see cref="KitchenDoorTrigger"/>) — extracted so the same
/// identity logic isn't duplicated between them.
/// </summary>
public static class PlayerColliderUtility
{
    public static bool BelongsToPlayer(Collider2D collider)
        => collider.GetComponent<PlayerController>() != null || collider.GetComponentInParent<PlayerController>() != null;
}
