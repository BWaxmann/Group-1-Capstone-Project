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
        Landed
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

    Renderer[] renderers;
    Renderer propRenderer;
    Vector3 velocity;
    Vector3 spinAxis;
    float spinSpeed;
    float timer;

    // Flight: a burst upwards, then an erratic drift down
    float flightTime;
    bool drifting;
    float driftTimer;
    Vector3 driftVelocity;
    Vector3 viewer;
    Vector3 actionCentre;

    // Holding and returning
    Vector3 holdTarget;
    Vector3 returnStart;
    Quaternion returnStartRotation;

    public void Setup()
    {
        homeParent = transform.parent;
        homeLocalPosition = transform.localPosition;
        homeLocalRotation = transform.localRotation;
        homeLocalScale = transform.localScale;
        renderers = GetComponentsInChildren<Renderer>();
        propRenderer = renderers.Length > 0 ? renderers[0] : null;
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
    public bool Knock(Vector3 playerPosition)
    {
        CatChaosSettings settings = CatChaosSettings.instance;
        if (state != State.Home || settings == null || airborne.Count >= settings.maxAirborne) return false;

        Vector3 toPlayer = playerPosition - transform.position;
        toPlayer.y = 0;
        if (toPlayer.sqrMagnitude < 0.01f) toPlayer = -transform.forward;
        Vector3 drift = Quaternion.Euler(0, Random.Range(-settings.launchSpread, settings.launchSpread), 0) * toPlayer.normalized;

        // Fast and high, but never up into the ceiling: v = sqrt(2 g h) reaches exactly the room left above it
        float headroom = Mathf.Max(0.3f, settings.floorHeight + settings.maxHeightAboveFloor - transform.position.y);
        float up = Mathf.Min(settings.launchUpSpeed * Random.Range(0.9f, 1.1f), Mathf.Sqrt(2f * settings.propGravity * headroom));
        velocity = Vector3.up * up + drift * settings.launchTowardPlayerSpeed * Random.Range(0.8f, 1.2f);
        spinAxis = Random.onUnitSphere;
        spinSpeed = Random.Range(180f, 360f);
        flightTime = 0;
        drifting = false;
        viewer = playerPosition;
        // It drifts about over the space between the player and where it came from, so it stays in front of them
        Vector3 fromPlayer = transform.position - playerPosition;
        fromPlayer.y = 0;
        actionCentre = playerPosition + fromPlayer * 0.6f;

        // Fly free of the pile (keeping its size), so the pile can rattle without dragging it along
        StopAllCoroutines();
        transform.localScale = homeLocalScale;
        transform.SetParent(null, true);
        state = State.Flying;
        airborne.Add(this);
        settings.PlayKnock(transform.position);
        if (burstPieces != null && burstPieces.Length > 0 && burstCount > 0)
        {
            FlutterPiece.Burst(burstPieces, burstCount, burstPieceSize, centre, settings.floorHeight);
        }
        return true;
    }

    // The gravity beam has grabbed it
    public void Hold(Vector3 target)
    {
        if (state != State.Flying) return;
        state = State.Held;
        holdTarget = target;
        velocity = Vector3.zero;
    }

    public void SetHoldTarget(Vector3 target)
    {
        holdTarget = target;
    }

    // Let go of the beam: the prop flies back to its spot on the pile
    public void Release()
    {
        if (state != State.Held) return;
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

        switch (state)
        {
            case State.Flying:
                flightTime += dt;
                if (!drifting)
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
                    float sink = Mathf.MoveTowards(velocity.y, -settings.driftFallSpeed, settings.propGravity * dt);
                    velocity = new Vector3(sideways.x, sink, sideways.z);
                }

                transform.position += velocity * dt;
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
                break;

            case State.Returning:
                timer += dt;
                float t = Mathf.Clamp01(timer / settings.returnDuration);
                float eased = t * t * (3f - 2f * t);
                Vector3 home = HomePosition();
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
        return direction.sqrMagnitude > 0.0001f ? direction.normalized * settings.driftSpeed * Random.Range(0.6f, 1.2f) : Vector3.zero;
    }

    void Land(CatChaosSettings settings)
    {
        airborne.Remove(this);
        state = State.Landed;
        timer = 0;
        SetVisible(false);
        settings.PlayLanded(transform.position);
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
