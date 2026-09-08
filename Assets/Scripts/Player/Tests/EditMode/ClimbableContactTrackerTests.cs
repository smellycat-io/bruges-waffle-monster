using NUnit.Framework;
using UnityEngine;

public class ClimbableContactTrackerTests
{
    private ClimbableContactTracker tracker;
    private GameObject climbableObject;
    private GameObject plainObject;

    [SetUp]
    public void SetUp()
    {
        tracker = new ClimbableContactTracker();

        climbableObject = new GameObject("ClimbableWall");
        climbableObject.AddComponent<ClimbableSurface>();

        plainObject = new GameObject("PlainWall");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(climbableObject);
        Object.DestroyImmediate(plainObject);
    }

    /// <summary>Stand-in for the collider Unity hands to OnCollision/OnTrigger callbacks.</summary>
    private static Collider2D ColliderOn(GameObject go)
    {
        return go.GetComponent<Collider2D>() ?? go.AddComponent<BoxCollider2D>();
    }

    [Test]
    public void NoContacts_IsNotTouching()
    {
        Assert.IsFalse(tracker.IsTouchingClimbable);
    }

    [Test]
    public void RegisterContact_WithClimbableSurface_IsTouching()
    {
        tracker.RegisterContact(ColliderOn(climbableObject));

        Assert.IsTrue(tracker.IsTouchingClimbable);
    }

    [Test]
    public void RegisterContact_WithArbitrarySurface_IsNotTouching()
    {
        tracker.RegisterContact(ColliderOn(plainObject));

        Assert.IsFalse(tracker.IsTouchingClimbable, "Only colliders tagged with ClimbableSurface may trigger a climb.");
    }

    [Test]
    public void RegisterContact_OnChildCollider_ResolvesParentClimbableSurface()
    {
        var child = new GameObject("ClimbableCollider");
        child.transform.SetParent(climbableObject.transform);
        var childCollider = child.AddComponent<BoxCollider2D>();

        tracker.RegisterContact(childCollider);

        Assert.IsTrue(tracker.IsTouchingClimbable);
    }

    [Test]
    public void UnregisterContact_ClearsThatContact()
    {
        var collider = ColliderOn(climbableObject);
        tracker.RegisterContact(collider);

        tracker.UnregisterContact(collider);

        Assert.IsFalse(tracker.IsTouchingClimbable);
    }

    [Test]
    public void StaysTouchingUntilAllClimbableContactsRemoved()
    {
        var secondWall = new GameObject("ClimbableWall2");
        secondWall.AddComponent<ClimbableSurface>();
        var secondCollider = secondWall.AddComponent<BoxCollider2D>();

        tracker.RegisterContact(ColliderOn(climbableObject));
        tracker.RegisterContact(secondCollider);

        tracker.UnregisterContact(ColliderOn(climbableObject));
        Assert.IsTrue(tracker.IsTouchingClimbable, "Still touching the second wall.");

        tracker.UnregisterContact(secondCollider);
        Assert.IsFalse(tracker.IsTouchingClimbable);

        Object.DestroyImmediate(secondWall);
    }

    [Test]
    public void Clear_RemovesAllContacts()
    {
        tracker.RegisterContact(ColliderOn(climbableObject));

        tracker.Clear();

        Assert.IsFalse(tracker.IsTouchingClimbable);
    }

    [Test]
    public void DestroyedSurfaceInContact_NoLongerCountsAsTouching()
    {
        tracker.RegisterContact(ColliderOn(climbableObject));
        Assert.IsTrue(tracker.IsTouchingClimbable);

        Object.DestroyImmediate(climbableObject);
        climbableObject = null;

        Assert.IsFalse(tracker.IsTouchingClimbable, "A destroyed surface must not strand the player in climb.");
    }
}
