using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class SpawnManager : MonoBehaviour
{
    [SerializeField]
    ObjectSpawner catSpawner;

    [Tooltip("Cat spawning rotation will be taken from the spawn point transform.")] 
    [SerializeField] 
    Transform spawnPointCentre;

    [SerializeField]
    [Min(0)]
    float spawnPointRadius;

    [SerializeField]
    Transform afterFeedingReturnPoint;

    [SerializeField]
    SpawnData[] spawnWavesData;

    public bool allWavesCompleted { get { return wavesCompleted >= spawnWavesData.Length; } }

    int wavesCompleted = 0;

    void Start()
    {
        GameManager.instance.GameStart += StartSpawningWaves;
    }

    public void StartSpawningWaves()
    {
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
                CatMovement cat = data.onlySpawnSpecificVariations 
                    ? catSpawner.GetRandomObjectFromPoolFromGivenVariations<CatMovement>(data.variationsToSpawn) 
                    : catSpawner.GetRandomObjectFromPool<CatMovement>();
                if (cat == null)
                {
                    Debug.LogError("Failed to spawn cats.", this);
                    break;
                }
                cat.gameObject.SetActive(true);
                Vector3 spawnPosition = spawnPointCentre.position + spawnPointCentre.right * Random.Range(-spawnPointRadius, spawnPointRadius);
                cat.transform.SetPositionAndRotation(spawnPosition, spawnPointCentre.rotation);
                cat.Initialise(afterFeedingReturnPoint);
            }

            yield return new WaitForSeconds(Random.Range(data.minimumDelayBetweenSpawns, data.maximumDelayBetweenSpawns));
        }

        wavesCompleted++;
        if (wavesCompleted >= spawnWavesData.Length)
        {            
            SpawnerShutDown();
        }
    }

    void SpawnerShutDown()
    {
        GameManager.instance.GameStart -= StartSpawningWaves;
        gameObject.SetActive(false);
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

        [SerializeField]
        bool _onlySpawnSpecificVariations;

        [SerializeField]
        int[] _variationsToSpawn;

        public float secondsAfterStartToBegin { get { return _secondsAfterStartToBegin; } }
        public int numberOfTimesToSpawn { get { return _numberOfTimesToSpawn; } }
        public int minimumNumberToSpawnAtATime { get { return _minimumNumberToSpawnAtATime; } }
        public int maximumNumberToSpawnAtATime { get { return _maximumNumberToSpawnAtATime; } }
        public float minimumDelayBetweenSpawns { get { return _minimumDelayBetweenSpawns; } }
        public float maximumDelayBetweenSpawns { get { return _maximumDelayBetweenSpawns; } }
        public bool onlySpawnSpecificVariations { get { return _onlySpawnSpecificVariations; } }
        public int[] variationsToSpawn { get { return _variationsToSpawn; } }
    }
}