using UnityEngine;

public class ScoreBonusPickup : Pickup
{
    [Header("Pickup Settings")]

    [SerializeField]
    [Min(0)]
    float scoreMultiplier = 2f;

    [SerializeField]
    [Min(0)]
    float duration = 10f;

    public override void OnPickup()
    {
        GameManager.instance.ChangeScoreMultiplier(scoreMultiplier);
        Invoke(nameof(EndMultiplier), duration);
    }

    void EndMultiplier()
    {
        GameManager.instance.ChangeScoreMultiplier(1 / scoreMultiplier);
    }
}
