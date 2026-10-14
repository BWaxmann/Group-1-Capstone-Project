using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Cat Cafe Rush: runs the round as a string of phases, one job at a time.
// Feed: hungry cats wander in with colour orders (left launcher Blue, right Orange).
// Catch: the cats run off, and the ninja blinks about knocking things into the air to catch (it can't be fed now).
// Boss: the finale. The ninja can be fed (it ends when it's full), while it keeps knocking things up.
// Brian's wave spawners are off in this scene; this spawns the cats itself.
public class RushDirector : MonoBehaviour
{
    public enum Kind
    {
        Feed,
        Catch,
        Boss
    }

    [Serializable]
    public class Phase
    {
        public Kind kind = Kind.Feed;
        [Min(1)] public float seconds = 35f;
        [Tooltip("Feed and Boss: how many hungry cats to keep about.")]
        [Min(0)] public int cats = 3;
        [Tooltip("Feed and Boss: shortest and longest order (treats per cat).")]
        [Min(1)] public int shortestOrder = 1;
        [Min(1)] public int longestOrder = 1;
        [TextArea] public string banner = "";
    }

    [Header("Round")]

    public List<Phase> phases = new List<Phase>
    {
        new Phase { kind = Kind.Feed, seconds = 35, cats = 3, shortestOrder = 1, longestOrder = 1, banner = "FEED THE CATS!" },
        new Phase { kind = Kind.Catch, seconds = 12, banner = "CATCH!\n<size=55%>Grip to grab what flies</size>" },
        new Phase { kind = Kind.Feed, seconds = 35, cats = 4, shortestOrder = 1, longestOrder = 2, banner = "COMBO ORDERS!" },
        new Phase { kind = Kind.Catch, seconds = 14, banner = "CATCH!" },
        new Phase { kind = Kind.Feed, seconds = 39, cats = 4, shortestOrder = 2, longestOrder = 3, banner = "THE CAFE IS PACKED!" },
        new Phase { kind = Kind.Boss, seconds = 45, cats = 1, shortestOrder = 1, longestOrder = 2, banner = "NINJA BOSS!\n<size=55%>Feed it before time's up!</size>" },
    };

    [Min(0.5f)]
    public float bannerSeconds = 2.5f;

    [Header("Cats")]

    public ObjectSpawner catPool;

    [Tooltip("Where cats come in, and where they run off to (in front of the player).")]
    public List<Transform> doors = new List<Transform>();

    [Min(0.2f)]
    [Tooltip("Shortest gap between new cats arriving, in seconds.")]
    public float arrivalGap = 1.5f;

    [Min(0.5f)]
    [Tooltip("Order bubbles are scaled up so they read from across the room.")]
    public float bubbleScale = 1.7f;

    [Header("Foods (each hand fires one)")]

    [Tooltip("Treat pool positions: 0 = Blue, 1 = Orange.")]
    public int leftFood = 0;
    public int rightFood = 1;
    public GameObject leftTreatPrefab;
    public GameObject rightTreatPrefab;
    public Color leftColour = new Color(0.3f, 0.6f, 1f);
    public Color rightColour = new Color(1f, 0.6f, 0.15f);
    [Tooltip("The tin hung under each launcher, tinted that hand's colour.")]
    public GameObject tinPrefab;
    [Min(0.01f)] public float tinSize = 0.09f;

    [Header("Ninja")]

    public NinjaRushCat ninjaPrefab;
    public List<Vector3> ninjaStops = new List<Vector3>();
    [Min(1)] public int catchKnocksPerStop = 2;
    [Min(0.2f)] public float catchSitMin = 0.8f;
    [Min(0.2f)] public float catchSitMax = 1.2f;
    [Min(1)] public int bossKnocksPerStop = 1;
    [Min(0.2f)] public float bossSitMin = 2f;
    [Min(0.2f)] public float bossSitMax = 2.8f;
    public string bossFedBanner = "NINJA FED!";
    public string bossLeftBanner = "THE NINJA VANISHED!";

    [Header("Look")]

    [Range(0.1f, 1f)]
    [Tooltip("Size of confetti and smoke puffs (1 = as made). Smaller keeps the action easy to see.")]
    public float effectScale = 0.5f;

    readonly List<CatOrder> orders = new List<CatOrder>();
    PlayerInput player;
    NinjaRushCat ninja;
    Phase current;
    float lastArrival = -100f;
    bool playing;

    void Awake()
    {
        ParticleManager.effectScale = effectScale;
    }

    void Start()
    {
        player = FindObjectOfType<PlayerInput>();
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
        ParticleManager.effectScale = 1f;
        if (GameManager.instance != null)
        {
            GameManager.instance.GameStart -= OnGameStart;
            GameManager.instance.GameEnd -= OnGameEnd;
        }
    }

    void OnGameStart()
    {
        playing = true;

        // Liminal switches the player rig on after the scene starts, so it isn't always there at Start
        if (player == null) player = FindObjectOfType<PlayerInput>();
        if (player == null)
        {
            Debug.LogError("[Cat Cafe Rush] No player rig (PlayerInput) found when the round started.");
            return;
        }

        // Each hand fires its own food, with a tin of that colour hanging under the launcher
        player.LoadFood(false, leftFood);
        player.LoadFood(true, rightFood);
        if (tinPrefab != null)
        {
            SpaceCafeVisuals.TinUnderLauncher(player.Launcher(false), tinPrefab, leftColour, tinSize);
            SpaceCafeVisuals.TinUnderLauncher(player.Launcher(true), tinPrefab, rightColour, tinSize);
        }

        StartCoroutine(RunPhases());
    }

    void OnGameEnd()
    {
        playing = false;
        current = null;
        if (ninja != null) ninja.Leave();
    }

    IEnumerator RunPhases()
    {
        foreach (Phase phase in phases)
        {
            if (!playing) yield break;
            current = phase;
            if (!string.IsNullOrEmpty(phase.banner)) GameManager.instance.ShowBanner(phase.banner, bannerSeconds);

            switch (phase.kind)
            {
                case Kind.Feed:
                    yield return new WaitForSeconds(phase.seconds);
                    break;

                case Kind.Catch:
                    Haptics.PulseBoth(Haptics.Strength.Strong);
                    SendCatsAway();
                    yield return new WaitForSeconds(1f);
                    SummonNinja(false);
                    yield return new WaitForSeconds(Mathf.Max(0, phase.seconds - 1f));
                    if (ninja != null) ninja.Leave();
                    break;

                case Kind.Boss:
                    Haptics.PulseBoth(Haptics.Strength.Huge);
                    yield return new WaitForSeconds(1f);
                    SummonNinja(true);
                    float timer = 1f;
                    while (playing && timer < phase.seconds && ninja.gameObject.activeInHierarchy && !ninja.isFullyFed)
                    {
                        timer += Time.deltaTime;
                        yield return null;
                    }
                    if (playing) GameManager.instance.ShowBanner(ninja.isFullyFed ? bossFedBanner : bossLeftBanner, bannerSeconds);
                    // If time is left after the boss, the round finishes with ordinary feeding
                    current = new Phase { kind = Kind.Feed, cats = phase.cats + 2, shortestOrder = phase.shortestOrder, longestOrder = phase.longestOrder };
                    while (playing) yield return null;
                    break;
            }
        }
    }

    void Update()
    {
        if (!playing || current == null || current.kind == Kind.Catch) return;
        if (Time.time - lastArrival < arrivalGap) return;

        orders.RemoveAll(o => o == null || !o.isHungry);
        if (orders.Count < current.cats) SendInCat();
    }

    void SendInCat()
    {
        if (catPool == null || doors.Count == 0) return;
        CatMovement cat = catPool.GetRandomObjectFromPool<CatMovement>();
        if (cat == null) return;

        Transform door = doors[Random.Range(0, doors.Count)];
        Vector3 position = door.position;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) position = hit.position;
        cat.gameObject.SetActive(true);
        cat.Initialise(door, position);

        int length = Random.Range(current.shortestOrder, current.longestOrder + 1);
        List<CatOrder.Food> foods = new List<CatOrder.Food>();
        for (int i = 0; i < length; i++)
        {
            bool left = Random.value < 0.5f;
            foods.Add(new CatOrder.Food
            {
                variation = left ? leftFood : rightFood,
                icon = new OrderBubble.Item(left ? leftTreatPrefab : rightTreatPrefab, left ? leftColour : rightColour)
            });
        }
        orders.Add(CatOrder.Give(cat, foods, bubbleScale));
        lastArrival = Time.time;
    }

    // Every hungry cat heads for the nearest door and leaves
    void SendCatsAway()
    {
        foreach (CatOrder order in orders)
        {
            if (order == null || !order.isHungry) continue;
            Vector3 cat = order.transform.position;
            Transform nearest = null;
            foreach (Transform door in doors)
            {
                if (nearest == null || (door.position - cat).sqrMagnitude < (nearest.position - cat).sqrMagnitude) nearest = door;
            }
            if (nearest != null) order.RunOff(nearest.position);
        }
        orders.Clear();
    }

    void SummonNinja(bool boss)
    {
        if (ninja == null || ninjaStops.Count == 0) return;
        if (boss) ninja.Configure(true, bossKnocksPerStop, bossSitMin, bossSitMax);
        else ninja.Configure(false, catchKnocksPerStop, catchSitMin, catchSitMax);
        ninja.stops = ninjaStops;
        ninja.gameObject.SetActive(true);
        ninja.Initialise(transform, ninjaStops[Random.Range(0, ninjaStops.Count)]);
    }
}
