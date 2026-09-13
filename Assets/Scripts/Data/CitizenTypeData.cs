using UnityEngine;

/// <summary>
/// Per-citizen-type balance data driving the single shared <c>CitizenAI</c> behavior
/// component. One asset per type (Tourist / Vendor / Guard, see
/// <c>Assets/ScriptableObjects/Enemies/</c>) — new citizen types are new assets, never new
/// scripts.
/// </summary>
[CreateAssetMenu(fileName = "CitizenTypeData", menuName = "Bruges Waffle Monster/Citizen Type Data")]
public class CitizenTypeData : ScriptableObject
{
    [Header("Wallet (locked balance values)")]
    [SerializeField, Min(0)] private int minWalletSize;
    [SerializeField, Min(0)] private int maxWalletSize;

    [Header("Combat (locked balance values)")]
    [Tooltip("Waffles the player's stash loses on a strike-1 catch by this citizen type.")]
    [SerializeField, Min(0)] private int hitStrength;

    [Header("Movement & detection (PLACEHOLDER — needs feel-testing)")]
    [SerializeField, Min(0f)] private float moveSpeed;
    [Tooltip("Max line-of-sight raycast distance. Beyond this, the citizen can't see the player regardless of obstruction.")]
    [SerializeField, Min(0f)] private float detectionRange;

    [Header("Abilities")]
    [Tooltip("Whether this type follows the player onto ClimbableSurface geometry (simple vertical pursuit at MoveSpeed) instead of staying strictly grounded/horizontal. Data-driven so any future type can opt in without touching CitizenAI.")]
    [SerializeField] private bool canClimb;

    [Header("Placeholder visuals")]
    [Tooltip("Tint applied to the placeholder sprite so citizen types are distinguishable before real art exists.")]
    [SerializeField] private Color placeholderColor = Color.white;

    public int MinWalletSize => minWalletSize;
    public int MaxWalletSize => maxWalletSize;
    public int HitStrength => hitStrength;
    public float MoveSpeed => moveSpeed;
    public float DetectionRange => detectionRange;
    public bool CanClimb => canClimb;
    public Color PlaceholderColor => placeholderColor;

    /// <summary>Rolls a starting/carried waffle count within [<see cref="MinWalletSize"/>, <see cref="MaxWalletSize"/>].</summary>
    public int RollWalletSize() => Random.Range(minWalletSize, maxWalletSize + 1);

    private void OnValidate()
    {
        if (maxWalletSize < minWalletSize)
        {
            maxWalletSize = minWalletSize;
        }
    }
}
