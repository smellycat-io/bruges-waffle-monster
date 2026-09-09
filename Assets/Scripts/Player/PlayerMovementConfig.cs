using UnityEngine;

/// <summary>
/// Designer-tweakable movement numbers for the waffle monster. All values are
/// PLACEHOLDERS pending a "feel" pass — see Docs/GameDesign.md ("Player Movement").
/// </summary>
[CreateAssetMenu(fileName = "PlayerMovementConfig", menuName = "Bruges Waffle Monster/Player Movement Config")]
public class PlayerMovementConfig : ScriptableObject, IPlayerMovementConfig
{
    [Header("PLACEHOLDER VALUES — need design sign-off (Docs/GameDesign.md)")]
    [SerializeField, Min(0f)] private float runSpeed = 6f;
    [SerializeField, Min(0f)] private float jumpVelocity = 12f;
    [SerializeField, Min(0f)] private float climbSpeed = 3f;
    [SerializeField, Tooltip("x = push away from the wall, y = upward impulse.")]
    private Vector2 wallJumpVelocity = new Vector2(8f, 11f);
    [SerializeField, Min(0f)] private float gravityScale = 3f;

    public float RunSpeed => runSpeed;
    public float JumpVelocity => jumpVelocity;
    public float ClimbSpeed => climbSpeed;
    public Vector2 WallJumpVelocity => wallJumpVelocity;
    public float GravityScale => gravityScale;
}
