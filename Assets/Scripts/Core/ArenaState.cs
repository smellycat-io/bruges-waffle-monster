using UnityEngine;

/// <summary>
/// The Waffle Iron encounter. For now this only activates the placeholder Arena room (see
/// <see cref="ArenaRoomMarker"/>, <c>WaffleIronMarker</c> — an empty room + a positional marker,
/// no real fight mechanics yet — that's explicitly out of scope here) and extends the Chef's
/// return countdown so the fight has breathing room without removing the threat outright.
/// </summary>
public sealed class ArenaState : IGameState
{
    public void Enter()
    {
        var room = Object.FindAnyObjectByType<ArenaRoomMarker>(FindObjectsInactive.Include);
        if (room != null)
        {
            room.gameObject.SetActive(true);
        }

        var chef = Object.FindAnyObjectByType<ChefController>();
        if (chef != null && chef.ScheduleData != null)
        {
            chef.ExtendReturnCountdown(chef.ScheduleData.ArenaEntryCountdownExtension);
        }
    }

    public void Exit()
    {
        var room = Object.FindAnyObjectByType<ArenaRoomMarker>();
        if (room != null)
        {
            room.gameObject.SetActive(false);
        }
    }

    public void Tick()
    {
    }
}
