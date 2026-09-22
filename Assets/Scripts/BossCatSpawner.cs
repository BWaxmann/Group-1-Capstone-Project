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
    List<float> bossSpawnTimes = new List<float>();

    List<BossCat> bossInstances = new List<BossCat>();
    List<Transform> bossSpawnPoints = new List<Transform>();

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

        foreach (BossData boss in bossPrefabs)
        {
            if (boss.bossPrefab != null)
            {
                BossCat instance = Instantiate(boss.bossPrefab, transform);
                instance.gameObject.SetActive(false);
                bossInstances.Add(instance);
                bossSpawnPoints.Add(boss.spawnPoint);
            }
        }

        if (bossInstances.Count == 0)
        {
            gameObject.SetActive(false);
            return;
        }

        GameManager.instance.GameStart += StartSpawnRoutine;
    }

    public void StartSpawnRoutine()
    {
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        for (int i = 0; i < bossSpawnTimes.Count; i++)
        {
            if (i == 0)
            {
                yield return new WaitForSeconds(bossSpawnTimes[0]);
            }
            else
            {
                yield return new WaitForSeconds(bossSpawnTimes[i] - bossSpawnTimes[i - 1]);
            }

            int index = Random.Range(0, bossInstances.Count);

            BossCat boss = bossInstances[index];
            boss.gameObject.SetActive(true);

            Transform spawnPoint = bossSpawnPoints[index];
            boss.Initialise(spawnPoint, spawnPoint.position);

            bossInstances.RemoveAt(index);
            bossSpawnPoints.RemoveAt(index);
        }

        GameManager.instance.GameStart -= StartSpawnRoutine;
    }

    [Serializable]
    public struct BossData
    {
        [SerializeField]
        BossCat _bossPrefab;

        [SerializeField]
        Transform _spawnPoint;

        public BossCat bossPrefab => _bossPrefab;
        public Transform spawnPoint => _spawnPoint;
    }
}
