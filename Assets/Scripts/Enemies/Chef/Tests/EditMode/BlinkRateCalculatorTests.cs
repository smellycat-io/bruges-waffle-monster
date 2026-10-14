using NUnit.Framework;

public class BlinkRateCalculatorTests
{
    private BlinkRateCalculator calculator;

    [SetUp]
    public void SetUp()
    {
        calculator = new BlinkRateCalculator();
    }

    [Test]
    public void StartsOn()
    {
        Assert.IsTrue(calculator.IsOn);
    }

    [Test]
    public void ZeroRate_StaysSteadyOn()
    {
        calculator.Tick(1f, 0f);
        calculator.Tick(1f, 0f);

        Assert.IsTrue(calculator.IsOn);
    }

    [Test]
    public void OneBlinkPerSecond_TogglesAtHalfSecondIntervals()
    {
        // 1 blink/sec -> half-cycle (on or off phase) = 0.5s.
        bool afterFirstHalf = calculator.Tick(0.5f, 1f);
        Assert.IsFalse(afterFirstHalf, "Should have toggled off after the first half-cycle.");

        bool afterSecondHalf = calculator.Tick(0.5f, 1f);
        Assert.IsTrue(afterSecondHalf, "Should have toggled back on after the second half-cycle.");
    }

    [Test]
    public void HigherRate_TogglesMoreOften()
    {
        int toggleCountAtLowRate = CountToggles(1f, totalTime: 2f, deltaTime: 0.05f);
        int toggleCountAtHighRate = CountToggles(8f, totalTime: 2f, deltaTime: 0.05f);

        Assert.Greater(toggleCountAtHighRate, toggleCountAtLowRate, "A faster blink rate should produce more on/off toggles over the same time span.");
    }

    private static int CountToggles(float blinksPerSecond, float totalTime, float deltaTime)
    {
        var calc = new BlinkRateCalculator();
        bool previous = calc.IsOn;
        int toggles = 0;

        for (float t = 0f; t < totalTime; t += deltaTime)
        {
            bool current = calc.Tick(deltaTime, blinksPerSecond);
            if (current != previous)
            {
                toggles++;
            }
            previous = current;
        }

        return toggles;
    }
}
