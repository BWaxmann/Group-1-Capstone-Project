using System.Collections;
using UnityEngine;

public abstract class BossCat : CatMovement
{
    [Header("Boss Cat Data")]

    [SerializeField]
    [Min(0)]
    float despawnAfter = 60f;

    public override bool isBoss
    {
        get
        {
            return true;
        }
    }

    // A director that ends the boss's visit itself can make it stay longer than Despawn After (seconds; 0 = as set)
    public float despawnAfterOverride = 0f;

    public override void Initialise(Transform spawnPoint, Vector3 spawnPosition)
    {
        base.Initialise(spawnPoint, spawnPosition);
        StartCoroutine(DespawnRoutine());
    }

    protected virtual IEnumerator DespawnRoutine()
    {
        yield return new WaitForSeconds(despawnAfterOverride > 0 ? despawnAfterOverride : despawnAfter);

        if (currentState != AIState.Full)
        {
            BeginVanish();
        }
    }

    protected override void Vanish()
    {
        GameManager.instance.EndRoundEarly();

        base.Vanish();
    }
}
