using UnityEngine;

public class InstaKillPickup : Pickup
{
    [Header("Pickup settings")]

    [SerializeField]
    [Min(0)]
    float effectDuration = 10f;

    public override void OnPickup()
    {
        player.MakeTreatsInstaKill(effectDuration);
    }
}
