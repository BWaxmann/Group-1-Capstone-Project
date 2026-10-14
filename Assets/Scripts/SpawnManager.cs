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

    void Start()
    {
        GameManager.instance.StartNewRound += StartSpawningWaves;
    }

    public void StartSpawningWaves(int round)
    {
        foreach (SpawnData data in spawnRoundsData[round - 1].spawnWaves)
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
            for (int j = 0; j < data.numberToSpawnAtATime; j++)
            {
                CatMovement cat = data.onlySpawnSpecificVariations
                    ? catSpawner.GetRandomObjectFromPoolFromGivenVariations<CatMovement>(data.variationsToSpawn)
                    : catSpawner.GetRandomObjectFromPool<CatMovement>();
                if (cat != null)
                {
                    cat.gameObject.SetActive(true);
                    Vector3 spawnPosition = spawnPointCentre.position + spawnPointCentre.right * Random.Range(-spawnPointRadius, spawnPointRadius);
                    cat.Initialise(afterFeedingReturnPoint, spawnPosition);
                }
            }

            yield return wait;
        }
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

        public SpawnData[] spawnWaves => _spawnWaves;
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