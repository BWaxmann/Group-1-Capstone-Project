using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class CatMovement : MonoBehaviour
{
    [SerializeField]
    int feedingScore = 10;

    [SerializeField]
    Transform scoreDisplayPosition;

    [SerializeField]
    [Tooltip("In radians per second.")]
    float rotationSpeed = 12f;

    [Header("Pickups")]

    [SerializeField]
    PickupDrop[] pickupDrops;

    [SerializeField]
    Vector3 pickupOffset = Vector3.up;

    [Header("AI Behaviour")]

    [SerializeField]
    [Min(0)]
    float targetUpdateFrequency = 0.2f;

    [SerializeField]
    float stoppingDistance = 0.1f;

    [SerializeField]
    [Min(0)]
    float pauseDurationBetweenWaypoints = 0.2f;

    [SerializeField]
    [Tooltip("This is the probability of going from roaming to targeting a defend point after reaching a waypoing.")]
    [Range(0f, 1f)]
    float roamingToTargetingChance = 0.2f;

    [SerializeField]
    [Tooltip("This is the probability of going from targeting a defend point to roaming after attacking the defend point.")]
    [Range(0f, 1f)]
    float targetingToRoamingChance = 0.1f;

    [SerializeField]
    [Min(0)]
    float pauseBetweenDefendPointHits = 0.5f;

    [Header("Animation")]

    [SerializeField]
    [Min(0)]
    float fullyFedPauseDuration = 1.2f;

    [SerializeField]
    Animator animator;

    [SerializeField]
    string fullyFedAnimationTrigger = "IsDisappeared";

    [SerializeField]
    string attackAnimationTrigger = "AngryCat";

    [SerializeField]
    string stunnedAnimationBool = "DizzyCat";

    [SerializeField]
    string jiggleAnimationTrigger = "Jiggle";

    [Header("Health")]

    [SerializeField]
    int treatsUntilFull = 3;

    [SerializeField]
    GameObject moodCanvas;

    [SerializeField]
    Image moodImage;

    [SerializeField]
    [Tooltip("This should be ordered from happiest to grumpiest.")]
    Sprite[] moodSprites;

    [SerializeField]
    [Min(0)]
    float hideMoodCanvasDelay = 1.2f;

    [SerializeField]
    [Min(0)]
    int eatingParticleIndex;

    [SerializeField]
    int disappearParticleIndex;

    [Header("Sounds")]

    [SerializeField] 
    AudioSource voiceSource;

    [SerializeField]
    AudioSource attackSource;

    [SerializeField]
    SoundData[] roamingSounds;

    [SerializeField]
    [Min(0)]
    float roamingSoundMinimumPause = 4f;

    [SerializeField]
    [Min(0)]
    float roamingSoundMaximumPause = 10f;

    [SerializeField]
    SoundData hitDefendPointSound = new SoundData(1f, 1f);

    [SerializeField]
    SoundData fedSound = new SoundData(1f, 1f);

    NavMeshAgent agent;
    AIState currentState;
    Vector3 targetDestination;
    DefendPoint targetDefendPoint;

    int currentHealth;
    bool isStunned;
    float stunTimer;
    bool previousStoppedState;

    int eatingTreatTriggerHash;
    int attackTriggerHash;
    int stunnedBoolHash;
    int jiggleTriggerHash;

    float moodDisplayTimer;

    PlayerInput player;

    public bool isMoving => agent.velocity.magnitude > 0;
    public bool isFullyFed => currentState == AIState.Full || currentState == AIState.ReturningHome;

    void Start()
    {
        eatingTreatTriggerHash = Animator.StringToHash(fullyFedAnimationTrigger);
        attackTriggerHash = Animator.StringToHash(attackAnimationTrigger);
        stunnedBoolHash = Animator.StringToHash(stunnedAnimationBool);
        jiggleTriggerHash = Animator.StringToHash(jiggleAnimationTrigger);

        player = FindObjectOfType<PlayerInput>();
    }

    void OnDisable()
    {
        StopAllCoroutines();
    }

    void Update()
    {
        if (moodDisplayTimer > 0)
        {
            moodDisplayTimer -= Time.deltaTime;
        }
        else
        {
            moodCanvas.SetActive(false);
        }
    }

    void FixedUpdate()
    {
        if (!agent.isStopped)
        {
            transform.forward = Vector3.RotateTowards(transform.forward, agent.velocity.normalized, rotationSpeed * Time.fixedDeltaTime, 0);
        }
    }

    public void Initialise(Transform spawnPoint, Vector3 spawnPosition)
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (voiceSource == null)
        {
            voiceSource = GetComponentInChildren<AudioSource>();
        }

        agent.Warp(spawnPosition);
        transform.rotation = spawnPoint.rotation;
        currentState = AIState.Roaming;
        targetDestination = WaypointManager.instance.GetRandomWaypoint();
        agent.updatePosition = true;
        isStunned = false;

        StartCoroutine(MoveToTargetRoutine());
        if (voiceSource != null)
        {
            StartCoroutine(SoundRoutine());
        }

        currentHealth = treatsUntilFull;
        if (moodCanvas != null)
        {
            moodCanvas.SetActive(false);
            moodDisplayTimer = 0;
        }
    }

    IEnumerator MoveToTargetRoutine()
    {
        WaitForSeconds updateWait = new WaitForSeconds(targetUpdateFrequency);
        WaitForSeconds interWaypointWait = new WaitForSeconds(pauseDurationBetweenWaypoints);
        WaitForSeconds interAttackWait = new WaitForSeconds(pauseBetweenDefendPointHits);
        while (true)
        {
            if (currentState == AIState.Full)
            {
                yield break;
            }

            if (!isStunned)
            {
                switch (currentState)
                {
                    case AIState.Roaming:
                        // Checks whether target destination has been reached
                        if (Vector3.Distance(targetDestination, transform.position) <= stoppingDistance + agent.stoppingDistance)
                        {
                            yield return interWaypointWait;

                            if (currentState != AIState.Roaming)
                            {
                                break;
                            }

                            // Determines whether to go after a defend point or keep roaming
                            if (Random.Range(0f, 1f) < roamingToTargetingChance)
                            {
                                targetDefendPoint = GameManager.instance.GetRandomDefendPoint();
                                if (targetDefendPoint != null)
                                {
                                    currentState = AIState.Targeting;
                                    targetDestination = targetDefendPoint.position;
                                }
                                else
                                {
                                    targetDestination = WaypointManager.instance.GetNewWaypoint(targetDestination);
                                }
                            }
                            else
                            {
                                targetDestination = WaypointManager.instance.GetNewWaypoint(targetDestination);
                            }
                        }
                        break;
                    case AIState.Targeting:
                        // Checks whether target destination has been reached
                        if (Vector3.Distance(targetDestination, transform.position) <= stoppingDistance + agent.stoppingDistance)
                        {
                            agent.isStopped = true;

                            while (true)
                            {
                                /*
                                if (animator != null && animator.runtimeAnimatorController != null)
                                {
                                    animator.SetTrigger(attackTriggerHash);
                                }
                                */

                                yield return interAttackWait;

                                if (currentState != AIState.Targeting)
                                {
                                    break;
                                }

                                if (!isStunned)
                                {
                                    hitDefendPointSound.PlayOneShot(attackSource);
                                    targetDefendPoint.GetHit();

                                    if (Random.Range(0f, 1f) < targetingToRoamingChance)
                                    {
                                        currentState = AIState.Roaming;
                                        targetDestination = WaypointManager.instance.GetRandomWaypoint();
                                        targetDefendPoint = null;
                                        agent.isStopped = false;
                                        break;
                                    }
                                }
                            }
                        }
                        break;
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
            if (currentState == AIState.Full)
            {
                yield break;
            }
            yield return updateWait;
        }
    }

    IEnumerator SoundRoutine()
    {
        if (roamingSounds != null && roamingSounds.Length > 0)
        {
            float nextTime = Random.Range(roamingSoundMinimumPause, roamingSoundMaximumPause);
            float timer = Random.Range(0, nextTime);
            while (!isFullyFed)
            {
                timer += Time.deltaTime;
                if (timer >= nextTime)
                {
                    roamingSounds[Random.Range(0, roamingSounds.Length)].PlayOneShot(voiceSource);
                    timer -= nextTime;
                    nextTime = Random.Range(roamingSoundMinimumPause, roamingSoundMaximumPause); ;
                }
                yield return null;
            }
        }
    }

    public void FeedCat()
    {
        if (isFullyFed) return;

        currentHealth--;

        moodDisplayTimer = hideMoodCanvasDelay;
        moodCanvas.SetActive(true);
        SetMoodImage();

        ParticleManager.instance.ShowParticleEffect(eatingParticleIndex, voiceSource.transform.position);
        fedSound.PlayOneShot(voiceSource);

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetTrigger(jiggleTriggerHash);
        }

        if (currentHealth <= 0)
        {
            currentState = AIState.Full;
            GameManager.instance.FeedCat(feedingScore, scoreDisplayPosition.position);
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetTrigger(eatingTreatTriggerHash);
            }
            agent.isStopped = true;
            Invoke(nameof(Vanish), fullyFedPauseDuration);
        }
        else if (currentState == AIState.Targeting && Random.Range(0f, 1f) < targetingToRoamingChance)
        {
            currentState = AIState.Roaming;
            targetDestination = WaypointManager.instance.GetRandomWaypoint();
            targetDefendPoint = null;
            agent.isStopped = false;
        }
    }

    void SetMoodImage()
    {
        if (moodImage != null && moodSprites != null && moodSprites.Length > currentHealth)
        {
            int index = Mathf.CeilToInt(moodSprites.Length * currentHealth / (float)treatsUntilFull);
            Sprite sp = moodSprites[currentHealth];
            if (sp != null)
            {
                moodImage.sprite = sp;
            }
        }
    }

    void Vanish()
    {
        DisappearParticleEffect();
        DropPickup();
        // Setting the cat to inactive will automatically recycle in back into the pool for spawning
        gameObject.SetActive(false);
    }

    public void DisappearParticleEffect()
    {
        ParticleManager.instance.ShowParticleEffect(disappearParticleIndex, transform.position);
    }

    void DropPickup()
    {
        bool lowAmmo = player.currentAmmo < 20;

        float max = 0;
        foreach (PickupDrop pd in pickupDrops)
        {
            if (lowAmmo && pd.changeChanceWhenLowAmmo)
            {
                max += pd.lowAmmoChance;
            }
            else
            {
                max += pd.dropChance;
            }
        }

        float rand = Random.Range(0f, Mathf.Max(1, max));
        float c = 0;

        foreach (PickupDrop pd in pickupDrops)
        {
            if (pd.changeChanceWhenLowAmmo && lowAmmo)
            {
                c += pd.lowAmmoChance;
            }
            else
            {
                c += pd.dropChance;
            }

            if (rand < c)
            {
                PickupManager.instance.SpawnPickup(pd.pickupIndex, pickupOffset + transform.position);
                return;
            }
        }
    }

    public void GetStunned(float duration)
    {
        isStunned = true;
        stunTimer = Mathf.Max(stunTimer, duration);
        previousStoppedState = agent.isStopped;

        /*
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(stunnedBoolHash, true);
        }
        */
    }

    void EndStun()
    {
        isStunned = false;
        agent.isStopped = previousStoppedState;

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(stunnedBoolHash, false);
        }
    }

    public enum AIState
    {
        Roaming,
        Targeting,
        Full,
        ReturningHome
    }

    [Serializable]
    public struct PickupDrop
    {
        [Tooltip("Index of the pickup in the Pickup Manager's Object Spawner.")]
        [Min(0)]
        public int pickupIndex;

        [Tooltip("Chance of the pickup being dropped once cat despawns. Value between 0 and 1.")]
        [Range(0f, 1f)]
        public float dropChance;

        public bool changeChanceWhenLowAmmo;

        [Min(0.01f)]
        public float lowAmmoChanceMultiplier;

        public float lowAmmoChance => dropChance * lowAmmoChanceMultiplier;
    }
}