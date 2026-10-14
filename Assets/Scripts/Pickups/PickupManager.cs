using UnityEngine;

public class PickupManager : MonoBehaviour
{
    [SerializeField]
    ObjectSpawner pickupSpawner;

    public static PickupManager instance;

    Transform playerVisionPoint;

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
        if (pickupSpawner == null)
        {
            pickupSpawner = GetComponent<ObjectSpawner>();
        }
    }

    public void SpawnPickup(int pickupIndex, Vector3 position)
    {
        GameObject obj = pickupSpawner.GetObjectFromPool(pickupIndex);
        if (obj != null)
        {
            obj.SetActive(true);
            Vector3 lookAtPosition = playerVisionPoint.position;
            lookAtPosition.y = position.y;
            obj.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAtPosition - position));
            obj.GetComponent<Pickup>().Initialise();
        }
    }

    public void SetPlayerVisionPoint(Transform point)
    {
        playerVisionPoint = point;
    }
}
