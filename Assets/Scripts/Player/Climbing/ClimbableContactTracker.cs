using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks the set of colliders the player is currently touching that belong to a
/// <see cref="ClimbableSurface"/>. Plain class (no MonoBehaviour) so "is a contact actually
/// climbable" and "which side is the wall on" can be unit-tested without a scene; the
/// owning MonoBehaviour forwards its collision/trigger enter/exit callbacks here.
/// </summary>
public sealed class ClimbableContactTracker
{
    private readonly HashSet<Collider2D> contacts = new HashSet<Collider2D>();

    /// <summary>True while at least one live climbable collider is being touched.</summary>
    public bool IsTouchingClimbable
    {
        get
        {
            PruneDestroyed();
            return contacts.Count > 0;
        }
    }

    /// <summary>Records a contact if <paramref name="other"/> (or a parent) carries a <see cref="ClimbableSurface"/>.</summary>
    public void RegisterContact(Collider2D other)
    {
        if (IsClimbable(other))
        {
            contacts.Add(other);
        }
    }

    /// <summary>Clears a contact previously recorded for <paramref name="other"/>.</summary>
    public void UnregisterContact(Collider2D other)
    {
        if (other != null)
        {
            contacts.Remove(other);
        }
    }

    /// <summary>Forgets every tracked contact (e.g. when the player is disabled).</summary>
    public void Clear() => contacts.Clear();

    /// <summary>
    /// Horizontal direction from <paramref name="fromPosition"/> to the nearest climbable
    /// contact: +1 = wall is to the right, -1 = wall is to the left, 0 = no contact.
    /// Used to push the player the opposite way on a wall-jump.
    /// </summary>
    public float GetWallDirection(Vector2 fromPosition)
    {
        PruneDestroyed();

        float nearestGap = float.PositiveInfinity;
        float direction = 0f;
        foreach (var collider in contacts)
        {
            float dx = collider.bounds.center.x - fromPosition.x;
            float gap = Mathf.Abs(dx);
            if (gap < nearestGap)
            {
                nearestGap = gap;
                direction = dx >= 0f ? 1f : -1f;
            }
        }
        return direction;
    }

    private void PruneDestroyed()
    {
        // Drop colliders destroyed while in contact so a stale entry can't strand the climb.
        contacts.RemoveWhere(collider => collider == null);
    }

    private static bool IsClimbable(Collider2D other)
    {
        return other != null && other.GetComponentInParent<ClimbableSurface>() != null;
    }
}
