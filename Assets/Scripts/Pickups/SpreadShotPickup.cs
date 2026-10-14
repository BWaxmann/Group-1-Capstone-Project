using UnityEngine;

public class SpreadShotPickup : Pickup
{
    [Header("Pickup settings")]

    [SerializeField]
    [Min(0)]
    float duration = 10f;

    public override void OnPickup()
    {
        player.ActivateSpread(duration);
    }
}
