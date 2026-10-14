using UnityEngine;

/// <summary>
/// Placed at the kitchen door, inside the hot zone. Reaching it while the Chef is away
/// (including mid return-warning, or Bedtime) hands off to <c>ArenaState</c> via
/// <c>GameStateManager</c> — the Waffle Iron encounter. While the Chef is Present, this does
/// nothing; <see cref="KitchenHotZone"/> already triggers an instant restart in that case
/// before the player could reasonably reach the door anyway, so this is a defensive no-op,
/// not the primary gate.
/// </summary>
[DisallowMultipleComponent]
public class KitchenDoorTrigger : MonoBehaviour
{
    [SerializeField, Tooltip("Auto-discovered if left unset.")] private ChefController chefController;

    private void Awake()
    {
        if (chefController == null)
        {
            chefController = FindAnyObjectByType<ChefController>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!PlayerColliderUtility.BelongsToPlayer(other))
        {
            return;
        }

        if (chefController != null && chefController.IsPresent)
        {
            return;
        }

        GameStateManager.Instance?.TransitionToArena();
    }
}
