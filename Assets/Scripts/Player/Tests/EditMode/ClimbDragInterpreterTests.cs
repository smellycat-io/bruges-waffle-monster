using NUnit.Framework;

public class ClimbDragInterpreterTests
{
    private const float Deadzone = 10f;

    private ClimbDragInterpreter interpreter;

    [SetUp]
    public void SetUp()
    {
        interpreter = new ClimbDragInterpreter(Deadzone);
    }

    [Test]
    public void NoTouch_AxisIsZero()
    {
        Assert.AreEqual(0f, interpreter.ClimbAxis);
    }

    [Test]
    public void TouchDownWithoutMoving_AxisStaysZero()
    {
        interpreter.PointerDown(1, screenY: 400f);

        Assert.AreEqual(0f, interpreter.ClimbAxis, "Touching the wall alone must not move the player.");
    }

    [Test]
    public void DragUpPastDeadzone_AxisIsPositive()
    {
        interpreter.PointerDown(1, screenY: 400f);
        interpreter.PointerMoved(1, screenY: 460f);

        Assert.AreEqual(1f, interpreter.ClimbAxis);
    }

    [Test]
    public void DragDownPastDeadzone_AxisIsNegative()
    {
        interpreter.PointerDown(1, screenY: 400f);
        interpreter.PointerMoved(1, screenY: 340f);

        Assert.AreEqual(-1f, interpreter.ClimbAxis);
    }

    [Test]
    public void MovementWithinDeadzone_AxisStaysZero()
    {
        interpreter.PointerDown(1, screenY: 400f);
        interpreter.PointerMoved(1, screenY: 405f); // < 10px

        Assert.AreEqual(0f, interpreter.ClimbAxis);
    }

    [Test]
    public void HoldingAboveTheAnchor_KeepsAscending()
    {
        interpreter.PointerDown(1, screenY: 400f);
        interpreter.PointerMoved(1, screenY: 500f);
        interpreter.PointerMoved(1, screenY: 500f); // finger held still, still above anchor

        Assert.AreEqual(1f, interpreter.ClimbAxis, "Axis follows displacement from the anchor, not frame delta.");
    }

    [Test]
    public void DraggingBackToTheAnchor_ReturnsToHang()
    {
        interpreter.PointerDown(1, screenY: 400f);
        interpreter.PointerMoved(1, screenY: 500f);
        Assert.AreEqual(1f, interpreter.ClimbAxis);

        interpreter.PointerMoved(1, screenY: 402f); // back within the deadzone of the anchor

        Assert.AreEqual(0f, interpreter.ClimbAxis);
    }

    [Test]
    public void Release_DropsAxisToZero_NoResidual()
    {
        interpreter.PointerDown(1, screenY: 400f);
        interpreter.PointerMoved(1, screenY: 500f);
        interpreter.PointerUp(1);

        Assert.AreEqual(0f, interpreter.ClimbAxis, "Hold-to-move: releasing stops immediately.");
    }

    [Test]
    public void SecondFingerIsIgnoredWhileFirstIsActive()
    {
        interpreter.PointerDown(1, screenY: 400f);
        interpreter.PointerMoved(1, screenY: 500f); // finger 1 -> ascend

        interpreter.PointerDown(2, screenY: 400f);
        interpreter.PointerMoved(2, screenY: 200f); // finger 2 would descend, but it's ignored

        Assert.AreEqual(1f, interpreter.ClimbAxis);
    }

    [Test]
    public void AfterFirstFingerReleases_ANewTouchTakesOver()
    {
        interpreter.PointerDown(1, screenY: 400f);
        interpreter.PointerMoved(1, screenY: 500f);
        interpreter.PointerUp(1);

        interpreter.PointerDown(2, screenY: 400f);
        interpreter.PointerMoved(2, screenY: 300f);

        Assert.AreEqual(-1f, interpreter.ClimbAxis);
    }

    [Test]
    public void MovedForUntrackedPointer_IsIgnored()
    {
        interpreter.PointerDown(1, screenY: 400f);
        interpreter.PointerMoved(9, screenY: 900f);

        Assert.AreEqual(0f, interpreter.ClimbAxis);
    }

    [Test]
    public void Reset_ClearsTheTouchAndAxis()
    {
        interpreter.PointerDown(1, screenY: 400f);
        interpreter.PointerMoved(1, screenY: 500f);

        interpreter.Reset();
        Assert.AreEqual(0f, interpreter.ClimbAxis);

        // A fresh touch after reset works normally.
        interpreter.PointerDown(2, screenY: 400f);
        interpreter.PointerMoved(2, screenY: 500f);
        Assert.AreEqual(1f, interpreter.ClimbAxis);
    }
}
