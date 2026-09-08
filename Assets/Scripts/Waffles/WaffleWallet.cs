using System;
using UnityEngine;

public class WaffleWallet : MonoBehaviour
{
    [SerializeField, Min(0)] private int capacity;
    [SerializeField, Min(0)] private int currentWaffleCount;

    public int Capacity => capacity;
    public int CurrentWaffleCount => currentWaffleCount;
    public bool IsDrained => currentWaffleCount == 0;

    public event Action Drained;

    private void Awake()
    {
        currentWaffleCount = Mathf.Clamp(currentWaffleCount, 0, capacity);
    }

    public void Initialize(int walletCapacity, int startingWaffleCount)
    {
        capacity = Mathf.Max(0, walletCapacity);
        currentWaffleCount = Mathf.Clamp(startingWaffleCount, 0, capacity);
    }

    public int RemoveWaffles(int hitStrength)
    {
        if (hitStrength <= 0 || IsDrained)
        {
            return 0;
        }

        int wafflesRemoved = Mathf.Min(hitStrength, currentWaffleCount);
        currentWaffleCount -= wafflesRemoved;

        if (IsDrained)
        {
            Drained?.Invoke();
        }

        return wafflesRemoved;
    }
}
