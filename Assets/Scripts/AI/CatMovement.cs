using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class CatMovement : MonoBehaviour
{
    [SerializeField]
    int feedingScore = 10;

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
    string fullyFedAnimationTrigger = "HappyCat";

    [SerializeField]
    string attackAnimationTrigger = "AngryCat";

    [Header("Health")]

    [SerializeField]
    int treatsUntilFull = 3;

    [SerializeField]
    GameObject moodCanvas;

    [SerializeField]
    Image moodImage;

    [SerializeField]
    [Tooltip("This should be the same size as 'Treats Until Full' and be ordered from happiest to grumpiest.")]
    Sprite[] moodSprites;

    [SerializeField]
    [Min(0)]
    float hideMoodCanvasDelay = 1.2f;

    [SerializeField]
    [Min(0)]
    int eatingParticleIndex;

    [SerializeField]
    [Tooltip("If false, cat will immediately despawn after 'fully fed' animation.")]
    bool returnHomeWhenFull = true;

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
    Transform home;
    Vector3 targetDestination;
    DefendPoint targetDefendPoint;
    int currentHealth;

    int eatingTreatTriggerHash;
    int attackTriggerHash;

    float moodDisplayTimer;
    Coroutine moodCanvasHideRoutine;

    public bool isMoving => agent.velocity.magnitude > 0;
    public bool isFullyFed => currentState == AIState.Full || currentState == AIState.ReturningHome;

    void Start()
    {
        eatingTreatTriggerHash = Animator.StringToHash(fullyFedAnimationTrigger);
        attackTriggerHash = Animator.StringToHash(attackAnimationTrigger);
    }

    void OnDisable()
    {
        StopAllCoroutines();
    }

    public void Initialise(Transform spawnPoint)
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

        home = spawnPoint;
        currentState = AIState.Roaming;
        targetDestination = WaypointManager.instance.GetRandomWaypoint();

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
            switch (currentState)
            {
                case AIState.Roaming:
                    // Checks whether target destination has been reached
                    if (Vector3.Distance(targetDestination, transform.position) <= stoppingDistance + agent.stoppingDistance)
                    {
                        yield return interWaypointWait;

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

                        while(true)
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

                            targetDefendPoint.GetHit();
                            hitDefendPointSound.PlayOneShot(attackSource);

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
                    break;
                case AIState.Full:
                    // Wait for animation of cat eating
                    agent.isStopped = true;
                    yield return new WaitForSeconds(fullyFedPauseDuration);

                    if (returnHomeWhenFull)
                    {
                        // Begin cat's return home
                        agent.isStopped = false;
                        currentState = AIState.ReturningHome;
                        targetDestination = home.position;
                    }
                    else
                    {
                        DisappearParticleEffect();
                        gameObject.SetActive(false);
                    }
                    break;
                case AIState.ReturningHome:
                    if (Vector3.Distance(targetDestination, transform.position) <= stoppingDistance + agent.stoppingDistance)
                    {
                        // Setting the cat to inactive will automatically recycle in back into the pool for spawning
                        gameObject.SetActive(false);
                    }
                    break;
            }

            agent.destination = targetDestination;
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

        moodCanvas.SetActive(true);
        SetMoodImage();
        moodDisplayTimer = hideMoodCanvasDelay;
        if (moodCanvasHideRoutine == null)
        {
            moodCanvasHideRoutine = StartCoroutine(HideMoodCanvasRoutine());
        }
        ParticleManager.instance.ShowParticleEffect(eatingParticleIndex, voiceSource.transform.position);

        fedSound.PlayOneShot(voiceSource);

        if (currentHealth <= 0)
        {
            currentState = AIState.Full;
            GameManager.instance.FeedCat(feedingScore);
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetTrigger(eatingTreatTriggerHash);
            }
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
            Sprite sp = moodSprites[currentHealth];
            if (sp != null)
            {
                moodImage.sprite = sp;
            }
        }
    }

    IEnumerator HideMoodCanvasRoutine()
    {
        while (moodDisplayTimer > 0)
        {
            moodDisplayTimer -= Time.deltaTime;
            yield return null;
        }
        moodCanvas.SetActive(false);
        moodCanvasHideRoutine = null;
    }

    public void DisappearParticleEffect()
    {
        ParticleManager.instance.ShowParticleEffect(disappearParticleIndex, transform.position);
    }

    public enum AIState
    {
        Roaming,
        Targeting,
        Full,
        ReturningHome
    }
}
