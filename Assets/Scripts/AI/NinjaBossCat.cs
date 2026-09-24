using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NinjaBossCat : BossCat
{
    [SerializeField]
    [Min(0)]
    float waypointSitShortDuration = 1f;

    [SerializeField]
    [Min(0)]
    float waypointSitLongDuration = 5f;

    [SerializeField]
    [Min(0)]
    int shortsBeforeLong = 2;

    [SerializeField]
    [Min(0)]
    float defendPointAttackDuration = 10f;

    Transform[] ninjaWaypoints;
    List<int> waypointSequence = new List<int>();
    float attackTimer;
    Vector3 playerPosition;

    protected override void Start()
    {
        base.Start();

        ninjaWaypoints = FindObjectOfType<NinjaWaypoints>().waypoints;
        playerPosition = FindObjectOfType<PlayerInput>().transform.position;
    }

    protected override IEnumerator MoveToTargetRoutine()
    {
        WaitForSeconds updateWait = new WaitForSeconds(targetUpdateFrequency);
        WaitForSeconds shortWait = new WaitForSeconds(waypointSitShortDuration);
        WaitForSeconds longWait = new WaitForSeconds(waypointSitLongDuration);
        WaitForSeconds attackWait = new WaitForSeconds(pauseBetweenDefendPointHits);

        currentState = AIState.Roaming;

        yield return shortWait;

        GetNewWaypointSequence();
        int shortCounter = 0;
        while (true)
        {
            if (currentState == AIState.Full)
            {
                yield break;
            }

            if (!isStunned)
            {
                if (shortCounter >= shortsBeforeLong)
                {
                    DisappearParticleEffect();
                    if (Random.Range(0f, 1f) < roamingToTargetingChance)
                    {
                        targetDefendPoint = GameManager.instance.GetRandomDefendPoint();
                        if (targetDefendPoint != null)
                        {
                            currentState = AIState.Targeting;
                            TeleportTo(targetDefendPoint.transform);
                            attackTimer = 0f;
                            yield return AttackRoutine(attackWait, updateWait);
                        }
                        else
                        {
                            TeleportTo(ninjaWaypoints[waypointSequence[shortCounter]]);
                            yield return longWait;
                        }
                    }
                    else
                    {
                        TeleportTo(ninjaWaypoints[waypointSequence[shortCounter]]);
                        yield return longWait;
                    }
                    shortCounter = 0;
                    GetNewWaypointSequence();
                }
                else
                {
                    DisappearParticleEffect();
                    TeleportTo(ninjaWaypoints[waypointSequence[shortCounter]]);
                    shortCounter++;
                    yield return shortWait;
                }
            }
            else
            {
                stunTimer -= targetUpdateFrequency;
                if (stunTimer <= 0)
                {
                    EndStun();
                }

                yield return updateWait;
            }
        }
    }

    protected override IEnumerator RotationRoutine()
    {
        WaitWhile wait = new WaitWhile(() => isStunned);
        while (true)
        {
            if (currentState == AIState.Targeting)
            {
                transform.forward = Vector3.RotateTowards(transform.forward, targetDefendPoint.transform.GetChild(0).position - transform.position, rotationSpeed * Time.deltaTime, 0);
            }
            else
            {
                Vector3 face = playerPosition - transform.position;
                face.y = 0;
                transform.forward = Vector3.RotateTowards(transform.forward, face, rotationSpeed * Time.deltaTime, 0);
            }
            yield return wait;
        }
    }

    protected override bool CheckExitAttacking()
    {
        attackTimer += pauseBetweenDefendPointHits;
        if (attackTimer >= defendPointAttackDuration)
        {
            currentState = AIState.Roaming;
            targetDefendPoint = null;
            return true;
        }
        return false;
    }

    void TeleportTo(Transform target)
    {
        targetDestination = target.position;
        agent.Warp(targetDestination);
        agent.destination = targetDestination;
        transform.rotation = Quaternion.LookRotation(target.forward);
    }

    void GetNewWaypointSequence()
    {
        waypointSequence.Clear();

        if (ninjaWaypoints == null || ninjaWaypoints.Length == 0) return;

        for (int i = 0; i < shortsBeforeLong + 1; i++)
        {
            while (true)
            {
                int index = Random.Range(0, ninjaWaypoints.Length);
                if (ninjaWaypoints.Length == 1 || i == 0 || waypointSequence[i - 1] != index)
                {
                    waypointSequence.Add(index);
                    break;
                }
            }
        }
    }
}
