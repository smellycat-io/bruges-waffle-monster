using UnityEngine;

/// <summary>
/// Marker + trigger for the area the Chef patrols. Mirrors <c>ClimbableSurface</c>'s role as a
/// placed marker on level geometry, layered with actual trigger detection — its only job is
/// reporting player enter/exit to <see cref="ChefEncounter"/>, which alone decides the
/// consequence (this component never restarts anything itself).
/// </summary>
[DisallowMultipleComponent]
public class KitchenHotZone : MonoBehaviour
{
    [SerializeField, Tooltip("Auto-discovered if left unset.")] private ChefEncounter chefEncounter;

    private void Awake()
    {
        if (chefEncounter == null)
        {
            chefEncounter = FindAnyObjectByType<ChefEncounter>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (PlayerColliderUtility.BelongsToPlayer(other))
        {
            chefEncounter?.NotifyPlayerEnteredHotZone();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (PlayerColliderUtility.BelongsToPlayer(other))
        {
            chefEncounter?.NotifyPlayerExitedHotZone();
        }
    }
}
