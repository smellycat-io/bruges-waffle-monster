using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks the set of <see cref="ClimbableSurface"/> colliders the player is currently
/// touching. Plain class (no MonoBehaviour) so "is a contact actually climbable" can be
/// unit-tested without a scene; the owning MonoBehaviour forwards its collision/trigger
/// enter/exit callbacks here.
/// </summary>
public sealed class ClimbableContactTracker
{
    private readonly HashSet<ClimbableSurface> contacts = new HashSet<ClimbableSurface>();

    /// <summary>True while at least one live climbable surface is being touched.</summary>
    public bool IsTouchingClimbable
    {
        get
        {
            // Drop any surfaces destroyed while in contact so a stale entry can't strand the climb.
            contacts.RemoveWhere(surface => surface == null);
            return contacts.Count > 0;
        }
    }

    /// <summary>Records a contact if <paramref name="other"/> (or a parent) carries a <see cref="ClimbableSurface"/>.</summary>
    public void RegisterContact(Component other)
    {
        var surface = ResolveSurface(other);
        if (surface != null)
        {
            contacts.Add(surface);
        }
    }

    /// <summary>Clears a contact previously recorded for <paramref name="other"/>.</summary>
    public void UnregisterContact(Component other)
    {
        var surface = ResolveSurface(other);
        if (surface != null)
        {
            contacts.Remove(surface);
        }
    }

    /// <summary>Forgets every tracked contact (e.g. when the player is disabled).</summary>
    public void Clear() => contacts.Clear();

    private static ClimbableSurface ResolveSurface(Component other)
    {
        return other == null ? null : other.GetComponentInParent<ClimbableSurface>();
    }
}
