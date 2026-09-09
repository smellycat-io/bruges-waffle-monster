using System.Collections.Generic;

/// <summary>
/// Turns raw pointer/touch events over a left/right screen split into "run" and "jump"
/// intent, independent of the input backend. Plain class (no MonoBehaviour, no device
/// reads) so the zone split and the tap-vs-hold timing are unit-testable;
/// <see cref="ScreenTapZoneInput"/> is the thin adapter that feeds it real touches.
///
/// Rules:
/// - Pointer down in a half → that half's run direction is held (reference-counted, so a
///   second finger in the same half doesn't double-fire and the hold lasts until every
///   finger in that half lifts).
/// - Pointer up within <c>tapMaxDuration</c> of its down → a jump tap, in addition to the
///   run release. A longer press is just a hold and fires no jump.
/// </summary>
public sealed class TapZoneInterpreter
{
    /// <summary>Sink for the interpreted intent. Maps 1:1 to TapPlayerInput's hooks.</summary>
    public interface ITarget
    {
        void RunLeftPressed();
        void RunLeftReleased();
        void RunRightPressed();
        void RunRightReleased();
        void JumpTapped();
    }

    private struct PointerInfo
    {
        public bool IsLeft;
        public float DownTime;
    }

    private readonly ITarget target;
    private readonly float tapMaxDuration;
    private readonly float leftZoneFraction;
    private readonly Dictionary<int, PointerInfo> pointers = new Dictionary<int, PointerInfo>();

    private int leftHolders;
    private int rightHolders;

    /// <param name="target">Where interpreted press/release/jump calls go.</param>
    /// <param name="tapMaxDuration">Down→up faster than this (seconds) counts as a jump tap.</param>
    /// <param name="leftZoneFraction">Fraction of screen width that is the left run zone (0..1).</param>
    public TapZoneInterpreter(ITarget target, float tapMaxDuration, float leftZoneFraction = 0.5f)
    {
        this.target = target;
        this.tapMaxDuration = tapMaxDuration;
        this.leftZoneFraction = leftZoneFraction;
    }

    public void PointerDown(int pointerId, float screenX, float screenWidth, float time)
    {
        if (pointers.ContainsKey(pointerId))
        {
            return;
        }

        bool isLeft = screenWidth <= 0f || screenX < screenWidth * leftZoneFraction;
        pointers[pointerId] = new PointerInfo { IsLeft = isLeft, DownTime = time };

        if (isLeft)
        {
            if (leftHolders++ == 0) target.RunLeftPressed();
        }
        else
        {
            if (rightHolders++ == 0) target.RunRightPressed();
        }
    }

    /// <summary>A completed press. Releases the run hold, and fires a jump if it was quick.</summary>
    public void PointerUp(int pointerId, float time)
    {
        if (!ReleasePointer(pointerId, out PointerInfo info))
        {
            return;
        }

        if (time - info.DownTime <= tapMaxDuration)
        {
            target.JumpTapped();
        }
    }

    /// <summary>An aborted press (e.g. OS-cancelled touch). Releases the run hold, never jumps.</summary>
    public void PointerCancelled(int pointerId) => ReleasePointer(pointerId, out _);

    /// <summary>Drops all pointers and run holds (e.g. component disabled).</summary>
    public void Reset()
    {
        pointers.Clear();
        if (leftHolders > 0) target.RunLeftReleased();
        if (rightHolders > 0) target.RunRightReleased();
        leftHolders = 0;
        rightHolders = 0;
    }

    private bool ReleasePointer(int pointerId, out PointerInfo info)
    {
        if (!pointers.TryGetValue(pointerId, out info))
        {
            return false;
        }

        pointers.Remove(pointerId);

        if (info.IsLeft)
        {
            if (--leftHolders == 0) target.RunLeftReleased();
        }
        else
        {
            if (--rightHolders == 0) target.RunRightReleased();
        }
        return true;
    }
}
