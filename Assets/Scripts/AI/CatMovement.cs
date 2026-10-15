using System;
using System.Collections;
using System.Collections.Generic;
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
    protected Slider healthBar;

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
    protected AudioSource bodySource;

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

    [SerializeField]
    protected SoundData shrinkSound = new SoundData(1f, 1f);

    [Header("Feel")]

    [SerializeField]
    [Tooltip("Happy Launch: the fed cat freezes for a moment, then flies up spinning and pops into confetti. Classic: the original shrink.")]
    protected FinisherStyle finisherStyle = FinisherStyle.HappyLaunch;

    [SerializeField]
    [Min(0)]
    [Tooltip("How long the cat freezes on the final treat before launching (hit-pause).")]
    protected float finisherFreeze = 0.09f;

    [SerializeField]
    [Min(0)]
    protected float launchUpSpeed = 4.5f;

    [SerializeField]
    [Min(0)]
    protected float launchSpinSpeed = 900f;

    [SerializeField]
    [Min(0.1f)]
    protected float launchDuration = 0.6f;

    [SerializeField]
    [Tooltip("Particle effect for the final pop (index in the Particle Manager; 2 = confetti). -1 = none.")]
    protected int launchPopParticleIndex = 2;

    protected NavMeshAgent agent;
    protected CatJuice juice;
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

    public bool isMoving => agent != null && agent.isActiveAndEnabled && agent.velocity.magnitude > 0;
    public bool isFullyFed => currentState == AIState.Full || currentState == AIState.ReturningHome;
    public virtual bool isBoss => false;

    // The defend point this cat is on its way to (or attacking), if any. Cat Chaos piles use it to warn the player.
    public DefendPoint headingFor => currentState == AIState.Targeting ? targetDefendPoint : null;

    // Every cat currently switched on, so other scripts can check on them without searching the scene
    public static readonly List<CatMovement> active = new List<CatMovement>();

    // A scene can override every cat's Roaming To Targeting Chance (Cat Chaos sends cats to the piles more often).
    // Below 0 = use each cat's own value.
    public static float targetingChanceOverride = -1f;

    protected float targetingChance => targetingChanceOverride >= 0 ? targetingChanceOverride : roamingToTargetingChance;

    protected virtual void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
    }

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
        active.Remove(this);
        StopAllCoroutines();
        // In case the cat was switched off during the finisher freeze
        if (animator != null) animator.speed = 1f;
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
        PrepareFeel();

        // The happy launch switches the agent off while the cat flies
        if (!agent.enabled) agent.enabled = true;
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

        GameManager.instance.CatSpawned();
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
                            if (Random.Range(0f, 1f) < targetingChance)
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
                Vector3 rotateTo = targetDefendPoint.transform.GetChild(0).position - transform.position;
                rotateTo.y = 0;
                transform.forward = Vector3.RotateTowards(transform.forward, rotateTo, rotationSpeed * Time.deltaTime, 0);
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
                stunTimer -= targetUpdateFrequency;
                if (stunTimer <= 0)
                {
                    EndStun();
                }

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

    // Called when a treat lands on this cat. Customer cats check the food matches their order.
    public virtual void HitByTreat(Treat treat)
    {
        // Something else can decide which treats this cat eats (e.g. a colour order); false = not eaten
        if (treatGate != null && !treatGate(treat)) return;
        FeedCat(treat.isInstaKill);
    }

    public System.Func<Treat, bool> treatGate;

    // The cat's face pictures and its pop-up mood display, for other scripts that show the cat's mood their own way
    public Sprite happiestFace => moodSprites != null && moodSprites.Length > 0 ? moodSprites[0] : null;
    public Sprite grumpiestFace => moodSprites != null && moodSprites.Length > 0 ? moodSprites[moodSprites.Length - 1] : null;
    public GameObject moodDisplay => moodCanvas;

    public virtual void FeedCat(bool instaKill)
    {
        if (isFullyFed) return;

        if (instaKill)
        {
            if (isBoss)
            {
                currentHealth = Mathf.Max(currentHealth - 2, 0);
            }
            else
            {
                currentHealth = 0;
            }
        }
        else
        {
            currentHealth--;
        }

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

        // Squish, head bobble, and a pop that gets higher with each treat
        PrepareFeel();
        juice.Hit(instaKill ? 1.6f : 1f);
        int bitesTaken = treatsUntilFull - currentHealth;
        GameManager.instance.PlayCatHitSound(bodySource != null ? bodySource : voiceSource, 1f + (bitesTaken - 1) * 0.12f);

        if (currentHealth <= 0)
        {
            Haptics.PulseBoth(isBoss ? Haptics.Strength.Huge : Haptics.Strength.Strong);
            GameManager.instance.FeedCat(feedingScore, scoreDisplayPosition.position);
            BeginVanish();
        }
    }

    // Used when the round ends: every cat still in the cafe leaves happy, without adding score.
    public virtual void CelebrateAndVanish()
    {
        if (isFullyFed || !gameObject.activeInHierarchy) return;

        StopAllCoroutines();
        if (moodCanvas != null && moodSprites != null && moodSprites.Length > 0)
        {
            currentHealth = 0;
            moodCanvas.SetActive(true);
            SetMoodImage();
        }
        BeginVanish();
    }

    protected virtual void SetMoodImage()
    {
        if (moodImage != null && moodSprites != null && moodSprites.Length > 0)
        {
            int index = Mathf.CeilToInt((moodSprites.Length - 1) * currentHealth / (float)treatsUntilFull);
            Sprite sp = moodSprites[index];
            if (sp != null)
            {
                moodImage.sprite = sp;
            }
        }

        if (healthBar != null)
        {
            healthBar.value = currentHealth / (float)treatsUntilFull;
        }
    }

    protected virtual void BeginVanish()
    {
        currentState = AIState.Full;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        if (finisherStyle == FinisherStyle.HappyLaunch)
        {
            // Stop walking/turning so they don't fight the flight
            StopAllCoroutines();
            StartCoroutine(HappyLaunchRoutine());
            return;
        }

        shrinkSound.PlayOneShot(bodySource);
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetTrigger(eatingTreatTriggerHash);
        }
        DisappearParticleEffect();
        StartCoroutine(VanishRoutine());
    }

    protected void PrepareFeel()
    {
        if (juice == null)
        {
            juice = CatJuice.AddTo(this, animator != null ? animator : GetComponentInChildren<Animator>());
        }
        juice.enabled = true;
    }

    // The fed cat freezes for a beat, then flies up spinning and pops into confetti
    protected IEnumerator HappyLaunchRoutine()
    {
        // Any pickup drops where the cat was fed, not up where it pops (pickups wait in the air to be shot)
        pickupDropPosition = transform.position;
        PrepareFeel();
        juice.Hit(2f);

        // Hit-pause: the cat holds still for a split second so the final hit lands
        float animatorSpeed = animator != null ? animator.speed : 1f;
        if (animator != null) animator.speed = 0f;
        yield return new WaitForSeconds(finisherFreeze);
        if (animator != null) animator.speed = animatorSpeed;

        // Take the cat off the floor so it can fly
        if (agent != null) agent.enabled = false;
        juice.ResetPose();
        juice.enabled = false;
        GameManager.instance.PlayCatLaunchSound(bodySource != null ? bodySource : voiceSource);

        Vector3 sideways = Random.insideUnitSphere * 0.8f;
        sideways.y = 0;
        Vector3 velocity = Vector3.up * launchUpSpeed + sideways;
        Vector3 spinAxis = new Vector3(Random.Range(-0.3f, 0.3f), 1f, Random.Range(-0.3f, 0.3f)).normalized;
        Vector3 startScale = transform.localScale;

        float timer = 0;
        while (timer < launchDuration)
        {
            float dt = Time.deltaTime;
            timer += dt;
            velocity += Physics.gravity * 0.5f * dt;
            transform.position += velocity * dt;
            transform.Rotate(spinAxis, launchSpinSpeed * dt, Space.World);

            // Puffs up just before popping
            float t = timer / launchDuration;
            transform.localScale = startScale * (1f + 0.35f * t * t);
            yield return null;
        }

        transform.localScale = startScale;
        if (launchPopParticleIndex >= 0)
        {
            ParticleManager.instance.ShowParticleEffect(launchPopParticleIndex, transform.position);
        }
        DisappearParticleEffect();
        GameManager.instance.PlayCatPopSound(transform.position);

        if (animator != null) animator.SetLayerWeight(jiggleLayerIndex, 0f);
        Vanish();
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
        GameManager.instance.CatDespawned();
        // Setting the cat to inactive will automatically recycle in back into the pool for spawning
        gameObject.SetActive(false);
    }

    public virtual void DisappearParticleEffect()
    {
        ParticleManager.instance.ShowParticleEffect(disappearParticleIndex, transform.position);
    }

    // Set by the happy launch to where the cat was fed
    protected Vector3? pickupDropPosition;

    protected virtual void DropPickup()
    {
        float max = 0;
        foreach (PickupDrop pd in pickupDrops)
        {
            max += pd.dropChance;
        }

        float rand = Random.Range(0f, Mathf.Max(1, max));
        float c = 0;

        foreach (PickupDrop pd in pickupDrops)
        {
            c += pd.dropChance;

            if (rand < c)
            {
                Vector3 dropAt = pickupDropPosition ?? transform.position;
                pickupDropPosition = null;
                PickupManager.instance.SpawnPickup(pd.pickupIndex, pickupOffset + dropAt);
                return;
            }
        }
    }

    public virtual void GetStunned(float duration)
    {
        if (isFullyFed) return;

        isStunned = true;
        stunTimer = Mathf.Max(stunTimer, duration * stunDurationMultiplier);
        previousStoppedState = agent.isStopped;
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

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

    public enum FinisherStyle
    {
        HappyLaunch,
        Classic
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
    }
}