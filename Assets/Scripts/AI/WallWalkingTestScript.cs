using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class WallWalkingTestScript : MonoBehaviour
{
    [SerializeField]
    Transform visibleCat;

    [SerializeField]
    Vector3 roomCentre;

    [SerializeField]
    float roomRadius = 8.7f;

    [SerializeField]
    float wallBottomY = 0.197f;

    [SerializeField]
    float wallStartAngle = 44.84f;

    [SerializeField]
    Vector3 analogueLowerLeft = new Vector3(20.49145f, -5f, 2.55f);

    NavMeshAgent agent;
    Vector3 destination;
    NavMeshPath path;
    float wSARadian;

    void Start()
    {
        wSARadian = wallStartAngle * Mathf.Deg2Rad;

        agent = GetComponent<NavMeshAgent>();
        destination = GetRandomNearbyPoint();
        agent.destination = destination;
        StartCoroutine(MoveRoutine());
    }

    void FixedUpdate()
    {
        if (visibleCat != null)
        {
            // TODO: move visible cat to analogous position on wall
            Vector3 analoguePosition = transform.position;
            Vector3 visiblePosition = Vector3.zero;
            visiblePosition.y = wallBottomY + Mathf.Abs(analoguePosition.z - analogueLowerLeft.z);
            float angle = wSARadian + Mathf.Abs(analoguePosition.x - analogueLowerLeft.x) / roomRadius;
            visiblePosition.x = roomRadius * Mathf.Sin(angle);
            visiblePosition.z = roomRadius * Mathf.Cos(angle);
            Vector3 direction = visiblePosition;
            direction.y = 0;
            direction.Normalize();
            visiblePosition += roomCentre;
            Debug.DrawLine(visiblePosition - direction, visiblePosition + direction, Color.red, 1f);
            if (Physics.Raycast(visiblePosition - direction, direction * 2f, out RaycastHit hit, 5f))
            {
                visibleCat.position = hit.point;
                visibleCat.rotation = Quaternion.RotateTowards(visibleCat.rotation, 
                    Quaternion.Euler(transform.eulerAngles.y + 90, Vector3.SignedAngle(Vector3.right, hit.normal, Vector3.up), -90), 
                    360 * Time.fixedDeltaTime);
            }
            else
            {
                visibleCat.position = visiblePosition;
                visibleCat.rotation = Quaternion.RotateTowards(visibleCat.rotation,
                    Quaternion.Euler(transform.eulerAngles.y + 90, angle * Mathf.Rad2Deg + 90, -90),
                    360 * Time.fixedDeltaTime);
            }
        }
    }

    IEnumerator MoveRoutine()
    {
        yield return null;

        WaitForSeconds wait = new WaitForSeconds(0.1f);
        while (true)
        {
            if (Vector3.Distance(transform.position, destination) < 1f)
            {
                destination = GetRandomNearbyPoint();
                agent.destination = destination;
            }

            yield return wait;
        }
    }

    Vector3 GetRandomNearbyPoint()
    {
        int tries = 0;
        while (tries < 10)
        {
            Vector2 randomPoint = Random.insideUnitCircle * 10f;
            Vector3 testPoint = transform.position + new Vector3(randomPoint.x, 0, randomPoint.y);
            if (path == null)
            {
                path = new NavMeshPath();
            }
            if (NavMesh.SamplePosition(testPoint, out NavMeshHit hit, 4f, NavMesh.AllAreas)
                && agent.CalculatePath(testPoint, path))
            {
                return hit.position;
            }
            tries++;
        }
        path = null;
        return transform.position;
    }
}
