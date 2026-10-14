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

    [Header("Feel")]

    [Min(1f)]
    [Tooltip("How fast treats fly, in m/s (the original game uses 15).")]
    public float treatSpeed = 25f;

    [Tooltip("Which pickups cats can drop (0 = x2 points, 1 = InstaKill, 2 = Shield, 3 = Rapid Fire). " +
             "InstaKill makes colour orders pointless and the shield protects nothing here, so only x2 by default.")]
    public List<int> allowedPickups = new List<int> { 0 };

    [Tooltip("Treats fly through furniture and only count when they reach a cat (or a pickup).")]
    public bool cleanShots = true;

    [Range(0.3f, 2f)]
    [Tooltip("Size of the invisible hit area around each cat's body (1 = about the body's size). 0.3 turns it off.")]
    public float hitboxGenerosity = 0.9f;

    [Range(0f, 20f)]
    [Tooltip("Light aim assist: a treat that's already within this many degrees of a cat wanting its colour curves " +
             "towards it. 0 = off.")]
    public float aimAssistAngle = 6f;

    [Min(0f)]
    [Tooltip("How sharply the aim assist can curve a treat, in degrees per second.")]
    public float aimAssistTurnSpeed = 120f;

    [Header("Catching")]

    [Tooltip("Knocked props burst outward in any direction (not just towards the player), then drift back in front as they fall.")]
    [Range(0f, 180f)] public float burstSpread = 180f;
    [Tooltip("How fast the burst throws a prop sideways, in m/s.")]
    [Min(0f)] public float burstOutwardSpeed = 3.5f;
    [Tooltip("How fast the burst throws a prop upwards, in m/s (less = flatter, more horizontal).")]
    [Min(0f)] public float burstUpSpeed = 3.2f;
    [Tooltip("Seconds a prop flies away before it can be caught.")]
    [Min(0f)] public float catchableAfter = 0.3f;
    [Tooltip("How slowly props sink once the burst is over, in m/s.")]
    [Min(0.05f)] public float fallSpeed = 0.55f;

    [Header("Player's space")]

    [Tooltip("The ninja never appears closer than this to the player, in metres.")]
    [Min(0f)] public float ninjaMinDistance = 2.5f;
    [Tooltip("Floor area around the player that cats can't walk into (e.g. inside the counter). Width, height, depth in metres; " +
             "kept low so cats can still jump up onto the counter top. Zero width = off.")]
    public Vector3 playerSpace = new Vector3(2.2f, 0.6f, 2.2f);

    [Header("Look")]

    [Range(0.1f, 1f)]
    [Tooltip("Size of confetti and smoke puffs (1 = as made). Smaller keeps the action easy to see.")]
    public float effectScale = 0.5f;

    readonly List<CatOrder> orders = new List<CatOrder>();
    PlayerInput player;
    NinjaRushCat ninja;
    SphereCollider ninjaHitbox;
    Phase current;
    float lastArrival = -100f;
    bool playing;

    void Awake()
    {
        ParticleManager.effectScale = effectScale;
        PickupManager.allowed = index => allowedPickups.Contains(index);
        Treat.cleanShots = cleanShots;
        Treat.aimAssistTurnSpeed = aimAssistTurnSpeed;
        Treat.aimAssistTarget = aimAssistAngle > 0 ? (System.Func<Treat, Vector3?>)AssistTarget : null;
    }

    // Where a treat should curve to: a hungry cat wanting its colour, or the ninja boss while it can be fed
    Vector3? AssistTarget(Treat treat)
    {
        Vector3? target = CatOrder.AssistTarget(treat, aimAssistAngle);
        if (target == null && ninja != null && ninja.feedable && ninja.gameObject.activeInHierarchy && !ninja.isFullyFed)
        {
            Vector3 point = ninjaHitbox != null ? ninjaHitbox.transform.TransformPoint(ninjaHitbox.center) : ninja.transform.position + Vector3.up * 0.3f;
            if (Vector3.Angle(treat.velocity, point - treat.transform.position) <= aimAssistAngle) target = point;
        }
        return target;
    }

    void Start()
    {
        // How knocked props fly in this mode
        CatChaosSettings flight = CatChaosSettings.instance;
        if (flight != null)
        {
            flight.launchSpread = burstSpread;
            flight.launchTowardPlayerSpeed = burstOutwardSpeed;
            flight.launchUpSpeed = burstUpSpeed;
            flight.catchableAfter = catchableAfter;
            flight.driftFallSpeed = fallSpeed;
        }

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
        PickupManager.allowed = null;
        Treat.cleanShots = false;
        Treat.aimAssistTarget = null;
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

        player.treatSpeed = treatSpeed;
        player.SetReticleColours(leftColour, rightColour);
        KeepCatsOutOfPlayerSpace();

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
        orders.Add(CatOrder.Give(cat, foods, bubbleScale, cleanShots ? hitboxGenerosity : 0f));
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

    // A cat-proof patch of floor where the player stands (cut out of the NavMesh while the round runs)
    void KeepCatsOutOfPlayerSpace()
    {
        if (playerSpace.x <= 0 || playerSpace.z <= 0 || player == null) return;
        GameObject space = new GameObject("Player Space (no cats)");
        Vector3 at = player.transform.position;
        if (NavMesh.SamplePosition(at, out NavMeshHit floor, 2f, NavMesh.AllAreas)) at.y = floor.position.y;
        space.transform.SetPositionAndRotation(at, Quaternion.Euler(0, player.transform.eulerAngles.y, 0));
        NavMeshObstacle obstacle = space.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = playerSpace;
        obstacle.center = new Vector3(0, playerSpace.y * 0.5f, 0);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
    }

    // Ninja stops far enough from the player (so nothing springs up right beside them)
    List<Vector3> StopsAwayFromPlayer()
    {
        if (player == null || ninjaMinDistance <= 0) return ninjaStops;
        Vector3 at = player.transform.position;
        List<Vector3> away = ninjaStops.FindAll(stop => new Vector2(stop.x - at.x, stop.z - at.z).magnitude >= ninjaMinDistance);
        return away.Count > 0 ? away : ninjaStops;
    }

    void SummonNinja(bool boss)
    {
        if (ninja == null || ninjaStops.Count == 0) return;
        List<Vector3> stops = StopsAwayFromPlayer();
        if (boss) ninja.Configure(true, bossKnocksPerStop, bossSitMin, bossSitMax);
        else ninja.Configure(false, catchKnocksPerStop, catchSitMin, catchSitMax);
        ninja.stops = stops;
        ninja.gameObject.SetActive(true);
        ninja.Initialise(transform, stops[Random.Range(0, stops.Count)]);
        // The boss gets the same generous hit area as the other cats
        if (cleanShots && ninjaHitbox == null) ninjaHitbox = CatOrder.AddHitbox(ninja.gameObject, hitboxGenerosity);
    }
}
