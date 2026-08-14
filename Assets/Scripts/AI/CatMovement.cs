using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class CatMovement : MonoBehaviour
{
    [SerializeField] int feedingScore = 10;
    [SerializeField] int escapePenalty = 5;

    [Header("AI Behaviour")]
    [SerializeField] float targetUpdateFrequency = 0.2f;
    [SerializeField] int minimumWaypointsBeforeExit = 0;
    [SerializeField] int maximumWaypointsBeforeExit = 3;
    [SerializeField] float stoppingDistance = 0.1f;
    [SerializeField] float pauseDurationBetweenWaypoints = 0.2f;

    [Header("Animation")]
    [SerializeField] float eatingTreatPauseDuration = 1.2f;
    [SerializeField] Animator animator;
    [SerializeField] string eatingTreatAnimationTrigger = "HappyCat";

    NavMeshAgent agent;
    AIState currentState;
    Vector3 targetDestination;
    int numberOfRoamingWaypoints;
    Vector3[] roamingWaypoints;
    int numberofWaypointsReached;
    int eatingTreatTriggerHash;

    public bool isFed => currentState == AIState.EatingTreat || currentState == AIState.ReturningHome;

    void Start()
    {
        eatingTreatTriggerHash = Animator.StringToHash(eatingTreatAnimationTrigger);
    }

    void OnDisable()
    {
        StopAllCoroutines();
    }

    public void Initialise()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

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
                    yield return new WaitForSeconds(eatingTreatPauseDuration);

                    // Begin cat's return home
                    agent.isStopped = false;
                    currentState = AIState.ReturningHome;
                    targetDestination = WaypointManager.instance.HomeWaypoint;
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

    public void FeedCat()
    {
        if (isFed) return;

        currentState = AIState.EatingTreat;
        GameManager.instance.UpdateScore(feedingScore);
        if (animator != null)
        {
            animator.SetTrigger(eatingTreatTriggerHash);
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
