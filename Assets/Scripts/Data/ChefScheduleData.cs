using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Balance data for the Chef's away/present/bedtime schedule and its three warning signals.
/// One instance per level (see Assets/ScriptableObjects/Enemies/). All fields marked
/// PLACEHOLDER below are first-pass numbers for feel-testing, not final balance — see
/// Docs/GameDesign.md.
///
/// Semantics decided while implementing (flagged rather than assumed silently):
/// <see cref="knownReturnWindows"/> is a list of (min, max) away-duration ranges, cycled in
/// order and looped, each away period's actual length randomly drawn from whichever range is
/// current — this is the "learnable rhythm" the player can anticipate. A random early return
/// (<see cref="randomReturnProbability"/>, rolled per second while safely Away) can cut that
/// short unpredictably. <see cref="bedtime"/> is an elapsed-level-time threshold, not a
/// duration: the first Away period that would otherwise start after that point is replaced by
/// one extended <see cref="bedtimeDuration"/>-long safe window, after which normal cycling
/// resumes (confirmed: does not stay safe permanently).
/// </summary>
[CreateAssetMenu(fileName = "ChefScheduleData", menuName = "Bruges Waffle Monster/Chef Schedule Data")]
public class ChefScheduleData : ScriptableObject
{
    [Header("Away/return schedule (PLACEHOLDER — needs feel-testing)")]
    [Tooltip("Cycled in order (looping): each entry is a (min, max) range in seconds an away period's actual length is randomly drawn from.")]
    [SerializeField] private List<Vector2> knownReturnWindows = new List<Vector2>();
    [Tooltip("Per-second chance, while safely Away, of an early return countdown starting regardless of the scheduled away duration — breaks the learnable pattern.")]
    [SerializeField, Range(0f, 1f)] private float randomReturnProbability;
    [Tooltip("Seconds before an actual return that the warning countdown (silhouette midpoint, audio ramp) becomes active.")]
    [SerializeField, Min(0f)] private float returnWarningLeadTime = 4f;
    [Tooltip("How long the Chef stays Present in the hot zone before leaving again. Not called out explicitly in the original design note, but required for the cycle to loop — flagged as an added placeholder.")]
    [SerializeField, Min(0f)] private float presentDuration = 8f;

    [Header("Bedtime (PLACEHOLDER — needs feel-testing)")]
    [Tooltip("Elapsed level-time (seconds) after which the next away period becomes the extended bedtime window, instead of a normal scheduled one.")]
    [SerializeField, Min(0f)] private float bedtime;
    [Tooltip("Length (seconds) of the extended, reliably-safe bedtime window. Added field — the original placeholder only had a bedtime start time, not a duration.")]
    [SerializeField, Min(0f)] private float bedtimeDuration = 30f;

    [Header("Security Monitor — blink (PLACEHOLDER — needs feel-testing)")]
    [Tooltip("X axis: 0-1 ramp progress (shared by real countdowns and fake-outs, indistinguishable by design). Y axis: blinks per second.")]
    [SerializeField] private AnimationCurve blinkRateCurve = AnimationCurve.Linear(0f, 0.5f, 1f, 6f);
    [Tooltip("Average fake-out ramps per minute while safely Away. Some fraction of blink ramps are fake, so the blink monitor alone is never fully trustworthy.")]
    [SerializeField, Min(0f)] private float fakeOutFrequency = 2f;
    [Tooltip("Seconds a fake-out ramp takes to rise to its peak before resetting. Added field.")]
    [SerializeField, Min(0f)] private float fakeOutRampDuration = 2f;
    [Tooltip("A fake-out ramps to a random peak within this 0-1 range (never guaranteed to look like a full/imminent real return) before resetting. Added field.")]
    [SerializeField] private Vector2 fakeOutPeakRange = new Vector2(0.3f, 0.9f);

    [Header("Arena entry (PLACEHOLDER — needs feel-testing)")]
    [Tooltip("Seconds added to the Chef's current/next return countdown the instant the Arena state is entered (Waffle Iron encounter begins) — breathing room without pausing the threat outright.")]
    [SerializeField, Min(0f)] private float arenaEntryCountdownExtension = 15f;

    public IReadOnlyList<Vector2> KnownReturnWindows => knownReturnWindows;
    public float RandomReturnProbability => randomReturnProbability;
    public float ReturnWarningLeadTime => returnWarningLeadTime;
    public float PresentDuration => presentDuration;
    public float Bedtime => bedtime;
    public float BedtimeDuration => bedtimeDuration;
    public AnimationCurve BlinkRateCurve => blinkRateCurve;
    public float FakeOutFrequency => fakeOutFrequency;
    public float FakeOutRampDuration => fakeOutRampDuration;
    public Vector2 FakeOutPeakRange => fakeOutPeakRange;
    public float ArenaEntryCountdownExtension => arenaEntryCountdownExtension;

    private void OnValidate()
    {
        if (fakeOutPeakRange.y < fakeOutPeakRange.x)
        {
            fakeOutPeakRange.y = fakeOutPeakRange.x;
        }

        fakeOutPeakRange.x = Mathf.Clamp01(fakeOutPeakRange.x);
        fakeOutPeakRange.y = Mathf.Clamp01(fakeOutPeakRange.y);
    }

    /// <summary>Builds the plain, Unity-Object-free config <see cref="ChefScheduleController"/> actually runs on.</summary>
    public ChefScheduleConfig BuildConfig()
    {
        return new ChefScheduleConfig(
            knownReturnWindows,
            randomReturnProbability,
            bedtime,
            bedtimeDuration,
            returnWarningLeadTime,
            presentDuration,
            fakeOutFrequency,
            fakeOutRampDuration,
            fakeOutPeakRange);
    }
}
