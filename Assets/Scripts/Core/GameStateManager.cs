using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    public IGameState CurrentState { get; private set; }
    public string CurrentStateName => CurrentState?.GetType().Name;

    [SerializeField] private bool enableDebugStateKeys = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        TransitionToRunner();
    }

    private void Update()
    {
        HandleDebugStateKeys();
        CurrentState?.Tick();
    }

    public void TransitionTo(IGameState nextState)
    {
        CurrentState?.Exit();
        CurrentState = nextState;
        CurrentState.Enter();
    }

    public void TransitionToRunner()
    {
        TransitionTo(new RunnerState());
    }

    public void TransitionToKitchenApproach()
    {
        TransitionTo(new KitchenApproachState());
    }

    public void TransitionToArena()
    {
        TransitionTo(new ArenaState());
    }

    /// <summary>
    /// Debug-only 1/2/3 state transitions for manual testing. Reads whichever backend is
    /// actually active (Project Settings ▸ Player ▸ Active Input Handling) rather than the
    /// legacy <c>UnityEngine.Input</c> class directly — that class throws
    /// <see cref="System.InvalidOperationException"/> the instant it's touched when Active
    /// Input Handling is set to "Input System Package (New)" only, which this project uses
    /// (see <c>TapPlayerInput.KeyboardHorizontal</c>/<c>KeyboardClimb</c> for the same pattern
    /// already established elsewhere in this codebase).
    /// </summary>
    private void HandleDebugStateKeys()
    {
        if (!enableDebugStateKeys)
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            TransitionToRunner();
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            TransitionToKitchenApproach();
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            TransitionToArena();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            TransitionToRunner();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            TransitionToKitchenApproach();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            TransitionToArena();
        }
#endif
    }
}
