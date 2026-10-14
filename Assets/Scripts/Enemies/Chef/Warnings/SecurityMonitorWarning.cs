using UnityEngine;

/// <summary>
/// The unreliable warning layer: a blink-rate indicator that speeds up as
/// <see cref="ChefController.BlinkRampProgress01"/> ramps toward 1, using the same curve/shape
/// whether that ramp is a real return countdown or a fake-out — this component has no way to
/// tell which, and never should (see <see cref="SilhouetteWarning"/> for the trustworthy
/// signal). Reads <see cref="ChefController"/> only; independently testable from the other two
/// warning layers per the brief.
/// </summary>
public class SecurityMonitorWarning : MonoBehaviour
{
    [SerializeField] private ChefController chefController;
    [SerializeField, Tooltip("Auto-discovered from a child if left unset.")] private SpriteRenderer indicatorRenderer;
    [SerializeField] private Color idleColor = Color.green;
    [SerializeField] private Color blinkColor = Color.red;

    private readonly BlinkRateCalculator blink = new BlinkRateCalculator();

    private void Awake()
    {
        if (chefController == null)
        {
            chefController = FindAnyObjectByType<ChefController>();
        }

        if (indicatorRenderer == null)
        {
            indicatorRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void Update() => Tick(Time.deltaTime);

    /// <summary>Exposed publicly so tests can drive it with synthetic time, same as PlayerStrikeSystem.Tick.</summary>
    public void Tick(float deltaTime)
    {
        if (chefController == null)
        {
            return;
        }

        float blinksPerSecond = chefController.ScheduleData != null
            ? chefController.ScheduleData.BlinkRateCurve.Evaluate(chefController.BlinkRampProgress01)
            : 0f;

        bool isOn = blink.Tick(deltaTime, blinksPerSecond);

        if (indicatorRenderer != null)
        {
            indicatorRenderer.color = isOn ? blinkColor : idleColor;
        }
    }
}
