using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The Chef's instant, total level-restart consequence — deliberately separate from
/// <c>PlayerStrikeSystem</c>'s respawn-with-remainder flow (see that class's own doc comment
/// distinguishing the two; this does not touch it). Being inside <see cref="KitchenHotZone"/>
/// the instant the Chef is/becomes Present reloads the whole scene: no stash carryover, no
/// partial reset plumbing needed anywhere else — wallet, strikes, citizen state, and the
/// Chef's own schedule all come back fresh because everything does.
/// </summary>
public class ChefEncounter : MonoBehaviour
{
    [SerializeField, Tooltip("Auto-discovered if left unset.")] private ChefController chefController;
    [SerializeField, Tooltip("Disable in tests to observe a trigger via RestartTriggered without actually reloading the scene.")]
    private bool reloadSceneOnRestart = true;

    private bool playerInHotZone;
    private bool subscribed;

    /// <summary>Fired whenever a restart is triggered, before the scene reload (if any) happens.</summary>
    public event Action RestartTriggered;

    private void Awake()
    {
        if (chefController == null)
        {
            chefController = FindAnyObjectByType<ChefController>();
        }
    }

    private void OnEnable()
    {
        subscribed = chefController != null;
        if (subscribed)
        {
            chefController.EnsureInitialized(); // guarantees Schedule is non-null — see ChefController
            chefController.Schedule.ChefReturned += HandleChefReturned;
        }
    }

    private void OnDisable()
    {
        if (subscribed)
        {
            chefController.Schedule.ChefReturned -= HandleChefReturned;
        }
        subscribed = false;
    }

    /// <summary>Called by <see cref="KitchenHotZone"/> when the player enters. Restarts immediately if the Chef is already Present.</summary>
    public void NotifyPlayerEnteredHotZone()
    {
        playerInHotZone = true;
        if (chefController != null && chefController.IsPresent)
        {
            TriggerRestart();
        }
    }

    /// <summary>Called by <see cref="KitchenHotZone"/> when the player leaves.</summary>
    public void NotifyPlayerExitedHotZone() => playerInHotZone = false;

    private void HandleChefReturned()
    {
        if (playerInHotZone)
        {
            TriggerRestart();
        }
    }

    public void TriggerRestart()
    {
        RestartTriggered?.Invoke();

        if (reloadSceneOnRestart)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
