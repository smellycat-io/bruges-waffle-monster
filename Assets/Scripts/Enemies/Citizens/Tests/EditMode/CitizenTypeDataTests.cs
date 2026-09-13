using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CitizenTypeDataTests
{
    private CitizenTypeData data;

    [SetUp]
    public void SetUp()
    {
        data = ScriptableObject.CreateInstance<CitizenTypeData>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(data);
    }

    private void SetWalletRange(int min, int max)
    {
        var so = new SerializedObject(data);
        so.FindProperty("minWalletSize").intValue = min;
        so.FindProperty("maxWalletSize").intValue = max;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [Test]
    public void RollWalletSize_NeverBelowMin()
    {
        SetWalletRange(3, 4);

        for (int i = 0; i < 200; i++)
        {
            Assert.GreaterOrEqual(data.RollWalletSize(), 3);
        }
    }

    [Test]
    public void RollWalletSize_NeverAboveMax()
    {
        SetWalletRange(3, 4);

        for (int i = 0; i < 200; i++)
        {
            Assert.LessOrEqual(data.RollWalletSize(), 4);
        }
    }

    [Test]
    public void RollWalletSize_BothEndsOfTheRangeAreReachable()
    {
        // Guards the classic off-by-one on an inclusive upper bound: with a 1-2 range
        // rolled many times, both 1 and 2 should show up.
        SetWalletRange(1, 2);
        bool sawMin = false;
        bool sawMax = false;

        for (int i = 0; i < 200; i++)
        {
            int rolled = data.RollWalletSize();
            if (rolled == 1) sawMin = true;
            if (rolled == 2) sawMax = true;
        }

        Assert.IsTrue(sawMin, "The minimum (1) should be reachable.");
        Assert.IsTrue(sawMax, "The maximum (2) should be reachable — Random.Range's upper bound is exclusive, so RollWalletSize must pass max + 1.");
    }

    [Test]
    public void RollWalletSize_FixedRange_AlwaysReturnsThatValue()
    {
        SetWalletRange(5, 5);

        Assert.AreEqual(5, data.RollWalletSize());
    }
}
