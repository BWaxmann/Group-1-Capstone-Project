using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class CatMovement : MonoBehaviour
{
    [SerializeField] float targetUpdateFrequency = 0.2f;
    [SerializeField] int minimumWaypointsBeforeExit = 0;
    [SerializeField] int maximumWaypointsBeforeExit = 3;
    [SerializeField] float stoppingDistance = 0.1f;

    NavMeshAgent agent;
    AIState currentState;
    AIState previousState;
    Vector3 targetDestination;
    int numberOfRoamingWaypoints;
    Vector3[] roamingWaypoints;
    int numberofWaypointsReached;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        Initialise();
        StartCoroutine(MoveToTargetRoutine());
    }

    public void Initialise()
    {
        numberOfRoamingWaypoints = Random.Range(minimumWaypointsBeforeExit, maximumWaypointsBeforeExit + 1);
        Debug.Log("Chosen number of roaming waypoints: " + numberOfRoamingWaypoints);
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
    }

    IEnumerator MoveToTargetRoutine()
    {
        WaitForSeconds updateWait = new WaitForSeconds(targetUpdateFrequency);
        while (true)
        {
            switch (currentState)
            {
                case AIState.Roaming:
                    // Checks whether target destination has been reached
                    if (Vector3.Distance(targetDestination, transform.position) <= stoppingDistance + agent.stoppingDistance)
                    {
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
                    goto case AIState.Exiting;
                case AIState.Exiting:
                    // TODO: If treat is nearby, then go to "sees treat" state; once exit is reached, despawn
                    break;
                case AIState.SeesTreat:
                    // TODO: If has arrived at treat, then begin eating treat; if treat has disappeared, resume previous behaviour
                    break;
                case AIState.EatingTreat:
                    // TODO: If treat has disappeared, then go home
                    break;
                case AIState.ReturningHome:
                    // TODO: Once home is reached, despawn
                    break;
            }

            agent.destination = targetDestination;
            yield return updateWait;
        }
    }

    public enum AIState
    {
        Roaming,
        Exiting,
        SeesTreat,
        EatingTreat,
        ReturningHome
    }
}
