using UnityEngine;

public class InfiniteAmmoPickup : Pickup
{
    [Header("Power-Up Settings")]
    [SerializeField]
    [Min(0)]
    float duration = 10f;

    public override void OnPickup()
    {
        player.MakeAmmoInfinite(duration);
    }
}