using UnityEngine;

[CreateAssetMenu(fileName = "CitizenTypeData", menuName = "Bruges Waffle Monster/Citizen Type Data")]
public class CitizenTypeData : ScriptableObject
{
    [SerializeField, Min(0)] private int walletSize;
    [SerializeField, Min(0)] private int hitStrength;
    [SerializeField, Min(0f)] private float speed;

    public int WalletSize => walletSize;
    public int HitStrength => hitStrength;
    public float Speed => speed;
}
