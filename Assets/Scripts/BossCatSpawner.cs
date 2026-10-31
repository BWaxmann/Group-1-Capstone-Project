using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class BossCatSpawner : MonoBehaviour
{
    [SerializeField]
    BossData[] bossPrefabs;

    [SerializeField]
    [Tooltip("Time is from the start of each round.")]
    List<float> bossSpawnTimes = new List<float>();

    [SerializeField]
    AudioSource bossSpawnSoundSource;

    // A boss prefab of this kind from the list (e.g. Cat Cafe Waves' Mafia heist borrows the Mafia cat), or null
    public T Prefab<T>() where T : BossCat
    {
        if (bossPrefabs == null)
        {
            return null;
        }
        foreach (BossData boss in bossPrefabs)
        {
            if (boss.bossPrefab is T found)
            {
                return found;
            }
        }
        return null;
    }

    List<BossCat> bossInstances = new List<BossCat>();
    List<Transform> bossSpawnPoints = new List<Transform>();
    List<SoundData> bossSpawnSounds = new List<SoundData>();

    void Start()
    {
        if (bossSpawnTimes.Count == 0)
        {
            gameObject.SetActive(false);
            return;
        }
        else if (bossSpawnTimes.Count > 1)
        {
            bossSpawnTimes.Sort();
        }

        StartCoroutine(InitialiseRoutine());

        GameManager.instance.StartNewRound += StartSpawnRoutine;
    }

    IEnumerator InitialiseRoutine()
    {
        yield return null;

        foreach (BossData boss in bossPrefabs)
        {
            if (boss.bossPrefab != null)
            {
                BossCat instance = Instantiate(boss.bossPrefab, transform);
                instance.gameObject.SetActive(false);
                bossInstances.Add(instance);
                bossSpawnPoints.Add(boss.spawnPoint);
                bossSpawnSounds.Add(boss.soundOnSpawn);
                yield return null;
                yield return null;
            }
        }

        if (bossInstances.Count == 0)
        {
            gameObject.SetActive(false);
        }
    }

    public void StartSpawnRoutine(int round)
    {
        // No boss set for this round (or the spawner has been switched off)
        if (!isActiveAndEnabled || round < 1 || round > bossSpawnTimes.Count)
        {
            return;
        }

        StartCoroutine(SpawnRoutine(round));
    }

    IEnumerator SpawnRoutine(int round)
    {
        yield return new WaitForSeconds(bossSpawnTimes[round - 1]);

        if (bossInstances.Count == 0)
        {
            yield break;
        }

        int index = Random.Range(0, bossInstances.Count);

        BossCat boss = bossInstances[index];
        boss.gameObject.SetActive(true);

        Transform spawnPoint = bossSpawnPoints[index];
        boss.Initialise(spawnPoint, spawnPoint.position);

        if (bossSpawnSoundSource != null)
        {
            bossSpawnSounds[index].PlayOneShot(bossSpawnSoundSource);
        }

        bossInstances.RemoveAt(index);
        bossSpawnPoints.RemoveAt(index);
        bossSpawnSounds.RemoveAt(index);
    }

    [Serializable]
    public struct BossData
    {
        [SerializeField]
        BossCat _bossPrefab;

        [SerializeField]
        Transform _spawnPoint;

        [SerializeField]
        SoundData _soundOnSpawn;

        public BossCat bossPrefab
        {
            get
            {
                return _bossPrefab;
            }
        }

        public Transform spawnPoint
        {
            get
            {
                return _spawnPoint;
            }
        }

        public SoundData soundOnSpawn
        {
            get
            {
                return _soundOnSpawn;
            }
        }
    }
}
