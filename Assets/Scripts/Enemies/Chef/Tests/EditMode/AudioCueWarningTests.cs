using NUnit.Framework;

/// <summary>Covers only the pure <see cref="AudioCueWarning.ComputeProximity"/> calculation — no GameObject/AudioSource needed.</summary>
public class AudioCueWarningTests
{
    [Test]
    public void NoActiveRealCountdown_ProximityIsZero()
    {
        Assert.AreEqual(0f, AudioCueWarning.ComputeProximity(false, remaining: 5f, duration: 10f));
    }

    [Test]
    public void CountdownJustStarted_ProximityIsNearZero()
    {
        float proximity = AudioCueWarning.ComputeProximity(true, remaining: 10f, duration: 10f);
        Assert.AreEqual(0f, proximity, 0.001f);
    }

    [Test]
    public void CountdownAboutToComplete_ProximityIsNearOne()
    {
        float proximity = AudioCueWarning.ComputeProximity(true, remaining: 0.01f, duration: 10f);
        Assert.Greater(proximity, 0.99f);
    }

    [Test]
    public void CountdownAtHalfway_ProximityIsAroundHalf()
    {
        float proximity = AudioCueWarning.ComputeProximity(true, remaining: 5f, duration: 10f);
        Assert.AreEqual(0.5f, proximity, 0.001f);
    }

    [Test]
    public void ZeroDuration_NeverThrows_ReturnsZero()
    {
        Assert.AreEqual(0f, AudioCueWarning.ComputeProximity(true, remaining: 0f, duration: 0f));
    }
}
