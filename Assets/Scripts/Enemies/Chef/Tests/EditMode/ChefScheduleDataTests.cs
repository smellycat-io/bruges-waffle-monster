using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ChefScheduleDataTests
{
    private ChefScheduleData data;

    [SetUp]
    public void SetUp()
    {
        data = ScriptableObject.CreateInstance<ChefScheduleData>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(data);
    }

    [Test]
    public void BuildConfig_MapsEveryFieldThrough()
    {
        var so = new SerializedObject(data);
        so.FindProperty("knownReturnWindows").arraySize = 1;
        so.FindProperty("knownReturnWindows").GetArrayElementAtIndex(0).vector2Value = new Vector2(3f, 7f);
        so.FindProperty("randomReturnProbability").floatValue = 0.25f;
        so.FindProperty("returnWarningLeadTime").floatValue = 4f;
        so.FindProperty("presentDuration").floatValue = 9f;
        so.FindProperty("bedtime").floatValue = 120f;
        so.FindProperty("bedtimeDuration").floatValue = 45f;
        so.FindProperty("fakeOutFrequency").floatValue = 3f;
        so.FindProperty("fakeOutRampDuration").floatValue = 1.5f;
        so.FindProperty("fakeOutPeakRange").vector2Value = new Vector2(0.2f, 0.8f);
        so.ApplyModifiedPropertiesWithoutUndo();

        ChefScheduleConfig config = data.BuildConfig();

        Assert.AreEqual(1, config.KnownAwayDurationRanges.Count);
        Assert.AreEqual(new Vector2(3f, 7f), config.KnownAwayDurationRanges[0]);
        Assert.AreEqual(0.25f, config.RandomEarlyReturnProbabilityPerSecond);
        Assert.AreEqual(4f, config.ReturnWarningLeadTime);
        Assert.AreEqual(9f, config.PresentDuration);
        Assert.AreEqual(120f, config.BedtimeStartTime);
        Assert.AreEqual(45f, config.BedtimeDuration);
        Assert.AreEqual(3f, config.FakeOutFrequencyPerMinute);
        Assert.AreEqual(1.5f, config.FakeOutRampDuration);
        Assert.AreEqual(new Vector2(0.2f, 0.8f), config.FakeOutPeakRange);
    }

    [Test]
    public void OnValidate_ClampsFakeOutPeakRange_ToZeroOneAndNonDescending()
    {
        var so = new SerializedObject(data);
        so.FindProperty("fakeOutPeakRange").vector2Value = new Vector2(1.5f, -0.5f);
        so.ApplyModifiedPropertiesWithoutUndo();

        // Invoked directly via reflection rather than relying on ApplyModifiedProperties to
        // trigger OnValidate — that timing isn't guaranteed inside a synchronous EditMode test.
        MethodInfo onValidate = typeof(ChefScheduleData).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
        onValidate.Invoke(data, null);

        Assert.LessOrEqual(data.FakeOutPeakRange.x, data.FakeOutPeakRange.y, "OnValidate should never leave the range inverted.");
        Assert.GreaterOrEqual(data.FakeOutPeakRange.x, 0f);
        Assert.LessOrEqual(data.FakeOutPeakRange.y, 1f);
    }
}
