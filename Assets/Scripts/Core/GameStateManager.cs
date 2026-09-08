using UnityEngine;

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

    private void HandleDebugStateKeys()
    {
        if (!enableDebugStateKeys)
        {
            return;
        }

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
    }
}
