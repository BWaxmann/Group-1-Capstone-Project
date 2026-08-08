using System.Collections.Generic;
using UnityEngine;

public class WaypointManager : MonoBehaviour
{
    public static WaypointManager instance;

    [SerializeField] Transform[] roamingWaypoints;
    [SerializeField] Transform exitWaypoint;
    [SerializeField] Transform homeWaypoint;

    public Vector3 ExitWaypoint { get { return exitWaypoint.position; } }
    public Vector3 HomeWaypoint { get { return homeWaypoint.position; } }

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
}
