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
    [Tooltip("Seperate first by round, then waves within the round.")]
    RoundData[] spawnRoundsData;

    // Waves from scenes made before rounds existed (our prototypes). They play in round 1 when no rounds are set.
    [SerializeField]
    [HideInInspector]
    SpawnData[] spawnWavesData;

    [Header("Finale Rush")]

    [SerializeField]
    [Min(0)]
    [Tooltip("Extra cats this spawner sends in once the finale starts.")]
    int finaleSpawnCount = 4;

    [SerializeField]
    [Min(0)]
    float finaleDelayBetweenSpawns = 3f;

    // While true, every spawner waits before sending its next cat (Cat Chaos pauses waves during its boss phase)
    public static bool holdSpawns;

    bool wavesStarted;

    void Start()
    {
        GameManager.instance.StartNewRound += StartSpawningWaves;
        GameManager.instance.FinaleStart += StartFinaleRush;
        GameManager.instance.GameEnd += StopSpawning;
    }

    void OnDestroy()
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.StartNewRound -= StartSpawningWaves;
            GameManager.instance.FinaleStart -= StartFinaleRush;
            GameManager.instance.GameEnd -= StopSpawning;
        }
    }

    public void StartSpawningWaves(int round)
    {
        // Switched off (e.g. by a prototype scene that spawns its own cats)
        if (!enabled || !gameObject.activeInHierarchy)
        {
            return;
        }
        SpawnData[] waves = WavesForRound(round);
        // No waves set up for this round
        if (waves == null)
        {
            return;
        }

        wavesStarted = true;
        foreach (SpawnData data in waves)
        {
            StartCoroutine(SpawnWaveRoutine(data));
        }
    }

    SpawnData[] WavesForRound(int round)
    {
        if (spawnRoundsData != null && spawnRoundsData.Length > 0)
        {
            if (round >= 1 && round <= spawnRoundsData.Length)
            {
                return spawnRoundsData[round - 1].spawnWaves;
            }
            return null;
        }
        if (round == 1)
        {
            return spawnWavesData;
        }
        return null;
    }

    IEnumerator SpawnWaveRoutine(SpawnData data)
    {
        // Wait for delay before starting wave
        yield return new WaitForSeconds(data.secondsAfterStartToBegin);

        WaitForSeconds wait = new WaitForSeconds(data.delayBetweenSpawns);
        for (int i = 0; i < data.numberOfTimesToSpawn; i++)
        {
            while (holdSpawns)
            {
                yield return null;
            }
            // Older waves were saved before this setting existed (it loads as 0): they send one at a time
            for (int j = 0; j < Mathf.Max(1, data.numberToSpawnAtATime); j++)
            {
                CatMovement cat;
                if (data.onlySpawnSpecificVariations)
                {
                    cat = catSpawner.GetRandomObjectFromPoolFromGivenVariations<CatMovement>(data.variationsToSpawn);
                }
                else
                {
                    cat = catSpawner.GetRandomObjectFromPool<CatMovement>();
                }
                SpawnCat(cat);
            }

            yield return wait;
        }
    }

    void StartFinaleRush()
    {
        if (finaleSpawnCount > 0 && isActiveAndEnabled && wavesStarted)
        {
            StartCoroutine(FinaleRushRoutine());
        }
    }

    IEnumerator FinaleRushRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(finaleDelayBetweenSpawns);
        for (int i = 0; i < finaleSpawnCount; i++)
        {
            while (holdSpawns)
            {
                yield return null;
            }
            SpawnCat(catSpawner.GetRandomObjectFromPool<CatMovement>());
            yield return wait;
        }
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

    void StopSpawning()
    {
        StopAllCoroutines();
    }

    public void EndRound()
    {
        StopAllCoroutines();
    }

    [Serializable]
    public struct RoundData
    {
        [SerializeField]
        SpawnData[] _spawnWaves;

        public SpawnData[] spawnWaves
        {
            get
            {
                return _spawnWaves;
            }
        }
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
        int _numberToSpawnAtATime;

        [SerializeField]
        [Min(0)]
        float _delayBetweenSpawns;

        [SerializeField]
        bool _onlySpawnSpecificVariations;

        [SerializeField]
        int[] _variationsToSpawn;

        public float secondsAfterStartToBegin { get { return _secondsAfterStartToBegin; } }
        public int numberOfTimesToSpawn { get { return _numberOfTimesToSpawn; } }
        public int numberToSpawnAtATime { get { return _numberToSpawnAtATime; } }
        public float delayBetweenSpawns { get { return _delayBetweenSpawns; } }
        public bool onlySpawnSpecificVariations { get { return _onlySpawnSpecificVariations; } }
        public int[] variationsToSpawn { get { return _variationsToSpawn; } }
    }
}
