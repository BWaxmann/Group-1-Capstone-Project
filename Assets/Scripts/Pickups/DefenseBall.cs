using UnityEngine;

public class DefenseBall : Pickup
{
    [Header("Defense Ball Settings")]
    [SerializeField]
    [Min(0)]
    float shieldDuration = 10f;

    public override void OnPickup()
    {
        GameManager.instance.ShieldAllDefendPoints(shieldDuration);
    }
}
