using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager instance;

    [SerializeField] 
    CatMovement[] catPrefabs;

    [SerializeField] 
    int initialPoolSize;

    [Tooltip("Cat spawning rotation will be taken from the spawn point transform.")] 
    [SerializeField] 
    Transform spawnPointCentre;

    [SerializeField] 
    float spawnPointRadius;

    [SerializeField]
    SpawnData[] spawnWavesData;

    public bool allWavesCompleted { get { return wavesCompleted >= spawnWavesData.Length; } }

    int wavesCompleted = 0;
    List<CatMovement> catPool = new List<CatMovement>();

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
        if (catPrefabs == null || catPrefabs.Length == 0)
        {
            Debug.LogError("No cat prefabs provided to spawner.");
            return;
        }

        // Create object pool for cats
        for (int i = 0; i < initialPoolSize; i++)
        {
            CatMovement spawnedCat = SpawnNewCatForPool();
            spawnedCat.gameObject.SetActive(false);
        }

        // Start timers for spawn waves
        foreach (SpawnData data in spawnWavesData)
        {
            StartCoroutine(SpawnWave(data));
        }
    }

    IEnumerator SpawnWave(SpawnData data)
    {
        // Wait for delay before starting wave
        yield return new WaitForSeconds(data.secondsAfterStartToBegin);

        for (int i = 0; i < data.numberOfTimesToSpawn; i++)
        {
            int numberToSpawn = Random.Range(data.minimumNumberToSpawnAtATime, data.maximumNumberToSpawnAtATime + 1);
            for (int j = 0; j < numberToSpawn; j++)
            {
                CatMovement cat = GetCatFromPool();
                cat.gameObject.SetActive(true);
                Vector3 spawnPosition = spawnPointCentre.position + spawnPointCentre.right * Random.Range(-spawnPointRadius, spawnPointRadius);
                cat.transform.SetPositionAndRotation(spawnPosition, spawnPointCentre.rotation);
                cat.Initialise();
            }

            yield return new WaitForSeconds(Random.Range(data.minimumDelayBetweenSpawns, data.maximumDelayBetweenSpawns));
        }
    }

    CatMovement GetCatFromPool()
    {
        RandomisePoolOrder();
        foreach (CatMovement cat in catPool)
        {
            if (!cat.gameObject.activeSelf)
            {
                return cat;
            }
        }
        return SpawnNewCatForPool();
    }

    CatMovement SpawnNewCatForPool()
    {
        int chosenPrefab = Random.Range(0, catPrefabs.Length);
        CatMovement cat = Instantiate(catPrefabs[chosenPrefab], transform);
        catPool.Add(cat);
        return cat;
    }

    void RandomisePoolOrder()
    {
        catPool.Sort(delegate (CatMovement x, CatMovement y)
        {
            return Random.Range(-1, 2);
        });
    }

    [Serializable]
    public struct SpawnData
    {
        [SerializeField]
        [Min(0)]
        float _secondsAfterStartToBegin;

        [SerializeField]
        [Min(1)]
        int _numberOfTimesToSpawn;

        [SerializeField]
        [Min(1)]
        int _minimumNumberToSpawnAtATime;

        [SerializeField]
        [Min(1)]
        int _maximumNumberToSpawnAtATime;

        [SerializeField]
        [Min(0)]
        float _minimumDelayBetweenSpawns;

        [SerializeField]
        [Min(0)]
        float _maximumDelayBetweenSpawns;

        public float secondsAfterStartToBegin { get { return _secondsAfterStartToBegin; } }
        public int numberOfTimesToSpawn { get { return _numberOfTimesToSpawn; } }
        public int minimumNumberToSpawnAtATime { get { return _minimumNumberToSpawnAtATime; } }
        public int maximumNumberToSpawnAtATime { get { return _maximumNumberToSpawnAtATime; } }
        public float minimumDelayBetweenSpawns { get { return _minimumDelayBetweenSpawns; } }
        public float maximumDelayBetweenSpawns { get { return _maximumDelayBetweenSpawns; } }
    }
}