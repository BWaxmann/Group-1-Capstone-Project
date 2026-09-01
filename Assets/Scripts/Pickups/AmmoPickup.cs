using UnityEngine;

public class AmmoPickup : Pickup
{
    [SerializeField]
    [Min(0)]
    int ammoAmount = 20;

    public override void OnPickup()
    {
        player.GetAmmo(ammoAmount);
    }
}