using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plain, Unity-Object-free snapshot of a <see cref="ChefScheduleData"/> asset's values,
/// built once (see <see cref="ChefScheduleData.BuildConfig"/>) and handed to
/// <see cref="ChefScheduleController"/> — keeps the actual state machine testable with
/// hand-built values and no ScriptableObject/SerializedObject reflection, mirroring
/// <c>CitizenBehaviorController</c> taking plain sensor facts rather than a
/// <c>CitizenTypeData</c> reference.
/// </summary>
public sealed class ChefScheduleConfig
{
    public IReadOnlyList<Vector2> KnownAwayDurationRanges { get; }
    public float RandomEarlyReturnProbabilityPerSecond { get; }
    public float BedtimeStartTime { get; }
    public float BedtimeDuration { get; }
    public float ReturnWarningLeadTime { get; }
    public float PresentDuration { get; }
    public float FakeOutFrequencyPerMinute { get; }
    public float FakeOutRampDuration { get; }
    public Vector2 FakeOutPeakRange { get; }

    public ChefScheduleConfig(
        IReadOnlyList<Vector2> knownAwayDurationRanges,
        float randomEarlyReturnProbabilityPerSecond,
        float bedtimeStartTime,
        float bedtimeDuration,
        float returnWarningLeadTime,
        float presentDuration,
        float fakeOutFrequencyPerMinute,
        float fakeOutRampDuration,
        Vector2 fakeOutPeakRange)
    {
        KnownAwayDurationRanges = knownAwayDurationRanges;
        RandomEarlyReturnProbabilityPerSecond = randomEarlyReturnProbabilityPerSecond;
        BedtimeStartTime = bedtimeStartTime;
        BedtimeDuration = bedtimeDuration;
        ReturnWarningLeadTime = returnWarningLeadTime;
        PresentDuration = presentDuration;
        FakeOutFrequencyPerMinute = fakeOutFrequencyPerMinute;
        FakeOutRampDuration = fakeOutRampDuration;
        FakeOutPeakRange = fakeOutPeakRange;
    }

    /// <summary>
    /// A harmless placeholder schedule (no fake-outs, no bedtime, and — with no known-away-
    /// duration ranges to draw from — cycles on <c>ChefScheduleController</c>'s internal
    /// fallback duration only) used when a <see cref="ChefController"/> has no
    /// <see cref="ChefScheduleData"/> assigned, so <c>Schedule</c> is always safe to use
    /// rather than every caller needing a null-check.
    /// </summary>
    public static ChefScheduleConfig Fallback()
    {
        return new ChefScheduleConfig(
            new List<Vector2>(),
            randomEarlyReturnProbabilityPerSecond: 0f,
            bedtimeStartTime: float.MaxValue,
            bedtimeDuration: 0f,
            returnWarningLeadTime: 0f,
            presentDuration: 0f,
            fakeOutFrequencyPerMinute: 0f,
            fakeOutRampDuration: 1f,
            fakeOutPeakRange: Vector2.zero);
    }
}
