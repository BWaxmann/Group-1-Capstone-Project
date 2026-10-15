using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class ObjectSpawner : MonoBehaviour
{
    [SerializeField]
    ObjectVariation[] objectsInPool;

    [SerializeField]
    [Min(0)]
    float initialPoolSpawnDelay;

    [SerializeField]
    [Min(0)]
    float maxPoolSize = 100;

    List<GameObject>[] objectPools;
    List<int> variationsForRandomPick = new List<int>();

    void Start()
    {
        if (objectsInPool == null || objectsInPool.Length == 0)
        {
            Debug.LogError("No object prefabs provided for pool.", this);
            return;
        }

        StartCoroutine(SpawnInitial());
    }

    IEnumerator SpawnInitial()
    {
        yield return new WaitForSeconds(initialPoolSpawnDelay);

        objectPools = new List<GameObject>[objectsInPool.Length];
        variationsForRandomPick.Clear();
        for (int i = 0; i < objectsInPool.Length; i++)
        {
            objectPools[i] = new List<GameObject>();
            int iPS = objectsInPool[i].initialPoolSize;
            if (iPS > 0)
            {
                for (int j = 0; j < iPS; j++)
                {
                    GameObject obj = SpawnNewObjectForPool(i);
                    obj.SetActive(false);
                    yield return null;
                    yield return null;
                }
            }

            if (!objectsInPool[i].excludeFromRandomSelection)
            {
                variationsForRandomPick.Add(i);
            }
        }
    }

    public GameObject GetObjectFromPool(int variation)
    {
        if (variation >= objectPools.Length)
        {
            Debug.LogError($"Attempted to get invalid variation {variation}.", this);
            return null;
        }
        foreach (GameObject obj in objectPools[variation])
        {
            if (!obj.activeSelf)
            {
                return obj;
            }
        }
        if (objectPools[variation].Count < maxPoolSize)
        {
            return SpawnNewObjectForPool(variation);
        }
        return null;
    }

    public T GetObjectFromPool<T>(int variation) where T : MonoBehaviour
    {
        GameObject obj = GetObjectFromPool(variation);
        if (obj == null) return null;
        return obj.GetComponent<T>();
    }

    public GameObject GetRandomObjectFromPoolFromGivenVariations(int[] variations)
    {
        if (variations == null || variations.Length == 0)
        {
            Debug.LogError("Attempted to get variation to spawn from an empty array.", this);
            return null;
        }
        return GetObjectFromPool(GetRandomItemFromArray(variations));
    }

    public T GetRandomObjectFromPoolFromGivenVariations<T>(int[] variations) where T : MonoBehaviour
    {
        GameObject obj = GetRandomObjectFromPoolFromGivenVariations(variations);
        if (obj == null) return null;
        return obj.GetComponent<T>();
    }    

    public GameObject GetRandomObjectFromPool()
    {
        if (variationsForRandomPick == null || variationsForRandomPick.Count == 0)
        {
            Debug.LogError("Attempted to pick from empty variation list.", this);
            return null;
        }
        return GetObjectFromPool(GetRandomItemFromList(variationsForRandomPick));
    }

    public T GetRandomObjectFromPool<T>() where T : MonoBehaviour
    {
        GameObject obj = GetRandomObjectFromPool();
        if (obj == null) return null;
        return obj.GetComponent<T>();
    }

    GameObject SpawnNewObjectForPool(int variation)
    {
        GameObject obj = Instantiate(objectsInPool[variation].prefab, transform);
        objectPools[variation].Add(obj);
        return obj;
    }

    T GetRandomItemFromArray<T>(T[] items)
    {
        return items[Random.Range(0, items.Length)];
    }

    T GetRandomItemFromList<T>(List<T> list)
    {
        return list[Random.Range(0, list.Count)];
    }

    [Serializable]
    public struct ObjectVariation
    {
        [SerializeField]
        GameObject _prefab;

        [SerializeField]
        bool _excludeFromRandomSelection;

        [SerializeField]
        [Min(0)]
        int _initialPoolSize;

        public GameObject prefab { get { return _prefab; } }
        public bool excludeFromRandomSelection { get { return _excludeFromRandomSelection; } }
        public int initialPoolSize { get { return _initialPoolSize; } }
    }
}
