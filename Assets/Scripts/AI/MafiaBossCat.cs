using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class MafiaBossCat : BossCat
{
    [SerializeField]
    [Min(0)]
    float minimumTimeBetweenSpawns = 5f;

    [SerializeField]
    [Min(0)]
    float maximumTimeBetweenSpawns = 10f;

    [SerializeField]
    [Min(0)]
    int minimumToSpawnAtATime = 2;

    [SerializeField]
    [Min(0)]
    int maximumToSpawnAtATime = 5;

    [SerializeField]
    [Min(0)]
    float spawnRadius = 1.2f;

    [SerializeField]
    [Min(0)]
    float preSpawnWait = 0.3f;

    [SerializeField]
    [Min(0)]
    float postSpawnWait = 0.3f;

    ObjectSpawner catPool;

    protected override void Start()
    {
        base.Start();

        ObjectSpawner[] spawners = FindObjectsOfType<ObjectSpawner>();
        foreach (ObjectSpawner spawner in spawners)
        {
            if (spawner != null && spawner.gameObject.name == "Cat Pool")
            {
                catPool = spawner;
                break;
            }
        }

        if (catPool == null)
        {
            Debug.Log("Mafia Cat was unable to find the cat pool.", this);
        }
    }

    protected override IEnumerator MoveToTargetRoutine()
    {
        WaitForSeconds updateWait = new WaitForSeconds(targetUpdateFrequency);
        WaitForSeconds preWait = new WaitForSeconds(preSpawnWait);
        WaitForSeconds postWait = new WaitForSeconds(postSpawnWait);

        currentState = AIState.Roaming;
        targetDestination = WaypointManager.instance.GetRandomWaypointInFrontOfPlayer();
        float spawnTimer = Random.Range(minimumTimeBetweenSpawns, maximumTimeBetweenSpawns);
        while (true)
        {
            if (currentState == AIState.Full)
            {
                yield break;
            }

            if (!isStunned)
            {
                // Checks whether it's time to spawn cats
                spawnTimer -= targetUpdateFrequency;
                if (spawnTimer < 0)
                {
                    spawnTimer += Random.Range(minimumTimeBetweenSpawns, maximumTimeBetweenSpawns);

                    // Get how many to spawn
                    int toSpawn = Random.Range(minimumToSpawnAtATime, maximumToSpawnAtATime + 1);
                    if (toSpawn > 0)
                    {
                        agent.isStopped = true;

                        yield return preWait;

                        // Spawn chosen number of cats in circle around boss
                        for (int i = 0; i < toSpawn; i++)
                        {
                            Vector3 spawnPosition = transform.position 
                                + spawnRadius * (Quaternion.Euler(0, i * 360 / (float)toSpawn, 0) * transform.forward);

                            if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                            {
                                CatMovement cat = catPool.GetRandomObjectFromPool<CatMovement>();
                                if (cat != null)
                                {
                                    cat.gameObject.SetActive(true);
                                    cat.Initialise(transform, hit.position);
                                }
                            }
                        }

                        yield return postWait;

                        if (currentState == AIState.Full)
                        {
                            yield break;
                        }

                        agent.isStopped = false;
                    }
                }

                // Checks whether target destination has been reached
                if (Vector3.Distance(targetDestination, transform.position) <= stoppingDistance + agent.stoppingDistance
                    && !agent.isOnOffMeshLink)
                {
                    targetDestination = WaypointManager.instance.GetNewWaypointInFrontOfPlayer(targetDestination);
                }

                if (agent.isOnNavMesh)
                {
                    agent.destination = targetDestination;
                }
            }
            else
            {
                stunTimer -= targetUpdateFrequency;
                if (stunTimer <= 0)
                {
                    EndStun();
                }
                else if (agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }
            }

            yield return updateWait;
        }
    }
}
