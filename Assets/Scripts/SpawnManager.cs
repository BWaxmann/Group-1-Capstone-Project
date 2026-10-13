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

    [Header("Finale Rush")]

    [SerializeField]
    [Min(0)]
    [Tooltip("Extra cats this spawner sends in once the finale starts.")]
    int finaleSpawnCount = 4;

    [SerializeField]
    [Min(0)]
    float finaleDelayBetweenSpawns = 3f;

    public bool allWavesCompleted { get { return wavesCompleted >= spawnWavesData.Length; } }

    int wavesCompleted = 0;
    bool finaleDone;

    void Start()
    {
        GameManager.instance.GameStart += StartSpawningWaves;
        GameManager.instance.FinaleStart += StartFinaleRush;
        GameManager.instance.GameEnd += StopSpawning;
    }

    void OnDestroy()
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.GameStart -= StartSpawningWaves;
            GameManager.instance.FinaleStart -= StartFinaleRush;
            GameManager.instance.GameEnd -= StopSpawning;
        }
    }

    void StartFinaleRush()
    {
        if (finaleSpawnCount > 0 && isActiveAndEnabled)
        {
            StartCoroutine(FinaleRush());
        }
    }

    IEnumerator FinaleRush()
    {
        WaitForSeconds wait = new WaitForSeconds(finaleDelayBetweenSpawns);
        for (int i = 0; i < finaleSpawnCount; i++)
        {
            SpawnCat(catSpawner.GetRandomObjectFromPool<CatMovement>());
            yield return wait;
        }
        finaleDone = true;

        if (allWavesCompleted)
        {
            SpawnerShutDown();
        }
    }

    void StopSpawning()
    {
        StopAllCoroutines();
    }

    void SpawnCat(CatMovement cat)
    {
        if (cat != null)
        {
            cat.gameObject.SetActive(true);
            Vector3 spawnPosition = spawnPointCentre.position + spawnPointCentre.right * Random.Range(-spawnPointRadius, spawnPointRadius);
            cat.Initialise(afterFeedingReturnPoint, spawnPosition);
        }
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

        WaitForSeconds wait = new WaitForSeconds(data.delayBetweenSpawns);
        for (int i = 0; i < data.numberOfTimesToSpawn; i++)
        {
            CatMovement cat = data.onlySpawnSpecificVariations
                ? catSpawner.GetRandomObjectFromPoolFromGivenVariations<CatMovement>(data.variationsToSpawn)
                : catSpawner.GetRandomObjectFromPool<CatMovement>();
            SpawnCat(cat);

            yield return wait;
        }

        wavesCompleted++;
        // Stay active until the finale rush has run, or it would be cut off
        if (wavesCompleted >= spawnWavesData.Length && (finaleSpawnCount == 0 || finaleDone))
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
        [Min(0)]
        float _delayBetweenSpawns;

        [SerializeField]
        bool _onlySpawnSpecificVariations;

        [SerializeField]
        int[] _variationsToSpawn;

        public float secondsAfterStartToBegin { get { return _secondsAfterStartToBegin; } }
        public int numberOfTimesToSpawn { get { return _numberOfTimesToSpawn; } }
        public float delayBetweenSpawns { get { return _delayBetweenSpawns; } }
        public bool onlySpawnSpecificVariations { get { return _onlySpawnSpecificVariations; } }
        public int[] variationsToSpawn { get { return _variationsToSpawn; } }
    }
}