using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class CatMovement : MonoBehaviour
{
    [SerializeField]
    protected int feedingScore = 10;

    [SerializeField]
    protected Transform scoreDisplayPosition;

    [SerializeField]
    [Tooltip("In radians per second.")]
    protected float rotationSpeed = 12f;

    [Header("Pickups")]

    [SerializeField]
    protected PickupDrop[] pickupDrops;

    [SerializeField]
    protected Vector3 pickupOffset = Vector3.up;

    [Header("AI Behaviour")]

    [SerializeField]
    [Min(0)]
    protected float targetUpdateFrequency = 0.2f;

    [SerializeField]
    protected float stoppingDistance = 0.1f;

    [SerializeField]
    [Min(0)]
    protected float pauseDurationBetweenWaypoints = 0.2f;

    [SerializeField]
    [Tooltip("This is the probability of going from roaming to targeting a defend point after reaching a waypoing.")]
    [Range(0f, 1f)]
    protected float roamingToTargetingChance = 0.2f;

    [SerializeField]
    [Tooltip("This is the probability of going from targeting a defend point to roaming after attacking the defend point.")]
    [Range(0f, 1f)]
    protected float targetingToRoamingChance = 0.1f;

    [SerializeField]
    [Min(0)]
    protected float pauseBetweenDefendPointHits = 0.5f;

    [SerializeField]
    [Min(0)]
    protected float stunDurationMultiplier = 1f;

    [Header("Animation")]

    [SerializeField]
    [Min(0)]
    protected float fullyFedPauseDuration = 1.2f;

    [SerializeField]
    protected Animator animator;

    [SerializeField]
    protected string fullyFedAnimationTrigger = "IsDisappeared";

    [SerializeField]
    protected string attackAnimationTrigger = "AngryCat";

    [SerializeField]
    protected string stunnedAnimationBool = "DizzyCat";

    [SerializeField]
    protected string jiggleAnimationTrigger = "Jiggle";

    [SerializeField]
    protected string jiggleAnimationLayer = "Jiggle Layer";

    [Header("Health")]

    [SerializeField]
    protected int treatsUntilFull = 3;

    [SerializeField]
    protected GameObject moodCanvas;

    [SerializeField]
    protected Image moodImage;

    [SerializeField]
    [Tooltip("This should be ordered from happiest to grumpiest.")]
    protected Sprite[] moodSprites;

    [SerializeField]
    [Min(0)]
    protected float hideMoodCanvasDelay = 1.2f;

    [SerializeField]
    [Min(0)]
    protected int eatingParticleIndex = 0;

    [SerializeField]
    protected int disappearParticleIndex = 1;

    [Header("Sounds")]

    [SerializeField]
    protected AudioSource voiceSource;

    [SerializeField]
    protected AudioSource attackSource;

    [SerializeField]
    protected SoundData[] roamingSounds;

    [SerializeField]
    [Min(0)]
    protected float roamingSoundMinimumPause = 4f;

    [SerializeField]
    [Min(0)]
    protected float roamingSoundMaximumPause = 10f;

    [SerializeField]
    protected SoundData hitDefendPointSound = new SoundData(1f, 1f);

    [SerializeField]
    protected SoundData fedSound = new SoundData(1f, 1f);

    protected NavMeshAgent agent;
    protected AIState currentState;
    protected Vector3 targetDestination;
    protected DefendPoint targetDefendPoint;

    protected int currentHealth;
    protected bool isStunned;
    protected float stunTimer;
    protected bool previousStoppedState;

    protected int eatingTreatTriggerHash;
    protected int attackTriggerHash;
    protected int stunnedBoolHash;
    protected int jiggleTriggerHash;
    protected int jiggleLayerIndex;

    protected float moodDisplayTimer;

    protected PlayerInput player;
    protected Coroutine moodDisplayRoutineInstance;

    public bool isMoving => agent.velocity.magnitude > 0;
    public bool isFullyFed => currentState == AIState.Full || currentState == AIState.ReturningHome;

    protected virtual void Start()
    {
        eatingTreatTriggerHash = Animator.StringToHash(fullyFedAnimationTrigger);
        attackTriggerHash = Animator.StringToHash(attackAnimationTrigger);
        stunnedBoolHash = Animator.StringToHash(stunnedAnimationBool);
        jiggleTriggerHash = Animator.StringToHash(jiggleAnimationTrigger);

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        jiggleLayerIndex = animator.GetLayerIndex(jiggleAnimationLayer);

        player = FindObjectOfType<PlayerInput>();
    }

    protected virtual void OnDisable()
    {
        StopAllCoroutines();
    }

    public virtual void Initialise(Transform spawnPoint, Vector3 spawnPosition)
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        else
        {
            animator.SetLayerWeight(jiggleLayerIndex, 1f);
        }
        if (voiceSource == null)
        {
            voiceSource = GetComponentInChildren<AudioSource>();
        }

        agent.Warp(spawnPosition);
        transform.rotation = spawnPoint.rotation;
        agent.updatePosition = true;
        isStunned = false;

        StartCoroutine(MoveToTargetRoutine());
        StartCoroutine(RotationRoutine());
        if (voiceSource != null)
        {
            StartCoroutine(SoundRoutine());
        }

        currentHealth = treatsUntilFull;
        if (moodCanvas != null)
        {
            moodCanvas.SetActive(false);
            moodDisplayTimer = 0;
            moodDisplayRoutineInstance = null;
        }
    }

    protected virtual IEnumerator MoveToTargetRoutine()
    {
        WaitForSeconds updateWait = new WaitForSeconds(targetUpdateFrequency);
        WaitForSeconds interWaypointWait = new WaitForSeconds(pauseDurationBetweenWaypoints);
        WaitForSeconds interAttackWait = new WaitForSeconds(pauseBetweenDefendPointHits);

        currentState = AIState.Roaming;
        targetDestination = WaypointManager.instance.GetRandomWaypoint();
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
                        if (Vector3.Distance(targetDestination, transform.position) <= stoppingDistance + agent.stoppingDistance
                            && !agent.isOnOffMeshLink)
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
                        if (Vector3.Distance(targetDestination, transform.position) <= stoppingDistance + agent.stoppingDistance 
                            && !agent.isOnOffMeshLink)
                        {
                            agent.isStopped = true;

                            yield return AttackRoutine(interAttackWait, updateWait);
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

    protected virtual IEnumerator RotationRoutine()
    {
        WaitWhile wait = new WaitWhile(() => isStunned);
        while (true)
        {
            if (currentState == AIState.Targeting && agent.isStopped)
            {
                transform.forward = Vector3.RotateTowards(transform.forward, targetDefendPoint.transform.GetChild(0).position - transform.position, rotationSpeed * Time.deltaTime, 0);
            }
            else
            {
                transform.forward = Vector3.RotateTowards(transform.forward, agent.velocity.normalized, rotationSpeed * Time.deltaTime, 0);
            }
            yield return wait;
        }
    }

    protected virtual IEnumerator AttackRoutine(WaitForSeconds interAttackWait, WaitForSeconds updateWait)
    {
        while (true)
        {
            if (!isStunned)
            {
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    animator.SetTrigger(attackTriggerHash);
                }

                yield return interAttackWait;

                if (currentState != AIState.Targeting)
                {
                    break;
                }

                if (!isStunned)
                {
                    hitDefendPointSound.PlayOneShot(attackSource);
                    targetDefendPoint.GetHit();

                    if (CheckExitAttacking()) break;
                }
            }
            else
            {
                yield return updateWait;

                if (currentState != AIState.Targeting)
                {
                    break;
                }
            }
        }
    }

    protected virtual bool CheckExitAttacking()
    {
        if (Random.Range(0f, 1f) < targetingToRoamingChance)
        {
            currentState = AIState.Roaming;
            targetDestination = WaypointManager.instance.GetRandomWaypoint();
            targetDefendPoint = null;
            agent.isStopped = false;
            return true;
        }
        return false;
    }

    protected virtual IEnumerator SoundRoutine()
    {
        if (roamingSounds != null && roamingSounds.Length > 0)
        {
            while (!isFullyFed)
            {
                yield return new WaitForSeconds(Random.Range(roamingSoundMinimumPause, roamingSoundMaximumPause));

                if (isFullyFed) break;
                roamingSounds[Random.Range(0, roamingSounds.Length)].PlayOneShot(voiceSource);
            }
        }
    }

    protected virtual IEnumerator MoodDisplayRoutine()
    {
        while (moodDisplayTimer > 0)
        {
            moodDisplayTimer -= Time.deltaTime;
            yield return null;
        }

        moodCanvas.SetActive(false);
        moodDisplayRoutineInstance = null;
    }

    public virtual void FeedCat()
    {
        if (isFullyFed) return;

        currentHealth--;

        if (moodCanvas != null)
        {
            moodDisplayTimer = hideMoodCanvasDelay;
            moodCanvas.SetActive(true);
            SetMoodImage();
            if (moodDisplayRoutineInstance == null)
            {
                moodDisplayRoutineInstance = StartCoroutine(MoodDisplayRoutine());
            }
        }

        ParticleManager.instance.ShowParticleEffect(eatingParticleIndex, voiceSource.transform.position);
        fedSound.PlayOneShot(voiceSource);

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetTrigger(jiggleTriggerHash);  
        }

        if (currentHealth <= 0)
        {
            GameManager.instance.FeedCat(feedingScore, scoreDisplayPosition.position);
            BeginVanish();
        }
    }

    protected virtual void SetMoodImage()
    {
        if (moodImage != null && moodSprites != null && moodSprites.Length > 0)
        {
            int index = Mathf.CeilToInt((moodSprites.Length - 1) * currentHealth / (float)treatsUntilFull);
            Debug.Log(gameObject.name + " " + currentHealth + " : " + index);
            Sprite sp = moodSprites[index];
            if (sp != null)
            {
                moodImage.sprite = sp;
            }
        }
    }

    protected virtual void BeginVanish()
    {
        currentState = AIState.Full;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetTrigger(eatingTreatTriggerHash);
        }
        agent.isStopped = true;
        DisappearParticleEffect();
        StartCoroutine(VanishRoutine());
    }

    protected virtual IEnumerator VanishRoutine()
    {
        float timer = fullyFedPauseDuration;
        while (timer > 0f)
        {
            timer = Mathf.Max(timer - Time.deltaTime, 0f);
            animator.SetLayerWeight(jiggleLayerIndex, timer / fullyFedPauseDuration);
            yield return null;
        }

        Vanish();
    }

    protected virtual void Vanish()
    {
        DropPickup();
        // Setting the cat to inactive will automatically recycle in back into the pool for spawning
        gameObject.SetActive(false);
    }

    public virtual void DisappearParticleEffect()
    {
        ParticleManager.instance.ShowParticleEffect(disappearParticleIndex, transform.position);
    }

    protected virtual void DropPickup()
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

    public virtual void GetStunned(float duration)
    {
        isStunned = true;
        stunTimer = Mathf.Max(stunTimer, duration * stunDurationMultiplier);
        previousStoppedState = agent.isStopped;

        /*
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(stunnedBoolHash, true);
        }
        */
    }

    protected virtual void EndStun()
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