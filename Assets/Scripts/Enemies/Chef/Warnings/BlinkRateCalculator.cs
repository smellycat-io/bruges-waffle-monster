/// <summary>
/// Pure on/off blink timing from a blinks-per-second rate — a plain class with no MonoBehaviour
/// or Unity API, so <see cref="SecurityMonitorWarning"/>'s timing logic is unit-testable without
/// a running scene, same reasoning as <c>ChefScheduleController</c>/<c>CitizenBehaviorController</c>.
/// </summary>
public sealed class BlinkRateCalculator
{
    private float timer;

    /// <summary>Current on/off phase. Starts (and stays, while idle) on — a steady "safe" indicator rather than a dark one.</summary>
    public bool IsOn { get; private set; } = true;

    /// <summary>
    /// Advances the blink timer by <paramref name="deltaTime"/> at <paramref name="blinksPerSecond"/>
    /// and returns the resulting on/off state. A rate of 0 (or less) holds steady on with no blinking.
    /// </summary>
    public bool Tick(float deltaTime, float blinksPerSecond)
    {
        if (blinksPerSecond <= 0f)
        {
            IsOn = true;
            timer = 0f;
            return IsOn;
        }

        float halfCycle = 1f / (blinksPerSecond * 2f); // two toggles (on+off) per full blink
        timer += deltaTime;

        while (timer >= halfCycle)
        {
            timer -= halfCycle;
            IsOn = !IsOn;
        }

        return IsOn;
    }
}
