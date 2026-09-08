using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Tap-based <see cref="IPlayerInputSource"/>. Run is press-and-hold (hold the left/right
/// control to run that way, release to stop); jump is a single tap.
///
/// This component only exposes press/release hooks — it does not know or care whether they
/// are driven by small on-screen buttons or by full-screen left/right tap zones. Wire
/// either style to <see cref="PressLeft"/> / <see cref="ReleaseLeft"/> etc. (a
/// <see cref="HoldButton"/> for the run controls, a plain Button for jump) and the layout
/// can be swapped later without touching this class. The placeholder scene uses on-screen
/// buttons — that is the intended default and is trivial to change.
/// </summary>
public class TapPlayerInput : MonoBehaviour, IPlayerInputSource
{
    [SerializeField]
    [Tooltip("Editor convenience: also read A/D + Space/W so the game can be play-tested without touch hardware. Has no effect in a build with no keyboard.")]
    private bool enableKeyboardFallback = true;

    private bool leftHeld;
    private bool rightHeld;
    private bool jumpQueued;

    public float HorizontalAxis
    {
        get
        {
            float axis = (rightHeld ? 1f : 0f) - (leftHeld ? 1f : 0f);
            axis += KeyboardHorizontal();
            return Mathf.Clamp(axis, -1f, 1f);
        }
    }

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
