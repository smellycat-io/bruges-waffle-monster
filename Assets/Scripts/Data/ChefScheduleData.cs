using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ChefScheduleData", menuName = "Bruges Waffle Monster/Chef Schedule Data")]
public class ChefScheduleData : ScriptableObject
{
    [SerializeField] private List<Vector2> knownReturnWindows = new List<Vector2>();
    [SerializeField, Range(0f, 1f)] private float randomReturnProbability;
    [SerializeField, Min(0f)] private float bedtime;
    [SerializeField] private AnimationCurve blinkRateCurve = new AnimationCurve();
    [SerializeField, Min(0f)] private float fakeOutFrequency;

    public IReadOnlyList<Vector2> KnownReturnWindows => knownReturnWindows;
    public float RandomReturnProbability => randomReturnProbability;
    public float Bedtime => bedtime;
    public AnimationCurve BlinkRateCurve => blinkRateCurve;
    public float FakeOutFrequency => fakeOutFrequency;
}
