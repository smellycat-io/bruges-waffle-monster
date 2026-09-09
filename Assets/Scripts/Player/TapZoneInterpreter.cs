using System.Collections.Generic;

/// <summary>
/// Turns raw pointer/touch events over a left/right screen split into "run" and "jump"
/// intent, independent of the input backend. Plain class (no MonoBehaviour, no device
/// reads) so the zone split and the tap-vs-hold timing are unit-testable;
/// <see cref="ScreenTapZoneInput"/> is the thin adapter that feeds it real touches.
///
/// Tap-vs-hold: a press does NOT start running immediately. It starts running only once
/// it has been held for <c>tapMaxDuration</c> (promoted by <see cref="Tick"/>). If it is
/// released before then it was a jump tap and never ran — so a quick jump produces zero
/// run drift. Run holds are reference-counted per half, so a second finger in the same
/// half neither double-fires nor ends the hold early.
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
        public bool Running; // promoted from "pending tap" to a held run
    }

    private readonly ITarget target;
    private readonly float tapMaxDuration;
    private readonly float leftZoneFraction;
    private readonly Dictionary<int, PointerInfo> pointers = new Dictionary<int, PointerInfo>();
    private readonly List<int> promoteScratch = new List<int>();

    private int leftHolders;
    private int rightHolders;

    /// <param name="target">Where interpreted press/release/jump calls go.</param>
    /// <param name="tapMaxDuration">Held longer than this (seconds) -> a run hold; released sooner -> a jump tap.</param>
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
        pointers[pointerId] = new PointerInfo { IsLeft = isLeft, DownTime = time, Running = false };
    }

    /// <summary>
    /// Advance time. Any still-pressed pointer held past <c>tapMaxDuration</c> is promoted
    /// to a run hold now. Call once per frame with a monotonic time.
    /// </summary>
    public void Tick(float time)
    {
        promoteScratch.Clear();
        foreach (var kvp in pointers)
        {
            if (!kvp.Value.Running && time - kvp.Value.DownTime >= tapMaxDuration)
            {
                promoteScratch.Add(kvp.Key);
            }
        }

        foreach (int id in promoteScratch)
        {
            PointerInfo info = pointers[id];
            info.Running = true;
            pointers[id] = info;
            PressRunHold(info.IsLeft);
        }
    }

    /// <summary>A completed press: releases a promoted run hold, or fires a jump if it never promoted.</summary>
    public void PointerUp(int pointerId, float time)
    {
        if (!pointers.TryGetValue(pointerId, out PointerInfo info))
        {
            return;
        }

        pointers.Remove(pointerId);

        if (info.Running)
        {
            ReleaseRunHold(info.IsLeft);
        }
        else if (time - info.DownTime < tapMaxDuration)
        {
            target.JumpTapped();
        }
        // else: released right at / past the threshold before Tick promoted it -> no-op
        // (in practice Tick runs every frame and would have promoted it to a run).
    }

    /// <summary>An aborted press (e.g. OS-cancelled touch): releases any run hold, never jumps.</summary>
    public void PointerCancelled(int pointerId)
    {
        if (!pointers.TryGetValue(pointerId, out PointerInfo info))
        {
            return;
        }

        pointers.Remove(pointerId);
        if (info.Running)
        {
            ReleaseRunHold(info.IsLeft);
        }
    }

    /// <summary>Drops all pointers and run holds (e.g. component disabled).</summary>
    public void Reset()
    {
        pointers.Clear();
        if (leftHolders > 0)
        {
            target.RunLeftReleased();
        }
        if (rightHolders > 0)
        {
            target.RunRightReleased();
        }
        leftHolders = 0;
        rightHolders = 0;
    }

    private void PressRunHold(bool isLeft)
    {
        if (isLeft)
        {
            if (leftHolders++ == 0) target.RunLeftPressed();
        }
        else
        {
            if (rightHolders++ == 0) target.RunRightPressed();
        }
    }

    private void ReleaseRunHold(bool isLeft)
    {
        if (isLeft)
        {
            if (--leftHolders == 0) target.RunLeftReleased();
        }
        else
        {
            if (--rightHolders == 0) target.RunRightReleased();
        }
    }
}
