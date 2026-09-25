using System.Collections;
using UnityEngine;

public class ArmouredBossCat : BossCat
{
    [SerializeField]
    [Min(0)]
    int armourMaxHealth = 20;

    [SerializeField]
    GameObject armourOnCat;

    [SerializeField]
    Rigidbody armourSeperatedPrefab;

    [SerializeField]
    [Min(0)]
    float armouredSpeed = 1f;

    [SerializeField]
    [Min(0)]
    float unarmouredSpeed = 1.5f;

    [SerializeField]
    [Min(0)]
    float armourFlyawayForce = 20f;

    [SerializeField]
    [Min(0)]
    float armourFlyawayVertical = 1f;

    [SerializeField]
    [Min(0)]
    float armourFlyawayDirectionMaxAngle = 60f;

    [SerializeField]
    [Min(0)]
    float armourFlyawayInitialOffset = 0.5f;

    [SerializeField]
    [Min(0)]
    float armourLossPauseDuration = 2f;

    [SerializeField]
    AudioSource armourHitSource;

    [SerializeField]
    SoundData armourHitSound = new SoundData(1, 1);

    Vector3 playerPosition;
    int armourHealth;
    Rigidbody armourSeperatedInstance;

    protected override void Start()
    {
        base.Start();

        playerPosition = FindObjectOfType<PlayerInput>().transform.position;

        armourSeperatedInstance = Instantiate(armourSeperatedPrefab);
        armourSeperatedInstance.gameObject.SetActive(false);
    }

    public override void Initialise(Transform spawnPoint, Vector3 spawnPosition)
    {
        base.Initialise(spawnPoint, spawnPosition);
        armourHealth = armourMaxHealth;
        armourOnCat.SetActive(true);
        agent.speed = armouredSpeed;
    }

    protected override IEnumerator MoveToTargetRoutine()
    {
        WaitForSeconds updateWait = new WaitForSeconds(targetUpdateFrequency);
        WaitForSeconds armourLossWait = new WaitForSeconds(armourLossPauseDuration);
        WaitForSeconds interAttackWait = new WaitForSeconds(pauseBetweenDefendPointHits);

        currentState = AIState.Targeting;
        targetDefendPoint = GameManager.instance.GetClosestDefendPoint(transform.position);
        targetDestination = targetDefendPoint.position;
        bool hasDonePause = false;
        while (true)
        {
            if (currentState == AIState.Full)
            {
                yield break;
            }

            if (!isStunned)
            {
                if (currentState == AIState.Targeting)
                {
                    if (Vector3.Distance(targetDestination, transform.position) <= stoppingDistance + agent.stoppingDistance
                            && !agent.isOnOffMeshLink)
                    {
                        agent.isStopped = true;

                        yield return AttackRoutine(interAttackWait, updateWait);
                    }
                }
                else
                {
                    // Boss has lost their armour
                    if (hasDonePause)
                    {
                        if (Vector3.Distance(targetDestination, transform.position) <= stoppingDistance + agent.stoppingDistance
                                && !agent.isOnOffMeshLink)
                        {
                            // Pick up dropped armour
                            armourSeperatedInstance.gameObject.SetActive(false);
                            armourOnCat.SetActive(true);
                            agent.speed = armouredSpeed;
                            armourHealth = armourMaxHealth;
                            
                            currentState = AIState.Targeting;
                            targetDefendPoint = GameManager.instance.GetClosestDefendPoint(transform.position);
                            targetDestination = targetDefendPoint.position;
                            hasDonePause = false;
                        }
                        else
                        {
                            targetDestination = armourSeperatedInstance.position;
                        }
                    }
                    else
                    {
                        yield return armourLossWait;

                        if (currentState == AIState.Full)
                        {
                            yield break;
                        }

                        hasDonePause = true;
                        agent.isStopped = false;
                        targetDestination = armourSeperatedInstance.position;
                    }
                }

                if (agent.isOnNavMesh && !agent.isStopped)
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
            if (currentState == AIState.Full)
            {
                yield break;
            }
            yield return updateWait;
        }
    }

    protected override bool CheckExitAttacking()
    {
        if (armourHealth <= 0)
        {
            currentState = AIState.Roaming;
            targetDefendPoint = null;
            return true;
        }
        return false;
    }

    public override void FeedCat(bool instaKill)
    {
        if (isFullyFed) return;
        if (armourHealth > 0)
        {
            if (instaKill)
            {
                armourHealth = Mathf.Max(armourHealth - 2, 0);
            }
            else
            {
                armourHealth--;
            }
            if (armourHealth <= 0)
            {
                armourOnCat.SetActive(false);
                armourSeperatedInstance.gameObject.SetActive(true);
                agent.speed = unarmouredSpeed;

                Vector3 flyDirection = playerPosition - transform.position;
                flyDirection.y = 0;
                flyDirection.Normalize();
                Quaternion flyDirRotation = Quaternion.Euler(0, Random.Range(-1f, 1f) * armourFlyawayDirectionMaxAngle, 0);
                flyDirection = flyDirRotation * flyDirection;
                flyDirection.y = armourFlyawayVertical;
                flyDirection.Normalize();

                armourSeperatedInstance.position = armourOnCat.transform.position + flyDirection * armourFlyawayInitialOffset;
                armourSeperatedInstance.AddForce(flyDirection * armourFlyawayForce, ForceMode.Impulse);
            }

            ParticleManager.instance.ShowParticleEffect(eatingParticleIndex, voiceSource.transform.position);
            armourHitSound.PlayOneShot(armourHitSource);
        }
        else
        {
            base.FeedCat(instaKill);
        }
    }

    protected override void BeginVanish()
    {
        base.BeginVanish();

        armourSeperatedInstance.gameObject.SetActive(false);
    }
}
