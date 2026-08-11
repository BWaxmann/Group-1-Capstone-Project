using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class CatMovement : MonoBehaviour
{
    [SerializeField] float targetUpdateFrequency = 0.2f;
    [SerializeField] int minimumWaypointsBeforeExit = 0;
    [SerializeField] int maximumWaypointsBeforeExit = 3;
    [SerializeField] float stoppingDistance = 0.1f;
    [SerializeField] float pauseDurationBetweenWaypoints = 0.2f;

    NavMeshAgent agent;
    AIState currentState;
    Vector3 targetDestination;
    int numberOfRoamingWaypoints;
    Vector3[] roamingWaypoints;
    int numberofWaypointsReached;

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
                    }
                    break;
                case AIState.EatingTreat:
                    // TODO: After short animation, then go home
                    break;
                case AIState.ReturningHome:
                    goto case AIState.Exiting;
            }

            agent.destination = targetDestination;
            yield return updateWait;
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
