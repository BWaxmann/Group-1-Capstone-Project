using System.Collections.Generic;
using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    [SerializeField]
    GameObject[] objectPrefabs;

    [SerializeField]
    [Min(0)]
    int initialPoolSize;

    List<GameObject> objectPool = new List<GameObject>();

    void Start()
    {
        if (objectPrefabs == null || objectPrefabs.Length == 0)
        {
            Debug.LogError("No object prefabs provided for pool.");
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject obj = SpawnNewObjectForPool();
            obj.SetActive(false);
        }
    }

    public GameObject GetObjectFromPool()
    {
        RandomisePoolOrder();
        foreach (GameObject obj in objectPool)
        {
            if (!obj.activeSelf)
            {
                return obj;
            }
        }
        return SpawnNewObjectForPool();
    }

    public T GetObjectFromPool<T>() where T : MonoBehaviour
    {
        return GetObjectFromPool().GetComponent<T>();
    }

    GameObject SpawnNewObjectForPool()
    {
        int chosenPrefab = Random.Range(0, objectPrefabs.Length);
        GameObject obj = Instantiate(objectPrefabs[chosenPrefab], transform);
        objectPool.Add(obj);
        return obj;
    }

    void RandomisePoolOrder()
    {
        objectPool.Sort(delegate (GameObject x, GameObject y)
        {
            return Random.Range(-1, 2);
        });
    }
}
