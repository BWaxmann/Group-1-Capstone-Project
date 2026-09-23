using UnityEngine;

public class NinjaWaypoints : MonoBehaviour
{
    Transform[] _waypoints;

    public Transform[] waypoints => _waypoints;

    void Awake()
    {
        _waypoints = transform.GetComponentsInChildren<Transform>();
    }
}
