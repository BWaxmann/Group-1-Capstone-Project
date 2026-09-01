using UnityEngine;

public class PickupManager : MonoBehaviour
{
    [SerializeField]
    ObjectSpawner pickupSpawner;

    [SerializeField]
    Transform playerVisionPoint;

    public static PickupManager instance;

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
            obj.transform.SetPositionAndRotation(position, Quaternion.LookRotation(playerVisionPoint.position - position));
            obj.GetComponent<Pickup>().Initialise();
        }
    }
}
