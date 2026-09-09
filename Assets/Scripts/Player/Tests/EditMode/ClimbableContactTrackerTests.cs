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
        // Note: no "?? " here — GetComponent can return a Unity fake-null that "?? " misses.
        var existing = go.GetComponent<Collider2D>();
        return existing != null ? existing : go.AddComponent<BoxCollider2D>();
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

    [Test]
    public void GetWallDirection_ReturnsZeroWhenNotTouchingAnything()
    {
        Assert.AreEqual(0f, tracker.GetWallDirection(Vector2.zero));
    }

    [Test]
    public void GetWallDirection_PositiveWhenSurfaceIsToTheRight()
    {
        climbableObject.transform.position = new Vector3(5f, 0f, 0f);
        tracker.RegisterContact(ColliderOn(climbableObject));

        Assert.AreEqual(1f, tracker.GetWallDirection(Vector2.zero));
    }

    [Test]
    public void GetWallDirection_NegativeWhenSurfaceIsToTheLeft()
    {
        climbableObject.transform.position = new Vector3(-5f, 0f, 0f);
        tracker.RegisterContact(ColliderOn(climbableObject));

        Assert.AreEqual(-1f, tracker.GetWallDirection(Vector2.zero));
    }

    [Test]
    public void GetWallDirection_UsesTheNearestClimbableContact()
    {
        climbableObject.transform.position = new Vector3(-8f, 0f, 0f);
        tracker.RegisterContact(ColliderOn(climbableObject));

        var nearWall = new GameObject("NearWall");
        nearWall.transform.position = new Vector3(2f, 0f, 0f);
        nearWall.AddComponent<ClimbableSurface>();
        tracker.RegisterContact(nearWall.AddComponent<BoxCollider2D>());

        Assert.AreEqual(1f, tracker.GetWallDirection(Vector2.zero), "Nearer wall (right, +2) should win over the far one (left, -8).");

        Object.DestroyImmediate(nearWall);
    }
}
