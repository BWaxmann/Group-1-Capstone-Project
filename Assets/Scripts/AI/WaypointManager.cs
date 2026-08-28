using System.Collections.Generic;
using UnityEngine;

public class WaypointManager : MonoBehaviour
{
    public static WaypointManager instance;

    [SerializeField] Transform[] roamingWaypoints;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public Vector3[] GetListOfRandomWaypoints(int number)
    {
        Vector3[] output = new Vector3[number];
        List<int> chosedIndices = new List<int>();
        int l = roamingWaypoints.Length;

        for (int i = 0; i < number; i++)
        {
            if (chosedIndices.Count >= l)
            {
                chosedIndices.Clear();
            }

            while (true)
            {
                int index = Random.Range(0, l);
                if (!chosedIndices.Contains(index))
                {
                    output[i] = roamingWaypoints[index].position;
                    chosedIndices.Add(index);
                    break;
                }
            }
        }

        return output;
    }

    public Vector3 GetRandomWaypoint()
    {
        return roamingWaypoints[Random.Range(0, roamingWaypoints.Length)].position;
    }

    public Vector3 GetNewWaypoint(Vector3 previousWaypoint)
    {
        if (roamingWaypoints == null || roamingWaypoints.Length == 0) return Vector3.zero;
        if (roamingWaypoints.Length == 1) return roamingWaypoints[0].position;

        int attempts = 0;
        while (attempts < 10)
        {
            Vector3 output = GetRandomWaypoint();
            if (Vector3.Distance(output, previousWaypoint) < 0.1f)
            {
                attempts++;
            }
            else
            {
                return output;
            }
        }
        return previousWaypoint;
    }
}
