using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class WaypointManager : MonoBehaviour
{
    public static WaypointManager instance;

    [SerializeField] Waypoint[] roamingWaypoints;

    List<Vector3> frontWaypoints = new List<Vector3>();

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

    void Start()
    {
        foreach (Waypoint waypoint in roamingWaypoints)
        {
            if (waypoint.inFrontOfPlayer)
            {
                frontWaypoints.Add(waypoint.waypoint.position);
            }
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
                    output[i] = roamingWaypoints[index].waypoint.position;
                    chosedIndices.Add(index);
                    break;
                }
            }
        }

        return output;
    }

    public Vector3 GetRandomWaypoint()
    {
        if (roamingWaypoints == null || roamingWaypoints.Length == 0) return Vector3.zero;
        if (roamingWaypoints.Length == 1) return roamingWaypoints[0].waypoint.position;
        return roamingWaypoints[Random.Range(0, roamingWaypoints.Length)].waypoint.position;
    }

    public Vector3 GetNewWaypoint(Vector3 previousWaypoint)
    {
        if (roamingWaypoints == null || roamingWaypoints.Length == 0) return Vector3.zero;
        if (roamingWaypoints.Length == 1) return roamingWaypoints[0].waypoint.position;

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

    public Vector3 GetRandomWaypointInFrontOfPlayer()
    {
        if (frontWaypoints.Count == 0) return GetRandomWaypoint();
        if (frontWaypoints.Count == 1) return frontWaypoints[0];
        return frontWaypoints[Random.Range(0, frontWaypoints.Count)];
    }

    public Vector3 GetNewWaypointInFrontOfPlayer(Vector3 previousWaypoint)
    {
        if (frontWaypoints.Count == 0) return GetNewWaypoint(previousWaypoint);
        if (frontWaypoints.Count == 1) return frontWaypoints[0];

        int attempts = 0;
        while (attempts < 10)
        {
            Vector3 output = GetRandomWaypointInFrontOfPlayer();
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

    [Serializable]
    public struct Waypoint
    {
        [SerializeField]
        Transform _waypoint;

        [SerializeField]
        bool _inFrontOfPlayer;

        public Transform waypoint => _waypoint;
        public bool inFrontOfPlayer => _inFrontOfPlayer;
    }
}
