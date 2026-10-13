using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Sends packs of peekaboo cats out of the CatHoles in the scene: 4-6 cats, one after another,
// each from a different hole, then a short break before the next pack.
// Created automatically when the level has at least one CatHole, so no scene setup is needed.
// Cat prefabs are loaded from Resources/PeekabooCats.
public class PeekabooPackSpawner : MonoBehaviour
{
    const string PrefabFolder = "PeekabooCats";

    [SerializeField]
    [Min(1)]
    int minimumPackSize = 4;

    [SerializeField]
    [Min(1)]
    int maximumPackSize = 6;

    [SerializeField]
    [Min(1)]
    [Tooltip("How many cats can be out at once. The next cat waits until one is fed or gives up, so there's always a clear target.")]
    int maxCatsOut = 1;

    [SerializeField]
    [Min(1)]
    int maxCatsOutDuringFinale = 2;

    [SerializeField]
    [Min(0)]
    [Tooltip("Short gap after a cat is fed before the next one warns.")]
    float delayBetweenCats = 0.35f;

    [SerializeField]
    [Min(0)]
    float firstPackDelay = 2f;

    [SerializeField]
    [Min(0)]
    [Tooltip("Seconds of calm after a pack finishes before the next one starts.")]
    float delayBetweenPacks = 3f;

    [SerializeField]
    [Min(0.5f)]
    [Tooltip("How long each cat stays out before ducking back in.")]
    float catStayDuration = 3.5f;

    [SerializeField]
    [Min(0.1f)]
    [Tooltip("During LAST ORDERS! the gaps between cats and packs are multiplied by this.")]
    float finaleSpeedMultiplier = 0.5f;

    [SerializeField]
    [Tooltip("Prototype setting: stops the old roaming cat waves so only peekaboo packs appear. Boss cats still come.")]
    bool turnOffRoamingWaves = true;

    public static PeekabooPackSpawner instance;

    readonly List<PeekabooCat> pool = new List<PeekabooCat>();
    PeekabooCat[] prefabs;
    float speed = 1f;
    int catsOutLimit = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateIfNeeded()
    {
        if (instance == null && CatHole.all.Count > 0 && FindObjectOfType<GameManager>() != null)
        {
            new GameObject("Peekaboo Pack Spawner").AddComponent<PeekabooPackSpawner>();
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        prefabs = Resources.LoadAll<PeekabooCat>(PrefabFolder);
        if (prefabs.Length == 0)
        {
            Debug.LogError($"[Peekaboo] No PeekabooCat prefabs found in a Resources/{PrefabFolder} folder.", this);
        }
    }

    void Start()
    {
        GameManager.instance.GameStart += BeginPacks;
        GameManager.instance.FinaleStart += SpeedUp;
        GameManager.instance.GameEnd += StopPacks;

        if (turnOffRoamingWaves)
        {
            foreach (SpawnManager waves in FindObjectsOfType<SpawnManager>())
            {
                waves.enabled = false;
            }
            Debug.Log("[Peekaboo] Roaming cat waves are off for the peekaboo prototype (Turn Off Roaming Waves).");
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (GameManager.instance != null)
        {
            GameManager.instance.GameStart -= BeginPacks;
            GameManager.instance.FinaleStart -= SpeedUp;
            GameManager.instance.GameEnd -= StopPacks;
        }
    }

    void BeginPacks()
    {
        speed = 1f;
        catsOutLimit = maxCatsOut;
        StartCoroutine(PackRoutine());
    }

    void SpeedUp()
    {
        speed = finaleSpeedMultiplier;
        catsOutLimit = Mathf.Max(maxCatsOut, maxCatsOutDuringFinale);
    }

    // Cats still waiting to be fed (fed cats flying off don't count)
    int HungryCatsOut()
    {
        int count = 0;
        foreach (PeekabooCat cat in pool)
        {
            if (cat != null && cat.gameObject.activeSelf && !cat.isFullyFed)
            {
                count++;
            }
        }
        return count;
    }

    void StopPacks()
    {
        StopAllCoroutines();
        foreach (CatHole hole in CatHole.all)
        {
            if (hole != null) hole.SetNextHint(false);
        }
    }

    IEnumerator PackRoutine()
    {
        yield return new WaitForSeconds(firstPackDelay);

        while (true)
        {
            List<CatHole> holes = PickHoles(Random.Range(minimumPackSize, maximumPackSize + 1));
            for (int i = 0; i < holes.Count; i++)
            {
                // Wait for room: the current cat is fed or gives up
                while (HungryCatsOut() >= catsOutLimit)
                {
                    yield return null;
                }

                // Faint eyes in the following hole, so the player knows where to look next
                if (i + 1 < holes.Count) holes[i + 1].SetNextHint(true);

                if (holes[i].isFree)
                {
                    PopAt(holes[i]);
                }
                else
                {
                    holes[i].SetNextHint(false);
                }
                yield return new WaitForSeconds(delayBetweenCats * speed);
            }

            // Pack done: wait for the last cat, then a short calm
            while (HungryCatsOut() > 0)
            {
                yield return null;
            }
            yield return new WaitForSeconds(delayBetweenPacks * speed);
        }
    }

    // Different free holes, in a random order
    List<CatHole> PickHoles(int count)
    {
        List<CatHole> free = new List<CatHole>();
        foreach (CatHole hole in CatHole.all)
        {
            if (hole != null && hole.isActiveAndEnabled && hole.isFree)
            {
                free.Add(hole);
            }
        }

        for (int i = free.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            CatHole swap = free[i];
            free[i] = free[j];
            free[j] = swap;
        }

        if (free.Count > count)
        {
            free.RemoveRange(count, free.Count - count);
        }
        return free;
    }

    public void PopAt(CatHole hole)
    {
        PeekabooCat cat = GetCat();
        if (cat == null || hole == null || !hole.isFree) return;

        cat.gameObject.SetActive(true);
        cat.PopOut(hole, catStayDuration);
    }

    PeekabooCat GetCat()
    {
        if (prefabs == null || prefabs.Length == 0) return null;

        // Reuse an idle cat of a random type if there is one, otherwise make a new one
        PeekabooCat prefab = prefabs[Random.Range(0, prefabs.Length)];
        foreach (PeekabooCat cat in pool)
        {
            if (cat != null && !cat.gameObject.activeSelf && cat.name.StartsWith(prefab.name))
            {
                return cat;
            }
        }

        PeekabooCat newCat = Instantiate(prefab, transform);
        newCat.name = prefab.name;
        newCat.gameObject.SetActive(false);
        pool.Add(newCat);
        return newCat;
    }
}
