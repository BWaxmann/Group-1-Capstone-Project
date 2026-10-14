using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Runs the Cat Chaos round on top of Brian's waves:
// - Keeps the room lively: if too few hungry cats are about, it sends one more in (Brian's waves still set the ramp).
// - Cats walking past loose props knock them flying.
// - The boss phase: partway through, waves pause and a ninja cat blinks around the piles knocking things up.
//   Feeding it ends the phase (it also leaves on its own after a while, so there's no fail).
public class CatChaosDirector : MonoBehaviour
{
    [Header("Keep the room lively")]

    public ObjectSpawner catPool;

    [Tooltip("Where top-up cats come in (Brian's spawn points in front of the player).")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Tooltip("Where those cats go after being fed (matches each spawn point).")]
    public List<Transform> returnPoints = new List<Transform>();

    [Min(1)]
    public float roundLength = 180f;

    [Min(0)]
    [Tooltip("Fewest hungry cats wanted at the start of the round...")]
    public int minimumCatsAtStart = 2;

    [Min(0)]
    [Tooltip("...rising to this many by the end.")]
    public int minimumCatsAtEnd = 4;

    [Min(0)]
    [Tooltip("No top-ups during the first few seconds, so the waves set the opening.")]
    public float topUpsFrom = 10f;

    [Min(0.2f)]
    [Tooltip("Shortest gap between top-up cats, in seconds.")]
    public float topUpGap = 2.5f;

    [Header("Boss phase")]

    public NinjaRushCat ninjaPrefab;

    [Min(0)]
    [Tooltip("Seconds into the round when the ninja arrives.")]
    public float bossPhaseStart = 80f;

    [TextArea]
    public string bossArrivesBanner = "NINJA CAT!\n<size=55%>Feed it to send it home!</size>";
    public string bossFedBanner = "NINJA FED!";
    public string bossLeftBanner = "THE NINJA VANISHED!";

    [Tooltip("Places by the piles and loose props where the ninja can appear (worked out by the scene generator).")]
    public List<Vector3> ninjaStops = new List<Vector3>();

    float elapsed;
    bool playing;
    bool bossPhaseRunning;
    bool bossPhaseDone;
    float lastTopUp = -100f;
    float checkTimer;
    float looseTimer;
    NinjaRushCat ninja;
    Transform viewer;

    void Start()
    {
        GameManager.instance.GameStart += OnGameStart;
        GameManager.instance.GameEnd += OnGameEnd;

        if (ninjaPrefab != null)
        {
            ninja = Instantiate(ninjaPrefab, transform);
            ninja.gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.GameStart -= OnGameStart;
            GameManager.instance.GameEnd -= OnGameEnd;
        }
        SpawnManager.holdSpawns = false;
    }

    void OnGameStart()
    {
        playing = true;
        elapsed = 0;
    }

    void OnGameEnd()
    {
        playing = false;
        SpawnManager.holdSpawns = false;
    }

    void Update()
    {
        if (!playing) return;
        elapsed += Time.deltaTime;

        if (!bossPhaseDone && !bossPhaseRunning && ninja != null && ninjaStops.Count > 0 && elapsed >= bossPhaseStart)
        {
            StartCoroutine(BossPhase());
        }

        looseTimer -= Time.deltaTime;
        if (looseTimer <= 0)
        {
            looseTimer = 0.1f;
            KnockLooseProps();
        }

        checkTimer -= Time.deltaTime;
        if (checkTimer <= 0)
        {
            checkTimer = 0.5f;
            if (!bossPhaseRunning) TopUp();
        }
    }

    // ---------- Keeping it lively ----------

    void TopUp()
    {
        if (catPool == null || spawnPoints.Count == 0 || elapsed < topUpsFrom) return;
        if (Time.time - lastTopUp < topUpGap) return;

        int hungry = 0;
        foreach (CatMovement cat in CatMovement.active)
        {
            if (cat != null && !cat.isFullyFed && !cat.isBoss) hungry++;
        }

        float progress = Mathf.Clamp01(elapsed / roundLength);
        int wanted = Mathf.RoundToInt(Mathf.Lerp(minimumCatsAtStart, minimumCatsAtEnd, progress));
        if (hungry >= wanted) return;

        CatMovement newCat = catPool.GetRandomObjectFromPool<CatMovement>();
        if (newCat == null) return;

        int index = Random.Range(0, spawnPoints.Count);
        Transform spawn = spawnPoints[index];
        Transform returnPoint = index < returnPoints.Count && returnPoints[index] != null ? returnPoints[index] : spawn;
        Vector3 position = spawn.position;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1f, NavMesh.AllAreas)) position = hit.position;

        newCat.gameObject.SetActive(true);
        newCat.Initialise(returnPoint, position);
        lastTopUp = Time.time;
    }

    // ---------- Knocking things as cats wander past ----------

    // A pile, or a single loose prop, that wandering cats can knock
    class KnockSpot
    {
        public PropPile pile;
        public FlyingProp prop;
        public readonly HashSet<CatMovement> catsNear = new HashSet<CatMovement>();

        public Vector3 position => pile != null ? pile.pile.position : prop.transform.position;
    }

    readonly List<KnockSpot> knockSpots = new List<KnockSpot>();
    readonly List<CatMovement> leaving = new List<CatMovement>();
    float lastKnock = -100f;

    void FindKnockSpots()
    {
        knockSpots.Clear();
        foreach (PropPile pile in FindObjectsOfType<PropPile>())
        {
            if (pile.pile != null) knockSpots.Add(new KnockSpot { pile = pile });
        }
        foreach (FlyingProp prop in FlyingProp.all)
        {
            if (prop != null && prop.isLoose) knockSpots.Add(new KnockSpot { prop = prop });
        }
    }

    // Each time a cat comes near a pile or prop, there's a chance something goes flying
    void KnockLooseProps()
    {
        CatChaosSettings settings = CatChaosSettings.instance;
        if (settings == null) return;
        if (knockSpots.Count == 0) FindKnockSpots();

        foreach (KnockSpot spot in knockSpots)
        {
            Vector3 spotPosition = spot.position;
            foreach (CatMovement cat in CatMovement.active)
            {
                if (cat == null || cat.isFullyFed || cat is NinjaRushCat) continue;
                Vector3 offset = cat.transform.position - spotPosition;
                bool near = Mathf.Abs(offset.y) <= settings.knockHeightReach &&
                            new Vector2(offset.x, offset.z).sqrMagnitude <= settings.knockReach * settings.knockReach;
                if (!near)
                {
                    spot.catsNear.Remove(cat);
                }
                else if (spot.catsNear.Add(cat))
                {
                    CatArrived(spot, settings);
                }
            }

            // Forget cats that have gone (fed, or switched off)
            leaving.Clear();
            foreach (CatMovement cat in spot.catsNear)
            {
                if (cat == null || !cat.isActiveAndEnabled || cat.isFullyFed) leaving.Add(cat);
            }
            foreach (CatMovement cat in leaving) spot.catsNear.Remove(cat);
        }
    }

    void CatArrived(KnockSpot spot, CatChaosSettings settings)
    {
        if (spot.pile != null) spot.pile.Rattle(0.5f);
        if (Time.time - lastKnock < settings.knockGap || Random.value > settings.knockChance) return;

        bool knocked = spot.pile != null
            ? spot.pile.KnockTop(Viewer(), settings.propRestTime)
            : spot.prop.state == FlyingProp.State.Home && spot.prop.timeAtHome >= settings.propRestTime && spot.prop.Knock(Viewer());
        if (knocked) lastKnock = Time.time;
    }

    // ---------- Boss phase ----------

    IEnumerator BossPhase()
    {
        bossPhaseRunning = true;
        SpawnManager.holdSpawns = true;
        GameManager.instance.ShowBanner(bossArrivesBanner, 3f);
        Haptics.PulseBoth(Haptics.Strength.Strong);

        yield return new WaitForSeconds(1.5f);

        ninja.stops = ninjaStops;
        ninja.gameObject.SetActive(true);
        ninja.Initialise(transform, ninjaStops[Random.Range(0, ninjaStops.Count)]);

        // Wait until it's fed, or it gives up and vanishes
        while (playing && ninja.gameObject.activeInHierarchy && !ninja.isFullyFed)
        {
            yield return null;
        }

        if (playing)
        {
            GameManager.instance.ShowBanner(ninja.isFullyFed ? bossFedBanner : bossLeftBanner, 2.5f);
        }

        SpawnManager.holdSpawns = false;
        bossPhaseRunning = false;
        bossPhaseDone = true;
    }

    Vector3 Viewer()
    {
        if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
        return viewer != null ? viewer.position : transform.position;
    }
}
