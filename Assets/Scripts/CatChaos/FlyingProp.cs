using System.Collections.Generic;
using UnityEngine;

// A cafe prop (plate, cup, jar...) that a cat can knock flying.
// It sits at its home spot on a pile, flies on a slow scripted arc when knocked (no physics, so it's cheap on
// Quest and predictable to catch), can be held by the gravity beam, and flies home when let go.
// If nobody catches it, it lands with a crash and pops back home a few seconds later. Nothing is ever lost.
public class FlyingProp : MonoBehaviour
{
    public enum State
    {
        Home,
        Flying,
        Held,
        Returning,
        Landed,
        Toppled
    }

    // Props that are flying or being held. Kept short (see CatChaosSettings.maxAirborne) for the Quest.
    public static readonly List<FlyingProp> airborne = new List<FlyingProp>();

    // Every prop in the scene
    public static readonly List<FlyingProp> all = new List<FlyingProp>();

    [Tooltip("Loose props sit along the cats' paths and get knocked by cats walking past (pile props get knocked by paw swipes).")]
    public bool isLoose;

    [Tooltip("Optional bits (leaves, cactus chunks...) that burst off when it's knocked and flutter to the floor. Decoration only.")]
    public GameObject[] burstPieces;

    [Min(0)]
    public int burstCount = 4;

    [Min(0.01f)]
    [Tooltip("Size of each burst piece, in metres.")]
    public float burstPieceSize = 0.14f;

    public State state { get; private set; } = State.Home;

    // Not straight away: it bursts out first, then drifts down slowly (that's the time to catch it)
    public bool isCatchable => state == State.Flying && CatChaosSettings.instance != null && flightTime >= CatChaosSettings.instance.catchableAfter;
    public Vector3 centre => propRenderer != null ? propRenderer.bounds.center : transform.position;

    // How long it's been sitting at home, so a prop that just came back isn't knocked again straight away
    public float timeAtHome => state == State.Home ? Time.time - homeSince : 0f;

    float homeSince = -100f;

    // Knocks up to `count` props sitting at home within `reach` of a point, top ones first. Returns how many went up.
    public static int KnockNear(Vector3 point, float reach, int count, Vector3 playerPosition)
    {
        int knocked = 0;
        for (int n = 0; n < count; n++)
        {
            FlyingProp best = null;
            foreach (FlyingProp prop in all)
            {
                if (prop == null || prop.state != State.Home) continue;
                Vector3 offset = prop.transform.position - point;
                offset.y *= 0.5f;   // reaching up or down a little is fine
                if (offset.magnitude > reach) continue;
                if (best == null || prop.transform.position.y > best.transform.position.y) best = prop;
            }
            if (best == null || !best.Knock(playerPosition)) break;
            knocked++;
        }
        return knocked;
    }

    Transform homeParent;
    Vector3 homeLocalPosition;
    Quaternion homeLocalRotation;
    Vector3 homeLocalScale;

    // Stacked props: what it sits on at home, and how far it has sunk because some of that isn't home (it rests on
    // what's left, or the table, instead of floating where the missing ones were)
    Bounds homeBounds;
    List<FlyingProp> supports;
    float sunk;

    Renderer[] renderers;
    Renderer propRenderer;
    Vector3 velocity;
    Vector3 spinAxis;
    float spinSpeed;
    float timer;

    // Flight: a burst upwards, then an erratic drift down
    float flightTime;
    bool drifting;
    bool falling;           // dropped off the beam away from its pile: straight down (it can still be grabbed)
    bool caughtThisFlight;  // the catch score is paid once per time it's knocked flying
    LineRenderer homeRing;
    GameObject dragHint;

    // Cat Cafe Waves' ninja phases: a caught prop has to be dragged back over its pile and let go there (a ring marks
    // the spot); let go anywhere else and it drops
    public static bool dragHome;
    // The drop zone is the top of whatever the pile sits on (its table); this if nothing wide is found under it
    public static float dropRadius = 0.8f;
    // How far past the table's edge (metres) a held prop still counts as over it: forgiving, since it's hard to be exact
    // in the middle of everything (set per phase by the director)
    public static float dropMargin = 3f;
    bool zoneMeasured;
    Vector3 zoneCentre;     // middle of the table top
    float zoneRadius;
    public static string dragHintText = "Bring it home!";
    static int dragHintsShown;
    const int DragHints = 2;

    // Where to carry it back to: the middle of its pile's table top
    public Vector3 dropCentre { get { MeasureZone(); return zoneCentre; } }
    // Held anywhere over its pile's table (from just above the top to well up in the air)
    public bool nearHome
    {
        get
        {
            if (state != State.Held) return false;
            MeasureZone();
            Vector3 c = centre;
            float across = new Vector2(c.x - zoneCentre.x, c.z - zoneCentre.z).magnitude;
            return across <= zoneRadius + dropMargin && c.y >= zoneCentre.y - 0.25f - dropMargin && c.y <= zoneCentre.y + 2f;
        }
    }

    // Finds the table top under its home spot (the widest solid thing just below it)
    void MeasureZone()
    {
        if (zoneMeasured) return;
        zoneMeasured = true;
        Vector3 home = HomePosition();
        zoneCentre = home;
        zoneRadius = dropRadius;
        float widest = 0;
        foreach (RaycastHit hit in Physics.RaycastAll(home + Vector3.up * 0.05f, Vector3.down, 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Collider below = hit.collider;
            if (below.GetComponentInParent<FlyingProp>() != null || below.GetComponentInParent<CatMovement>() != null) continue;
            Bounds b = below.bounds;
            float half = Mathf.Max(b.extents.x, b.extents.z);
            if (half < 0.3f || half > 1.6f || half <= widest) continue;
            widest = half;
            zoneCentre = new Vector3(b.center.x, Mathf.Max(hit.point.y, b.max.y), b.center.z);
            zoneRadius = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z), 0.45f, 1.2f);
        }
    }
    float driftTimer;
    Vector3 driftVelocity;
    Vector3 viewer;
    Vector3 actionCentre;
    float maxReach;   // never further from the player than when it was knocked (so never back through the wall behind it)
    bool slowFall;    // hangs in the air for a long time (e.g. the first prop, while the catch is explained)
    Outline glow;

    // Holding and returning
    Vector3 holdTarget;
    Vector3 returnStart;
    Quaternion returnStartRotation;

    // Growing while airborne, and the trail
    Vector3 flyingScale;
    float grow;
    TrailRenderer trail;

    // Raised when a prop hits the floor uncaught (e.g. to end a catch streak)
    public static event System.Action<FlyingProp> Landed;

    public void Setup()
    {
        homeParent = transform.parent;
        homeLocalPosition = transform.localPosition;
        homeLocalRotation = transform.localRotation;
        homeLocalScale = transform.localScale;
        renderers = GetComponentsInChildren<Renderer>();
        propRenderer = renderers.Length > 0 ? renderers[0] : null;
        homeBounds = propRenderer != null ? propRenderer.bounds : new Bounds(transform.position, Vector3.zero);
        foreach (Renderer rend in renderers) homeBounds.Encapsulate(rend.bounds);
    }

    void Awake()
    {
        if (homeParent == null) Setup();
    }

    void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    void OnDisable()
    {
        airborne.Remove(this);
        all.Remove(this);
    }

    // Sends the prop up on a slow arc, drifting towards the player. Returns false if it can't go right now.
    // A slow-fall prop hangs in the air for a long time. Ignoring the limit lets a whole pile go up at once.
    public bool Knock(Vector3 playerPosition, bool slowFall = false, bool ignoreLimit = false)
    {
        CatChaosSettings settings = CatChaosSettings.instance;
        if (state != State.Home || settings == null || (!ignoreLimit && airborne.Count >= settings.maxAirborne)) return false;

        Vector3 toPlayer = playerPosition - transform.position;
        toPlayer.y = 0;
        if (toPlayer.sqrMagnitude < 0.01f) toPlayer = -transform.forward;
        toPlayer.Normalize();

        // A random direction within the spread, but not into a wall: try a few, else head towards the player.
        // The check starts a little towards the player, so a wall the prop is pushed up against still counts.
        Vector3 drift = toPlayer;
        Vector3 from = centre + toPlayer * 0.3f;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            Vector3 candidate = Quaternion.Euler(0, Random.Range(-settings.launchSpread, settings.launchSpread), 0) * toPlayer;
            if (!WallAhead(from, candidate, 2.3f, out _))
            {
                drift = candidate;
                break;
            }
        }

        // Fast and high, but never up into the ceiling: v = sqrt(2 g h) reaches exactly the room left above it
        float headroom = Mathf.Max(0.3f, Mathf.Min(settings.floorHeight + settings.maxHeightAboveFloor, CeilingAbove() - 0.3f) - transform.position.y);
        float up = Mathf.Min(settings.launchUpSpeed * Random.Range(0.9f, 1.1f), Mathf.Sqrt(2f * settings.propGravity * headroom));
        velocity = Vector3.up * up + drift * settings.launchTowardPlayerSpeed * Random.Range(0.8f, 1.2f);
        spinAxis = Random.onUnitSphere;
        spinSpeed = Random.Range(180f, 360f);
        flightTime = 0;
        drifting = false;
        falling = false;
        caughtThisFlight = false;
        viewer = playerPosition;
        this.slowFall = slowFall;
        // It drifts about over the space between the player and where it came from, so it stays in front of them
        Vector3 fromPlayer = transform.position - playerPosition;
        fromPlayer.y = 0;
        actionCentre = playerPosition + fromPlayer * 0.6f;
        maxReach = fromPlayer.magnitude + 0.25f;

        // Fly free of the pile (keeping its size), so the pile can rattle without dragging it along
        StopAllCoroutines();
        transform.localScale = homeLocalScale;
        transform.SetParent(null, true);
        flyingScale = transform.localScale;
        grow = 0;
        SetTrail(settings, true);
        state = State.Flying;
        airborne.Add(this);
        ResettleThoseOnTop();   // anything stacked on it drops onto what's left
        settings.PlayKnock(transform.position);
        if (burstPieces != null && burstPieces.Length > 0 && burstCount > 0)
        {
            FlutterPiece.Burst(burstPieces, burstCount, burstPieceSize, centre, settings.floorHeight);
        }
        return true;
    }

    // A gentler knock: the prop tips off the side of its pile and lands on whatever's below (table or floor),
    // lies there for a bit, then pops back home. Not catchable. Returns false if it can't go right now.
    public bool Topple()
    {
        CatChaosSettings settings = CatChaosSettings.instance;
        if (state != State.Home || settings == null) return false;

        Vector2 random = Random.insideUnitCircle.normalized;
        Vector3 side = new Vector3(random.x, 0, random.y);
        Vector3 start = transform.position;
        Vector3 over = start + side * Random.Range(0.25f, 0.45f);
        // The highest surface below, ignoring cats (the swiping cat is usually right there)
        float landY = settings.floorHeight;
        foreach (RaycastHit hit in Physics.RaycastAll(over + Vector3.up * 0.3f, Vector3.down, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<CatMovement>() != null) continue;
            if (hit.point.y > landY && hit.point.y <= start.y + 0.05f) landY = hit.point.y;
        }
        Vector3 land = new Vector3(over.x, landY, over.z);

        StopAllCoroutines();
        transform.localScale = homeLocalScale;
        transform.SetParent(null, true);
        state = State.Toppled;
        ResettleThoseOnTop();
        settings.PlayKnock(start);
        StartCoroutine(ToppleRoutine(start, land, side, settings));
        return true;
    }

    System.Collections.IEnumerator ToppleRoutine(Vector3 start, Vector3 land, Vector3 side, CatChaosSettings settings)
    {
        Quaternion from = transform.rotation;
        Quaternion lying = Quaternion.AngleAxis(90f, Vector3.Cross(Vector3.up, side)) * from;   // tipped over, away from the pile
        float duration = Mathf.Clamp(0.25f + (start.y - land.y) * 0.3f, 0.3f, 0.7f);
        float t = 0;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / duration);
            Vector3 position = Vector3.Lerp(start, land, t * t);   // speeds up as it falls
            position.y += Mathf.Sin(t * Mathf.PI) * 0.12f;         // a little hop off the edge
            transform.position = position;
            transform.rotation = Quaternion.Slerp(from, lying, t);
            yield return null;
        }
        settings.PlayLanded(transform.position);

        yield return new WaitForSeconds(settings.respawnDelay + 1f);
        SettleHome();
        settings.PlayRespawn(transform.position);
        StartCoroutine(PopIn());
    }

    // Raised whenever the gravity beam grabs a prop (e.g. so the announcer can explain carrying it home, once)
    public static event System.Action<FlyingProp> Grabbed;

    // The gravity beam has grabbed it
    public void Hold(Vector3 target)
    {
        if (state != State.Flying) return;
        state = State.Held;
        Grabbed?.Invoke(this);
        holdTarget = target;
        velocity = Vector3.zero;
        falling = false;
        if (dragHome && dragHintsShown < DragHints && !string.IsNullOrEmpty(dragHintText))
        {
            dragHintsShown++;
            dragHint = FloatingWord.Make(dragHintText, new Color(1f, 0.85f, 0.2f), 2.2f);
        }
    }

    public void SetHoldTarget(Vector3 target)
    {
        holdTarget = target;
    }

    // True the first time it's caught since it was knocked flying (a dropped prop grabbed again doesn't score again)
    public bool FirstCatch()
    {
        if (caughtThisFlight) return false;
        caughtThisFlight = true;
        return true;
    }

    // Let go of the beam: the prop flies back to its spot on the pile (in the ninja phases, only if it was let go over
    // its pile: otherwise it drops)
    public void Release()
    {
        if (state != State.Held) return;
        ShowHomeRing(false);
        if (dragHint != null) Destroy(dragHint);
        if (dragHome && !nearHome)
        {
            state = State.Flying;
            falling = true;
            velocity = Vector3.zero;
            flightTime = CatChaosSettings.instance != null ? CatChaosSettings.instance.catchableAfter : 0f;   // a quick save
            return;
        }
        state = State.Returning;
        timer = 0;
        returnStart = transform.position;
        returnStartRotation = transform.rotation;
    }

    void Update()
    {
        CatChaosSettings settings = CatChaosSettings.instance;
        if (settings == null) return;
        float dt = Time.deltaTime;
        UpdateGlow(settings);

        // Bigger while it's in the air or held, so it's easy to spot; back to normal on the way home
        if (settings.airborneScale > 1f && (state == State.Flying || state == State.Held || state == State.Returning))
        {
            grow = state == State.Returning ? Mathf.Clamp01(1f - timer / settings.returnDuration) : Mathf.MoveTowards(grow, 1f, dt * 5f);
            transform.localScale = flyingScale * Mathf.Lerp(1f, settings.airborneScale, grow);
        }

        switch (state)
        {
            case State.Flying:
                flightTime += dt;
                if (falling)
                {
                    velocity += Vector3.down * settings.propGravity * dt;
                }
                else if (!drifting)
                {
                    // The burst: up fast, slowing under gravity
                    velocity += Vector3.down * settings.propGravity * dt;
                    if (velocity.y <= 0)
                    {
                        drifting = true;
                        driftTimer = 0;
                        spinSpeed *= 0.5f;
                    }
                }
                else
                {
                    // The drift: swoops about, changing direction now and then, and sinks slowly
                    driftTimer -= dt;
                    if (driftTimer <= 0)
                    {
                        driftTimer = Random.Range(settings.driftTurnTime * 0.5f, settings.driftTurnTime);
                        driftVelocity = PickDrift(settings);
                    }
                    Vector3 sideways = Vector3.Lerp(new Vector3(velocity.x, 0, velocity.z), driftVelocity, 1f - Mathf.Exp(-3f * dt));
                    float fall = slowFall ? Mathf.Min(0.08f, settings.driftFallSpeed) : settings.driftFallSpeed;
                    float sink = Mathf.MoveTowards(velocity.y, -fall, settings.propGravity * dt);
                    velocity = new Vector3(sideways.x, sink, sideways.z);
                }

                transform.position += KeepOutOfWalls(velocity * dt, dt);
                transform.Rotate(spinAxis, spinSpeed * dt, Space.World);
                if (velocity.y < 0 && transform.position.y <= settings.floorHeight + 0.05f)
                {
                    Land(settings);
                }
                break;

            case State.Held:
                // Floats where the beam points, with a gentle bob and spin so it feels caught, not frozen
                Vector3 bob = Vector3.up * Mathf.Sin(Time.time * 4f) * 0.03f;
                transform.position = Vector3.Lerp(transform.position, holdTarget + bob, 1f - Mathf.Exp(-12f * dt));
                transform.Rotate(Vector3.up, 90f * dt, Space.World);
                if (dragHome) UpdateHomeRing();
                break;

            case State.Returning:
                timer += dt;
                float t = Mathf.Clamp01(timer / settings.returnDuration);
                float eased = t * t * (3f - 2f * t);
                Vector3 home = HomePosition() + Vector3.down * RestingDrop();   // onto what's there now
                Vector3 position = Vector3.Lerp(returnStart, home, eased);
                position.y += Mathf.Sin(t * Mathf.PI) * 0.5f;   // a little hop on the way back
                transform.position = position;
                transform.rotation = Quaternion.Slerp(returnStartRotation, HomeRotation(), eased);
                if (t >= 1f)
                {
                    SettleHome();
                    settings.PlayHome(transform.position);
                }
                break;

            case State.Landed:
                // Missed props can stay broken for the rest of the round (the pile shows what's left)
                if (settings.missedStayDown) break;
                timer += dt;
                if (timer >= settings.respawnDelay)
                {
                    SettleHome();
                    SetVisible(true);
                    settings.PlayRespawn(transform.position);
                    StartCoroutine(PopIn());
                }
                break;
        }
    }

    // A new random sideways direction, pulled back towards the action if it has wandered off
    // (and kept from drifting into the player's face)
    Vector3 PickDrift(CatChaosSettings settings)
    {
        Vector2 random = Random.insideUnitCircle.normalized;
        Vector3 direction = new Vector3(random.x, 0, random.y);

        Vector3 toCentre = actionCentre - transform.position;
        toCentre.y = 0;
        direction += toCentre * 0.6f;

        Vector3 fromViewer = transform.position - viewer;
        fromViewer.y = 0;
        if (fromViewer.magnitude < 1.4f) direction += fromViewer.normalized * 2f;

        direction.y = 0;
        if (direction.sqrMagnitude < 0.0001f) return Vector3.zero;
        direction.Normalize();

        // Not into a wall: if one's close that way, head back towards the action instead
        if (WallAhead(transform.position, direction, 0.8f, out _))
        {
            direction = toCentre.sqrMagnitude > 0.0001f ? toCentre.normalized : -direction;
        }
        return direction * settings.driftSpeed * Random.Range(0.6f, 1.2f);
    }

    // The step a flying prop takes this frame, kept out of walls: it glances off any wall in its way, and it never
    // goes further from the player than where it started (so not back through the wall behind its spot, even where
    // that wall has no collider)
    Vector3 KeepOutOfWalls(Vector3 step, float dt)
    {
        Vector3 flat = new Vector3(step.x, 0, step.z);
        float length = flat.magnitude;
        if (length > 0.00001f && WallAhead(transform.position, flat / length, length + 0.15f, out Vector3 normal))
        {
            normal.y = 0;
            if (normal.sqrMagnitude > 0.0001f)
            {
                normal.Normalize();
                float into = Vector3.Dot(velocity, normal);
                if (into < 0) velocity -= normal * into * 1.4f;
                float driftInto = Vector3.Dot(driftVelocity, normal);
                if (driftInto < 0) driftVelocity -= normal * driftInto * 2f;
                step = velocity * dt;
            }
        }

        Vector3 next = transform.position + step;
        Vector3 away = next - viewer;
        away.y = 0;
        float distance = away.magnitude;
        if (maxReach > 0 && distance > maxReach)
        {
            Vector3 outward = away / distance;
            next -= outward * (distance - maxReach);
            float outSpeed = Vector3.Dot(velocity, outward);
            if (outSpeed > 0) velocity -= outward * outSpeed;
            step = next - transform.position;
        }
        return step;
    }

    // Height of whatever's overhead (the ceiling, a shelf...), ignoring props and cats; very high if nothing is
    float CeilingAbove()
    {
        int count = Physics.RaycastNonAlloc(centre, Vector3.up, wallHits, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float lowest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider other = wallHits[i].collider;
            if (other.GetComponentInParent<FlyingProp>() != null || other.GetComponentInParent<CatMovement>() != null) continue;
            lowest = Mathf.Min(lowest, wallHits[i].point.y);
        }
        return lowest;
    }

    static readonly RaycastHit[] wallHits = new RaycastHit[16];

    // Is there a wall (or the side of something solid) that way? Floors and table tops, cats, props and the player
    // don't count.
    static bool WallAhead(Vector3 from, Vector3 direction, float distance, out Vector3 normal)
    {
        normal = Vector3.zero;
        int count = Physics.RaycastNonAlloc(from, direction, wallHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = wallHits[i];
            if (hit.distance <= 0f || hit.distance >= nearest) continue;
            if (Vector3.Dot(hit.normal, Vector3.up) > 0.6f) continue;
            Collider other = hit.collider;
            if (other.GetComponentInParent<FlyingProp>() != null || other.GetComponentInParent<CatMovement>() != null ||
                other.GetComponentInParent<PlayerInput>() != null) continue;
            nearest = hit.distance;
            normal = hit.normal;
        }
        return nearest < float.MaxValue;
    }

    // Catchable props pulse with a bright outline (seen through furniture too), if the scene wants it
    void UpdateGlow(CatChaosSettings settings)
    {
        bool on = settings.catchableGlow && isCatchable;
        if (on && glow == null)
        {
            glow = gameObject.AddComponent<Outline>();
            glow.OutlineMode = Outline.Mode.OutlineAll;
        }
        if (glow == null) return;
        if (glow.enabled != on) glow.enabled = on;
        if (!on) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
        glow.OutlineWidth = Mathf.Lerp(3f, 8f, pulse);
        glow.OutlineColor = Color.Lerp(settings.glowColour, Color.white, pulse * 0.6f);
    }

    // Every prop that hit the floor and stayed down pops back onto its pile (a fresh stock of crockery, e.g. before the
    // next ninja phase). Returns how many came back.
    public static int RestockAll()
    {
        int back = 0;
        CatChaosSettings settings = CatChaosSettings.instance;
        foreach (FlyingProp prop in all)
        {
            if (prop == null || prop.state != State.Landed) continue;
            prop.SettleHome();
            prop.SetVisible(true);
            prop.StartCoroutine(prop.PopIn());
            if (settings != null && back % 3 == 0) settings.PlayRespawn(prop.transform.position);   // not a wall of sound
            back++;
        }
        return back;
    }

    // While it's held in a ninja phase: a ring round its spot on the pile, brighter once it's close enough to let go
    void UpdateHomeRing()
    {
        ShowHomeRing(true);
        bool close = nearHome;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (close ? 14f : 6f));
        Color colour = close ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.85f, 0.2f);
        colour.a = Mathf.Lerp(0.5f, 1f, pulse);
        homeRing.startColor = homeRing.endColor = colour;
        homeRing.widthMultiplier = close ? 0.05f : 0.03f;
        homeRing.transform.parent.position = zoneCentre + Vector3.up * 0.02f;
        if (dragHint != null)
        {
            dragHint.transform.position = zoneCentre + Vector3.up * (0.45f + Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 0.05f);
            FloatingWord.FacePlayer(dragHint.transform);
        }
    }

    void ShowHomeRing(bool show)
    {
        if (show && homeRing == null)
        {
            Transform holder = new GameObject("Drop Ring").transform;
            holder.rotation = Quaternion.Euler(90f, 0, 0);
            MeasureZone();
            homeRing = SpaceCafeVisuals.Ring(holder, "Ring", zoneRadius, 0.03f, Color.white);
        }
        if (homeRing != null && homeRing.transform.parent.gameObject.activeSelf != show) homeRing.transform.parent.gameObject.SetActive(show);
    }

    void Land(CatChaosSettings settings)
    {
        airborne.Remove(this);
        state = State.Landed;
        timer = 0;
        SetVisible(false);
        SetTrail(settings, false);
        settings.PlayLanded(transform.position);
        Landed?.Invoke(this);
    }

    void SetTrail(CatChaosSettings settings, bool on)
    {
        if (on && settings != null && settings.airborneTrail && trail == null)
        {
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = SpaceCafeVisuals.UnlitMaterial(Color.white);
            trail.time = 0.35f;
            trail.minVertexDistance = 0.04f;
            trail.widthMultiplier = 0.12f;
            trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
            Gradient gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(settings.trailColour, 0), new GradientColorKey(settings.trailColour, 1) },
                             new[] { new GradientAlphaKey(settings.trailColour.a, 0), new GradientAlphaKey(0, 1) });
            trail.colorGradient = gradient;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }
        if (trail == null) return;
        if (on) trail.Clear();
        trail.emitting = on;
    }

    void SettleHome()
    {
        airborne.Remove(this);
        transform.SetParent(homeParent, false);
        transform.localPosition = homeLocalPosition;
        transform.localRotation = homeLocalRotation;
        transform.localScale = homeLocalScale;
        state = State.Home;
        homeSince = Time.time;
        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }
        // Down onto what's actually there, and anything that was resting lower for want of this goes back up
        sunk = RestingDrop();
        if (sunk > 0) transform.position += Vector3.down * sunk;
        ResettleThoseOnTop();
    }

    // The props it sits on at home (worked out once, from where everything started)
    List<FlyingProp> Supports()
    {
        if (supports != null) return supports;
        supports = new List<FlyingProp>();
        Vector3 middle = homeBounds.center;
        foreach (FlyingProp other in all)
        {
            if (other == null || other == this) continue;
            Bounds below = other.homeBounds;
            bool underneath = below.max.y <= middle.y && below.max.y >= homeBounds.min.y - 0.05f;
            bool across = middle.x >= below.min.x - 0.03f && middle.x <= below.max.x + 0.03f &&
                          middle.z >= below.min.z - 0.03f && middle.z <= below.max.z + 0.03f;
            if (underneath && across) supports.Add(other);
        }
        return supports;
    }

    // How far below its home spot it should sit right now: 0 with what it sits on at home; otherwise down onto the
    // highest of those that is, or the table under it if none are
    float RestingDrop()
    {
        List<FlyingProp> under = Supports();
        if (under.Count == 0) return 0f;
        float top = float.MinValue;
        foreach (FlyingProp support in under)
        {
            if (support != null && support.state == State.Home) top = Mathf.Max(top, support.homeBounds.max.y - support.sunk);
        }
        if (top == float.MinValue)
        {
            // None of them: the table (the first solid thing under it that isn't a prop or a cat)
            RaycastHit[] hits = Physics.RaycastAll(homeBounds.center, Vector3.down, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<FlyingProp>() != null || hit.collider.GetComponentInParent<CatMovement>() != null) continue;
                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    top = hit.point.y;
                }
            }
            if (top == float.MinValue) return 0f;
        }
        return Mathf.Clamp(homeBounds.min.y - top, 0f, 1f);
    }

    // Props at home stacked on this one settle onto whatever is under them now (down when this leaves, up when it's back)
    void ResettleThoseOnTop()
    {
        foreach (FlyingProp other in all)
        {
            if (other == null || other == this || other.state != State.Home || !other.Supports().Contains(this)) continue;
            float drop = other.RestingDrop();
            if (Mathf.Abs(drop - other.sunk) < 0.005f) continue;
            other.StartCoroutine(other.SinkTo(drop));
        }
    }

    System.Collections.IEnumerator SinkTo(float drop)
    {
        Vector3 from = transform.position;
        Vector3 to = HomePosition() + Vector3.down * drop;
        sunk = drop;
        ResettleThoseOnTop();
        float t = 0;
        while (t < 1f && state == State.Home)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / 0.2f);
            transform.position = Vector3.Lerp(from, to, t * t);
            yield return null;
        }
    }

    Vector3 HomePosition() => homeParent != null ? homeParent.TransformPoint(homeLocalPosition) : homeLocalPosition;
    Quaternion HomeRotation() => homeParent != null ? homeParent.rotation * homeLocalRotation : homeLocalRotation;

    void SetVisible(bool visible)
    {
        foreach (Renderer rend in renderers)
        {
            if (rend != null) rend.enabled = visible;
        }
    }

    System.Collections.IEnumerator PopIn()
    {
        float t = 0;
        while (t < 0.35f && state == State.Home)
        {
            t += Time.deltaTime;
            transform.localScale = homeLocalScale * Mathf.Max(0.01f, SpaceCafeVisuals.EaseOutBack(t / 0.35f));
            yield return null;
        }
        if (state == State.Home) transform.localScale = homeLocalScale;
    }
}
