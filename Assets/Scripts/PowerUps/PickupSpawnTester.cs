using UnityEngine;

public class PickupSpawnTester : MonoBehaviour
{
    [SerializeField] private int pickupIndex = 1;
    [SerializeField] private Transform spawnPoint;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (PickupManager.instance == null)
            {
                Debug.LogError("PickupManager.instance is missing!");
                return;
            }

            Vector3 spawnPosition =
                spawnPoint != null ? spawnPoint.position : transform.position;

            PickupManager.instance.SpawnPickup(
                pickupIndex,
                spawnPosition
            );

            Debug.Log("Test pickup spawned.");
        }
    }
}