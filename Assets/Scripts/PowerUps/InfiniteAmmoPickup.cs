using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfiniteAmmoPickup : Pickup
{
    public override void OnPickup()
    {
        Debug.Log("Infinite Ammo Pickup collected!");
    }
}