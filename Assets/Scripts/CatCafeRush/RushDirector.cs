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
// Stampede and Pop-up are the Cat Cafe Waves additions (still being built: for now the cats clear out for that long).
// Brian's wave spawners are off in this scene; this spawns the cats itself.
public class RushDirector : MonoBehaviour
{
    // New kinds go on the end, so scenes that already have phases keep theirs
    public enum Kind
    {
        Feed,
        Catch,
        Boss,
        Stampede,
        PopUp
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
        [Range(0f, 1f)]
        [Tooltip("Feed: chance, at each stop while wandering, that a hungry cat goes to attack a pile. 0 = they just wander.")]
        public float attackChance = 0f;
        [Tooltip("Announcer line called out as this phase starts (its id in Resources/Announcer/lines.txt). Empty = none.")]
        public string line = "";
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

    [Tooltip("Which pickups cats can drop (0 = x2 points, 1 = InstaKill, 2 = Shield, 3 = Spread Shot, 4 = Rapid Fire). " +
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

    [Header("Trials")]

    [Tooltip("A cat attacking a pile tips its top prop over (not catchable) instead of sending it flying.")]
    public bool attacksTopple = false;

    [Tooltip("In the boss finale, each hand can flick its thumbstick to swap Single / Scatter / Buckshot.")]
    public bool finaleFireStyles = false;

    [TextArea]
    public string fireStylesBanner = "NINJA BOSS!\n<size=50%>Flick the thumbsticks to swap shots!</size>";

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

    [Header("Cat Cafe Waves")]

    [Tooltip("The cafe manager calls out each phase on the speaker (each phase's Line), with subtitles.")]
    public bool announcer = false;
    public string welcomeLine = "welcome";
    public string closingLine = "closing";
    public string bossFedLine = "boss_fed";
    public string bossLeftLine = "boss_left";
    [Tooltip("Played before the ninja's catch and boss lines (from Resources/CafeSounds).")]
    public string ninjaSting = "ninja_sting";

    [Tooltip("Fed cats run off to a hole in the floor (one at each door) and dive in, instead of launching.")]
    public bool fedCatsUseHoles = false;

    [Min(0)]
    [Tooltip("Coins that fly from a fed cat into a tip jar on the counter, for each treat in its order. 0 = no tip jar.")]
    public int coinsPerTreat = 0;
    [Min(0)] public int bossCoins = 8;
    [Tooltip("Optional: where the tip jar stands. Empty = on the counter beside the cat clock.")]
    public Transform tipJarSpot;

    readonly List<CatOrder> orders = new List<CatOrder>();
    PlayerInput player;
    NinjaRushCat ninja;
    SphereCollider ninjaHitbox;
    Phase current;
    float lastArrival = -100f;
    bool playing;
    Announcer voice;
    TipJar tipJar;
    readonly List<Vector3> holes = new List<Vector3>();

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

        if (announcer)
        {
            voice = gameObject.AddComponent<Announcer>();
            StartCoroutine(Welcome());
        }
    }

    IEnumerator Welcome()
    {
        yield return new WaitForSeconds(1.5f);
        if (!playing) Say(welcomeLine);
    }

    void Say(string line, string sting = null)
    {
        if (voice != null) voice.Say(line, sting);
    }

    void OnDestroy()
    {
        ParticleManager.effectScale = 1f;
        PickupManager.allowed = null;
        Treat.cleanShots = false;
        Treat.aimAssistTarget = null;
        PropPile.toppleOnHit = false;
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
        if (fedCatsUseHoles) MakeHoles();
        if (coinsPerTreat > 0 || bossCoins > 0) PlaceTipJar();

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
        Say(closingLine);
    }

    IEnumerator RunPhases()
    {
        foreach (Phase phase in phases)
        {
            if (!playing) yield break;
            current = phase;

            // Hungry cats only go for the piles in feeding phases (and only if this phase says so)
            CatMovement.targetingChanceOverride = phase.kind == Kind.Feed ? phase.attackChance : 0f;
            PropPile.toppleOnHit = attacksTopple;

            string banner = phase.banner;
            if (phase.kind == Kind.Boss && finaleFireStyles)
            {
                player.UnlockFireStyles(true);
                if (!string.IsNullOrEmpty(fireStylesBanner)) banner = fireStylesBanner;
            }
            if (!string.IsNullOrEmpty(banner)) GameManager.instance.ShowBanner(banner, bannerSeconds);
            bool ninjaPhase = phase.kind == Kind.Catch || phase.kind == Kind.Boss;
            Say(phase.line, ninjaPhase ? ninjaSting : null);

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
                    if (playing)
                    {
                        GameManager.instance.ShowBanner(ninja.isFullyFed ? bossFedBanner : bossLeftBanner, bannerSeconds);
                        Say(ninja.isFullyFed ? bossFedLine : bossLeftLine);
                        if (ninja.isFullyFed && tipJar != null) tipJar.AddCoins(ninja.transform.position + Vector3.up * 0.3f, bossCoins);
                    }
                    // If time is left after the boss, the round finishes with ordinary feeding
                    current = new Phase { kind = Kind.Feed, cats = phase.cats + 2, shortestOrder = phase.shortestOrder, longestOrder = phase.longestOrder };
                    while (playing) yield return null;
                    break;

                case Kind.Stampede:
                case Kind.PopUp:
                    // Not built yet: plays as ordinary feeding for now (with its own banner and callout)
                    Debug.LogWarning($"[Cat Cafe Rush] {phase.kind} phase isn't built yet; it plays as feeding for {phase.seconds:F0} s.");
                    yield return new WaitForSeconds(phase.seconds);
                    break;
            }
        }
    }

    // Phases where hungry cats with orders keep coming in (Stampede and Pop-up too, until they're built)
    static bool HasOrders(Kind kind)
    {
        return kind != Kind.Catch;
    }

    void Update()
    {
        if (!playing || current == null || !HasOrders(current.kind)) return;
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
        CatOrder order = CatOrder.Give(cat, foods, bubbleScale, cleanShots ? hitboxGenerosity : 0f);
        order.served = Served;
        order.holeFor = fedCatsUseHoles && holes.Count > 0 ? (Func<Vector3, Vector3?>)NearestHole : null;
        orders.Add(order);
        lastArrival = Time.time;
    }

    void Served(CatOrder order)
    {
        if (tipJar != null && coinsPerTreat > 0) tipJar.AddCoins(order.transform.position + Vector3.up * 0.3f, coinsPerTreat * order.orderLength);
    }

    // ---------- Cat Cafe Waves: holes and the tip jar ----------

    Vector3? NearestHole(Vector3 from)
    {
        Vector3? best = null;
        foreach (Vector3 hole in holes)
        {
            if (best == null || (hole - from).sqrMagnitude < (best.Value - from).sqrMagnitude) best = hole;
        }
        return best;
    }

    // A dark round hole in the floor at each door (cats come out of them, and fed cats dive back in)
    void MakeHoles()
    {
        holes.Clear();
        foreach (Transform door in doors)
        {
            if (door == null) continue;
            Vector3 at = door.position;
            if (NavMesh.SamplePosition(at, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) at = hit.position;
            holes.Add(at);

            GameObject rim = SpaceCafeVisuals.Shape(PrimitiveType.Cylinder, transform, "Cat Hole", new Color(0.18f, 0.11f, 0.06f));
            rim.transform.localScale = new Vector3(0.62f, 0.002f, 0.62f);
            rim.transform.position = at + Vector3.up * 0.004f;
            GameObject hole = SpaceCafeVisuals.Shape(PrimitiveType.Cylinder, rim.transform, "Dark", Color.black);
            hole.GetComponent<Renderer>().sharedMaterial.renderQueue = 3001;
            hole.transform.localScale = new Vector3(0.82f, 1.2f, 0.82f);
        }
    }

    // On the counter beside the cat clock (it stands on the counter), on whichever side is nearer the player's front
    // right, so it's in view but out of the way of shots at the room. A Tip Jar Spot, if set, wins.
    void PlaceTipJar()
    {
        Transform eye = player.eyePoint != null ? player.eyePoint : (Camera.main != null ? Camera.main.transform : null);
        Vector3 head = eye != null ? eye.position : player.transform.position + Vector3.up * 1.6f;
        if (tipJarSpot != null)
        {
            tipJar = TipJar.Create(tipJarSpot.position, head);
            return;
        }

        Vector3 forward = player.transform.forward;
        forward.y = 0;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 wanted = head + forward * 0.8f + right * 0.5f;

        Vector3? spot = BesideTheClock(wanted);
        if (spot == null)
        {
            Debug.LogWarning("[Cat Cafe Waves] No Cat_clock on a counter to put the tip jar beside; it's guessing the counter height.");
            spot = new Vector3(wanted.x, head.y - 0.65f, wanted.z);
        }
        tipJar = TipJar.Create(spot.Value, head);
    }

    Vector3? BesideTheClock(Vector3 wanted)
    {
        GameObject clock = GameObject.Find("Cat_clock");
        if (clock == null) return null;

        // The counter top is where the clock's base sits
        float top = clock.transform.position.y;
        bool any = false;
        foreach (Renderer rend in clock.GetComponentsInChildren<Renderer>())
        {
            if (rend.GetComponentInParent<Canvas>() != null) continue;
            top = any ? Mathf.Min(top, rend.bounds.min.y) : rend.bounds.min.y;
            any = true;
        }
        Vector3 centre = clock.transform.position;
        centre.y = top;

        // Spots around the clock where the counter is clear (a ray down lands on a surface at the counter's height),
        // the one nearest where we'd like it
        Vector3? best = null;
        float bestDistance = float.MaxValue;
        for (float distance = 0.3f; distance <= 0.61f; distance += 0.15f)
        {
            for (int angle = 0; angle < 360; angle += 30)
            {
                Vector3 spot = centre + Quaternion.Euler(0, angle, 0) * Vector3.forward * distance;
                if (!ClearCounterAt(spot, top, clock.transform)) continue;
                float off = new Vector2(spot.x - wanted.x, spot.z - wanted.z).magnitude;
                if (off < bestDistance)
                {
                    bestDistance = off;
                    best = spot;
                }
            }
        }

        // Nothing to test against (no colliders): just beside the clock, towards where we'd like it
        if (best == null)
        {
            Vector3 towards = wanted - centre;
            towards.y = 0;
            best = centre + (towards.sqrMagnitude > 0.001f ? towards.normalized : Vector3.right) * 0.3f;
        }
        return best;
    }

    static bool ClearCounterAt(Vector3 spot, float top, Transform clock)
    {
        RaycastHit[] hits = Physics.RaycastAll(spot + Vector3.up * 0.6f, Vector3.down, 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float highest = float.MinValue;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(clock) || hit.collider.GetComponentInParent<CatMovement>() != null) continue;
            highest = Mathf.Max(highest, hit.point.y);
        }
        // The highest thing there is the counter top itself (not the floor below it, and nothing standing on it)
        return Mathf.Abs(highest - top) < 0.04f;
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
