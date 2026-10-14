using UnityEngine;

/// <summary>
/// The 100%-reliable warning layer: shows a silhouette exactly at the midpoint of an ACTUAL
/// return countdown, via <see cref="ChefScheduleController.RealReturnMidpointReached"/> — a
/// signal that only ever fires for a real return, never a fake-out (guaranteed by
/// <c>ChefScheduleController</c> itself, see its own tests). This component's own job is just
/// showing/hiding a visual for <see cref="visibleDuration"/> once triggered; independently
/// testable from the other two warning layers per the brief.
/// </summary>
public class SilhouetteWarning : MonoBehaviour
{
    [SerializeField] private ChefController chefController;
    [SerializeField, Tooltip("Auto-discovered from a child if left unset.")] private SpriteRenderer silhouetteRenderer;
    [SerializeField, Min(0f), Tooltip("PLACEHOLDER — how long the silhouette stays visible once triggered.")]
    private float visibleDuration = 1.5f;

    private float remaining;
    private bool subscribed;

    private void Awake()
    {
        if (chefController == null)
        {
            chefController = FindAnyObjectByType<ChefController>();
        }

        if (silhouetteRenderer == null)
        {
            silhouetteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void OnEnable()
    {
        subscribed = chefController != null;
        if (subscribed)
        {
            chefController.EnsureInitialized(); // guarantees Schedule is non-null — see ChefController
            chefController.Schedule.RealReturnMidpointReached += HandleMidpointReached;
        }
    }

    private void OnDisable()
    {
        if (subscribed)
        {
            chefController.Schedule.RealReturnMidpointReached -= HandleMidpointReached;
        }
        subscribed = false;
    }

    private void HandleMidpointReached() => remaining = visibleDuration;

    private void Update() => Tick(Time.deltaTime);

    /// <summary>Exposed publicly so tests can drive it with synthetic time.</summary>
    public void Tick(float deltaTime)
    {
        if (remaining > 0f)
        {
            remaining = Mathf.Max(0f, remaining - deltaTime);
        }

        if (silhouetteRenderer != null)
        {
            silhouetteRenderer.enabled = remaining > 0f;
        }
    }

    /// <summary>Test hook: fires the same path <see cref="ChefScheduleController.RealReturnMidpointReached"/> would.</summary>
    public void TriggerForTest() => HandleMidpointReached();
}
