using System.Collections;
using UnityEngine;

public abstract class BossCat : CatMovement
{
    [Header("Boss Cat Data")]

    [SerializeField]
    [Min(0)]
    float despawnAfter = 60f;

    public override void Initialise(Transform spawnPoint, Vector3 spawnPosition)
    {
        base.Initialise(spawnPoint, spawnPosition);
        StartCoroutine(DespawnRoutine());
    }

    protected virtual IEnumerator DespawnRoutine()
    {
        yield return new WaitForSeconds(despawnAfter);

        if (currentState != AIState.Full)
        {
            BeginVanish();
        }
    }
}
