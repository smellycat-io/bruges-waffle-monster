using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// The single <see cref="IPlayerInputSource"/> the movement code reads. It just holds the
/// current intent (run axis, climb axis, a queued jump) and exposes hooks that driver
/// components call — it does not read devices itself (beyond an optional keyboard fallback).
///
/// Default drivers: <see cref="ScreenTapZoneInput"/> feeds run + jump (left/right screen
/// halves, tap = jump); <see cref="ClimbDragInput"/> feeds the climb axis (drag up/down
/// while on a wall). Anything else calling the same hooks works the same way, so the
/// control scheme can change without touching this class or the movement state machine.
/// </summary>
public class TapPlayerInput : MonoBehaviour, IPlayerInputSource
{
    [SerializeField]
    [Tooltip("Editor convenience: also read A/D + arrow keys + Space/W so the game can be play-tested without touch hardware. No effect in a build with no keyboard.")]
    private bool enableKeyboardFallback = true;

    private bool leftHeld;
    private bool rightHeld;
    private bool jumpQueued;
    private float climbAxisFromDrag;

    public float HorizontalAxis
    {
        get
        {
            float axis = (rightHeld ? 1f : 0f) - (leftHeld ? 1f : 0f);
            axis += KeyboardHorizontal();
            return Mathf.Clamp(axis, -1f, 1f);
        }
    }

    public float ClimbAxis => Mathf.Clamp(climbAxisFromDrag + KeyboardClimb(), -1f, 1f);

    public bool ConsumeJumpRequest()
    {
        if (!jumpQueued)
        {
            return false;
        }

        jumpQueued = false;
        return true;
    }

    private void Update()
    {
        if (KeyboardJumpPressedThisFrame())
        {
            jumpQueued = true;
        }
    }

    // --- On-screen control hooks: wire these from the UI ---

    public void PressLeft() => leftHeld = true;
    public void ReleaseLeft() => leftHeld = false;
    public void PressRight() => rightHeld = true;
    public void ReleaseRight() => rightHeld = false;
    public void PressJump() => jumpQueued = true;

    /// <summary>Climb-drag hook: +1 up, -1 down, 0 hang. Driver pushes this every frame.</summary>
    public void SetClimbAxis(float value) => climbAxisFromDrag = value;

    /// <summary>Safety hook for zone-style controls: clears both run directions at once.</summary>
    public void ReleaseAllRun()
    {
        leftHeld = false;
        rightHeld = false;
    }

    private float KeyboardHorizontal()
    {
        if (!enableKeyboardFallback)
        {
            return 0f;
        }
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return 0f;
        }

        float axis = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            axis -= 1f;
        }
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            axis += 1f;
        }
        return axis;
#elif ENABLE_LEGACY_INPUT_MANAGER
        float axis = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            axis -= 1f;
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            axis += 1f;
        }
        return axis;
#else
        return 0f;
#endif
    }

    private float KeyboardClimb()
    {
        if (!enableKeyboardFallback)
        {
            return 0f;
        }
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return 0f;
        }

        float axis = 0f;
        if (keyboard.upArrowKey.isPressed)
        {
            axis += 1f;
        }
        if (keyboard.downArrowKey.isPressed)
        {
            axis -= 1f;
        }
        return axis;
#elif ENABLE_LEGACY_INPUT_MANAGER
        float axis = 0f;
        if (Input.GetKey(KeyCode.UpArrow))
        {
            axis += 1f;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            axis -= 1f;
        }
        return axis;
#else
        return 0f;
#endif
    }

    private bool KeyboardJumpPressedThisFrame()
    {
        if (!enableKeyboardFallback)
        {
            return false;
        }
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        return keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W);
#else
        return false;
#endif
    }
}
