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

    List<CatMovement> bossInstances = new List<CatMovement>();
    List<Transform> bossSpawnPoints = new List<Transform>();

    void Start()
    {
        if (bossSpawnTimes.Count == 0)
        {
            gameObject.SetActive(false);
            return;
        }
        bossSpawnTimes.Sort();

        foreach (BossData boss in bossPrefabs)
        {
            if (boss.bossPrefab != null)
            {
                CatMovement instance = Instantiate(boss.bossPrefab, transform);
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

            CatMovement boss = bossInstances[index];
            boss.gameObject.SetActive(true);

            Transform spawnPoint = bossSpawnPoints[index];
            boss.Initialise(spawnPoint, spawnPoint.position);

            bossInstances.RemoveAt(index);
            bossSpawnPoints.RemoveAt(index);
        }

        GameManager.instance.GameStart -= StartSpawnRoutine;
        gameObject.SetActive(false);
    }

    [Serializable]
    public struct BossData
    {
        [SerializeField]
        CatMovement _bossPrefab;

        [SerializeField]
        Transform _spawnPoint;

        public CatMovement bossPrefab => _bossPrefab;
        public Transform spawnPoint => _spawnPoint;
    }
}
