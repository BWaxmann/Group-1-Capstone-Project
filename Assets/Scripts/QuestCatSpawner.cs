using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class QuestCatSpawner : MonoBehaviour
{
    [Header("CAT PREFAB")]

    [Tooltip("Drag the actual Cat prefab here. This completely bypasses ObjectSpawner.")]
    [SerializeField]
    GameObject catPrefab;


    [Header("SPAWN POINT")]

    [Tooltip("Centre point used for spawning cats.")]
    [SerializeField]
    Transform spawnPointCentre;

    [Tooltip("Maximum distance left/right from the spawn point.")]
    [SerializeField]
    float spawnPointRadius = 0f;

    [Tooltip("Where cats should return after feeding.")]
    [SerializeField]
    Transform afterFeedingReturnPoint;


    [Header("SPAWNING")]

    [Tooltip("Spawn this many cats immediately when the scene starts.")]
    [SerializeField]
    int catsToSpawnImmediately = 1;

    [Tooltip("Continue spawning cats automatically.")]
    [SerializeField]
    bool continuouslySpawn = true;

    [Tooltip("Time between automatic spawns.")]
    [SerializeField]
    float secondsBetweenSpawns = 5f;

    [Tooltip("Maximum number of cats allowed to exist at once.")]
    [SerializeField]
    int maximumCatsAlive = 20;


    [Header("DEBUG")]

    [SerializeField]
    bool enableDebugLogs = true;


    List<GameObject> spawnedCats =
        new List<GameObject>();


    bool hasStarted = false;


    void Awake()
    {
        DebugLog("AWAKE - QuestCatSpawner loaded.");

        ValidateSetup();
    }


    void Start()
    {
        DebugLog("START - QuestCatSpawner starting.");

        if (!ValidateSetup())
        {
            Debug.LogError(
                "[QUEST CAT SPAWNER] SETUP INVALID. SPAWNING ABORTED.",
                this
            );

            return;
        }


        // Spawn cats immediately.
        if (catsToSpawnImmediately > 0)
        {
            DebugLog(
                "Immediately spawning " +
                catsToSpawnImmediately +
                " cat(s)."
            );


            for (
                int i = 0;
                i < catsToSpawnImmediately;
                i++
            )
            {
                ForceSpawnCat();
            }
        }


        // Start continuous spawning.
        if (continuouslySpawn)
        {
            DebugLog(
                "Starting continuous spawning."
            );

            StartCoroutine(
                ContinuousSpawnRoutine()
            );
        }


        hasStarted = true;
    }


    bool ValidateSetup()
    {
        bool valid = true;


        if (catPrefab == null)
        {
            Debug.LogError(
                "[QUEST CAT SPAWNER] CAT PREFAB IS NOT ASSIGNED!",
                this
            );

            valid = false;
        }
        else
        {
            DebugLog(
                "Cat prefab assigned: " +
                catPrefab.name
            );


            CatMovement movement =
                catPrefab.GetComponent<CatMovement>();


            if (movement == null)
            {
                Debug.LogError(
                    "[QUEST CAT SPAWNER] " +
                    "Cat prefab does NOT contain CatMovement!",
                    catPrefab
                );

                valid = false;
            }
            else
            {
                DebugLog(
                    "CatMovement found on prefab."
                );
            }
        }


        if (spawnPointCentre == null)
        {
            Debug.LogError(
                "[QUEST CAT SPAWNER] " +
                "SPAWN POINT CENTRE IS NOT ASSIGNED!",
                this
            );

            valid = false;
        }
        else
        {
            DebugLog(
                "Spawn point assigned: " +
                spawnPointCentre.name
            );
        }


        if (afterFeedingReturnPoint == null)
        {
            Debug.LogWarning(
                "[QUEST CAT SPAWNER] " +
                "After Feeding Return Point is not assigned.",
                this
            );
        }


        return valid;
    }


    IEnumerator ContinuousSpawnRoutine()
    {
        DebugLog(
            "Continuous spawn coroutine started."
        );


        while (true)
        {
            yield return new WaitForSeconds(
                secondsBetweenSpawns
            );


            CleanupDestroyedCats();


            if (
                maximumCatsAlive > 0 &&
                spawnedCats.Count >= maximumCatsAlive
            )
            {
                DebugLog(
                    "Maximum cats alive reached: " +
                    spawnedCats.Count +
                    "/" +
                    maximumCatsAlive
                );

                continue;
            }


            DebugLog(
                "Automatic spawn timer reached."
            );


            ForceSpawnCat();
        }
    }


    [ContextMenu("FORCE SPAWN CAT NOW")]
    public void ForceSpawnCat()
    {
        DebugLog(
            "========================================"
        );

        DebugLog(
            "FORCE SPAWN CAT CALLED"
        );

        DebugLog(
            "========================================"
        );


        if (catPrefab == null)
        {
            Debug.LogError(
                "[QUEST CAT SPAWNER] " +
                "Cannot spawn - catPrefab is NULL!",
                this
            );

            return;
        }


        if (spawnPointCentre == null)
        {
            Debug.LogError(
                "[QUEST CAT SPAWNER] " +
                "Cannot spawn - spawnPointCentre is NULL!",
                this
            );

            return;
        }


        CleanupDestroyedCats();


        if (
            maximumCatsAlive > 0 &&
            spawnedCats.Count >= maximumCatsAlive
        )
        {
            DebugLog(
                "Cannot spawn. Maximum number of cats reached."
            );

            return;
        }


        // ------------------------------------------------
        // CALCULATE SPAWN POSITION
        // ------------------------------------------------

        float randomOffset =
            Random.Range(
                -spawnPointRadius,
                spawnPointRadius
            );


        Vector3 spawnPosition =
            spawnPointCentre.position +
            spawnPointCentre.right *
            randomOffset;


        Quaternion spawnRotation =
            spawnPointCentre.rotation;


        DebugLog(
            "Spawn position: " +
            spawnPosition
        );

        DebugLog(
            "Spawn rotation: " +
            spawnRotation.eulerAngles
        );


        // ------------------------------------------------
        // DIRECT INSTANTIATE
        // ------------------------------------------------

        DebugLog(
            "ABOUT TO DIRECTLY INSTANTIATE CAT PREFAB."
        );


        GameObject catObject =
            Instantiate(
                catPrefab,
                spawnPosition,
                spawnRotation
            );


        if (catObject == null)
        {
            Debug.LogError(
                "[QUEST CAT SPAWNER] " +
                "INSTANTIATE RETURNED NULL!",
                this
            );

            return;
        }


        DebugLog(
            "INSTANTIATE SUCCESSFUL: " +
            catObject.name
        );


        // ------------------------------------------------
        // DO NOT PARENT TO SPAWNER
        // ------------------------------------------------

        // Deliberately leave this object unparented.
        // This eliminates an inactive-parent hierarchy
        // from causing activeInHierarchy problems.


        catObject.transform.position =
            spawnPosition;

        catObject.transform.rotation =
            spawnRotation;


        // ------------------------------------------------
        // FORCE ACTIVE
        // ------------------------------------------------

        DebugLog(
            "Cat activeSelf BEFORE SetActive: " +
            catObject.activeSelf
        );


        DebugLog(
            "Cat activeInHierarchy BEFORE SetActive: " +
            catObject.activeInHierarchy
        );


        catObject.SetActive(true);


        DebugLog(
            "Cat activeSelf AFTER SetActive: " +
            catObject.activeSelf
        );


        DebugLog(
            "Cat activeInHierarchy AFTER SetActive: " +
            catObject.activeInHierarchy
        );


        // ------------------------------------------------
        // GET CAT MOVEMENT
        // ------------------------------------------------

        CatMovement catMovement =
            catObject.GetComponent<CatMovement>();


        if (catMovement == null)
        {
            Debug.LogError(
                "[QUEST CAT SPAWNER] " +
                "INSTANTIATED CAT DOES NOT HAVE CatMovement!",
                catObject
            );

            return;
        }


        DebugLog(
            "CatMovement successfully found on instantiated cat."
        );


        // ------------------------------------------------
        // INITIALISE
        // ------------------------------------------------

        if (!catObject.activeInHierarchy)
        {
            Debug.LogError(
                "[QUEST CAT SPAWNER] " +
                "CAT IS STILL INACTIVE BEFORE INITIALISE! " +
                "This should NOT happen.",
                catObject
            );

            return;
        }


        DebugLog(
            "Calling CatMovement.Initialise()."
        );


        catMovement.Initialise(
            afterFeedingReturnPoint
        );


        DebugLog(
            "CatMovement.Initialise() completed."
        );


        // ------------------------------------------------
        // TRACK CAT
        // ------------------------------------------------

        spawnedCats.Add(
            catObject
        );


        DebugLog(
            "CAT SPAWN COMPLETE."
        );


        DebugLog(
            "Total spawned cats currently tracked: " +
            spawnedCats.Count
        );


        DebugLog(
            "========================================"
        );
    }


    void CleanupDestroyedCats()
    {
        for (
            int i = spawnedCats.Count - 1;
            i >= 0;
            i--
        )
        {
            if (spawnedCats[i] == null)
            {
                spawnedCats.RemoveAt(i);
            }
        }
    }


    public int GetNumberOfCatsAlive()
    {
        CleanupDestroyedCats();

        return spawnedCats.Count;
    }


    [ContextMenu("SPAWN 5 CATS NOW")]
    public void SpawnFiveCats()
    {
        DebugLog(
            "FORCING FIVE CATS TO SPAWN."
        );


        for (
            int i = 0;
            i < 5;
            i++
        )
        {
            ForceSpawnCat();
        }
    }


    [ContextMenu("SPAWN 10 CATS NOW")]
    public void SpawnTenCats()
    {
        DebugLog(
            "FORCING TEN CATS TO SPAWN."
        );


        for (
            int i = 0;
            i < 10;
            i++
        )
        {
            ForceSpawnCat();
        }
    }


    [ContextMenu("STOP AUTOMATIC SPAWNING")]
    public void StopAutomaticSpawning()
    {
        DebugLog(
            "STOPPING ALL SPAWNING COROUTINES."
        );


        StopAllCoroutines();
    }


    [ContextMenu("DESTROY ALL SPAWNED CATS")]
    public void DestroyAllSpawnedCats()
    {
        DebugLog(
            "DESTROYING ALL TRACKED CATS."
        );


        for (
            int i = spawnedCats.Count - 1;
            i >= 0;
            i--
        )
        {
            if (spawnedCats[i] != null)
            {
                Destroy(
                    spawnedCats[i]
                );
            }
        }


        spawnedCats.Clear();
    }


    void DebugLog(string message)
    {
        if (!enableDebugLogs)
        {
            return;
        }


        Debug.Log(
            "[QUEST CAT SPAWNER] " +
            message,
            this
        );
    }
}