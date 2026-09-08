using NUnit.Framework;
using UnityEngine;

public class WaffleWalletTests
{
    private GameObject walletObject;
    private WaffleWallet wallet;

    [SetUp]
    public void SetUp()
    {
        walletObject = new GameObject("WaffleWalletTestObject");
        wallet = walletObject.AddComponent<WaffleWallet>();
        wallet.Initialize(10, 10);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(walletObject);
    }

    [Test]
    public void RemoveWaffles_RemovesHitStrengthFromWallet()
    {
        int wafflesRemoved = wallet.RemoveWaffles(3);

        Assert.AreEqual(3, wafflesRemoved);
        Assert.AreEqual(7, wallet.CurrentWaffleCount);
        Assert.IsFalse(wallet.IsDrained);
    }

    [Test]
    public void RemoveWaffles_WhenCountReachesZero_TriggersDrained()
    {
        int drainedEventCount = 0;
        wallet.Drained += () => drainedEventCount++;

        int wafflesRemoved = wallet.RemoveWaffles(10);

        Assert.AreEqual(10, wafflesRemoved);
        Assert.AreEqual(0, wallet.CurrentWaffleCount);
        Assert.IsTrue(wallet.IsDrained);
        Assert.AreEqual(1, drainedEventCount);
    }
}
