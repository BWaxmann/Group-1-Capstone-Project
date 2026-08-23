using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class CatMovement : MonoBehaviour
{
    [SerializeField]
    int feedingScore = 10;

    [SerializeField] 
    int escapePenalty = 5;

    [Header("AI Behaviour")]

    [SerializeField]
    [Min(0)]
    float targetUpdateFrequency = 0.2f;

    [SerializeField]
    [Min(0)]
    int minimumWaypointsBeforeExit = 0;

    [SerializeField]
    [Min(0)]
    int maximumWaypointsBeforeExit = 3;

    [SerializeField] 
    float stoppingDistance = 0.1f;

    [SerializeField]
    [Min(0)]
    float pauseDurationBetweenWaypoints = 0.2f;

    [Header("Animation")]

    [SerializeField]
    [Min(0)]
    float fullyFedPauseDuration = 1.2f;

    [SerializeField] 
    Animator animator;

    [SerializeField] 
    string fullyFedAnimationTrigger = "HappyCat";

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
    float hideMoodCanvasWhenFullDelay = 1.2f;

    [SerializeField]
    [Tooltip("If false, cat will immediately despawn after 'fully fed' animation.")]
    bool returnHomeWhenFull = true;

    [Header("Sounds")]

    [SerializeField] 
    AudioSource voiceSource;

    [SerializeField]
    AudioClip[] roamingSounds;

    [SerializeField]
    [Range(0, 1)]
    float roamingSoundVolume = 0.4f;

    [SerializeField]
    [Min(0)]
    float roamingSoundMinimumPause = 4f;

    [SerializeField]
    [Min(0)]
    float roamingSoundMaximumPause = 10f;

    [SerializeField]
    AudioClip fedSound;

    [SerializeField]
    [Range(0, 1)]
    float fedSoundVolume = 1f;

    NavMeshAgent agent;
    AIState currentState;
    Transform home;
    Vector3 targetDestination;
    int numberOfRoamingWaypoints;
    Vector3[] roamingWaypoints;
    int numberofWaypointsReached;
    int currentHealth;
    int eatingTreatTriggerHash;

    public bool isFullyFed => currentState == AIState.EatingTreat || currentState == AIState.ReturningHome;

    void Start()
    {
        eatingTreatTriggerHash = Animator.StringToHash(fullyFedAnimationTrigger);
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
        numberOfRoamingWaypoints = Random.Range(minimumWaypointsBeforeExit, maximumWaypointsBeforeExit + 1);
        if (numberOfRoamingWaypoints == 0)
        {
            currentState = AIState.Exiting;
            targetDestination = WaypointManager.instance.ExitWaypoint;
        }
        else
        {
            currentState = AIState.Roaming;
            roamingWaypoints = WaypointManager.instance.GetListOfRandomWaypoints(numberOfRoamingWaypoints);
            numberofWaypointsReached = 0;
            targetDestination = roamingWaypoints[0];
        }

        StartCoroutine(MoveToTargetRoutine());
        if (voiceSource != null)
        {
            StartCoroutine(SoundRoutine());
        }

        currentHealth = treatsUntilFull;
        if (moodCanvas != null)
        {
            moodCanvas.SetActive(true);
            SetMoodImage();
        }
    }

    IEnumerator MoveToTargetRoutine()
    {
        WaitForSeconds updateWait = new WaitForSeconds(targetUpdateFrequency);
        WaitForSeconds interWaypointWait = new WaitForSeconds(pauseDurationBetweenWaypoints);
        while (true)
        {
            switch (currentState)
            {
                case AIState.Roaming:
                    // Checks whether target destination has been reached
                    if (Vector3.Distance(targetDestination, transform.position) <= stoppingDistance + agent.stoppingDistance)
                    {
                        yield return interWaypointWait;
                        numberofWaypointsReached++;
                        // Determines whether all waypoints have been reached and updates destination accordingly
                        if (numberofWaypointsReached >= roamingWaypoints.Length)
                        {
                            currentState = AIState.Exiting;
                            targetDestination = WaypointManager.instance.ExitWaypoint;
                        }
                        else
                        {
                            targetDestination = roamingWaypoints[numberofWaypointsReached];
                        }
                    }
                    break;
                case AIState.Exiting:
                    if (Vector3.Distance(targetDestination, transform.position) <= stoppingDistance + agent.stoppingDistance)
                    {
                        // Setting the cat to inactive will automatically recycle in back into the pool for spawning
                        gameObject.SetActive(false);
                        GameManager.instance.UpdateScore(-escapePenalty);
                    }
                    break;
                case AIState.EatingTreat:
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
                    voiceSource.PlayOneShot(roamingSounds[Random.Range(0, roamingSounds.Length)], roamingSoundVolume);
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
        SetMoodImage();
        voiceSource.PlayOneShot(fedSound, fedSoundVolume);

        if (currentHealth <= 0)
        {
            currentState = AIState.EatingTreat;
            GameManager.instance.UpdateScore(feedingScore);
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetTrigger(eatingTreatTriggerHash);
            }
            Invoke(nameof(HideMoodCanvas), hideMoodCanvasWhenFullDelay);
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

    void HideMoodCanvas()
    {
        if (moodCanvas != null)
        {
            moodCanvas.SetActive(false);
        }
    }

    public enum AIState
    {
        Roaming,
        Exiting,
        EatingTreat,
        ReturningHome
    }
}
