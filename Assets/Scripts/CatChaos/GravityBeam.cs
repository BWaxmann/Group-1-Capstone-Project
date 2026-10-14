using UnityEngine;

// Hold a launcher's grip to switch on its gravity beam: it grabs the flying prop nearest to where that
// launcher points, scores the catch, and holds the prop in front of you. Let go and the prop flies home.
// Sits on the player rig next to PlayerInput. Which hands can catch is set here; which can shoot is on PlayerInput.
[RequireComponent(typeof(PlayerInput))]
public class GravityBeam : MonoBehaviour
{
    [Tooltip("Which launchers' grips work the gravity beam.")]
    public bool catchWithLeft = true;

    public bool catchWithRight = true;

    [Range(5f, 45f)]
    [Tooltip("How far off the launcher's aim (in degrees) a flying prop can be and still get grabbed. Generous on purpose.")]
    public float aimAngle = 20f;

    [Min(1f)]
    public float range = 15f;

    [Min(0.3f)]
    [Tooltip("How far in front of the launcher a caught prop floats, in metres.")]
    public float holdDistance = 1.3f;

    [Min(0)]
    [Tooltip("Seconds the beam has to stay on a prop before it grabs it (a ring fills up around the prop meanwhile). " +
             "Stops grip-spamming; 0 grabs instantly.")]
    public float lockOnTime = 0.35f;

    [Min(0)]
    public int catchScore = 5;

    public Color beamColour = new Color(0.72f, 0.38f, 1f);

    class Hand
    {
        public bool isRight;
        public FlyingProp held;
        public float holdDistance;
        public FlyingProp locking;
        public float lockProgress;
        public LineRenderer beam;
        public LineRenderer glow;
        public LineRenderer lockRing;
    }

    const float LockRingRadius = 0.2f;

    const int BeamPoints = 12;

    PlayerInput player;
    Hand left;
    Hand right;

    void Start()
    {
        player = GetComponent<PlayerInput>();
        left = MakeHand(false);
        right = MakeHand(true);
        GameManager.instance.GameEnd += ReleaseAll;
    }

    void OnDestroy()
    {
        if (GameManager.instance != null) GameManager.instance.GameEnd -= ReleaseAll;
    }

    void Update()
    {
        bool playing = player.hasLaunchers && GameManager.instance.enablePlayerInput;
        UpdateHand(right, playing && catchWithRight);
        UpdateHand(left, playing && catchWithLeft);
    }

    void UpdateHand(Hand hand, bool allowed)
    {
        Transform muzzle = player.LaunchPoint(hand.isRight);
        bool beamOn = allowed && muzzle != null && player.GripHeld(hand.isRight);

        if (!beamOn)
        {
            Let(hand);
            hand.locking = null;
            hand.lockRing.enabled = false;
            SetVisible(hand, false);
            return;
        }

        SetVisible(hand, true);

        // Lost it some other way (e.g. the round ended)? Then look for something new
        if (hand.held != null && hand.held.state != FlyingProp.State.Held) hand.held = null;

        Vector3 start = muzzle.position;
        if (hand.held != null)
        {
            // Reel the prop in to a comfortable distance, then it follows where the launcher points
            hand.holdDistance = Mathf.MoveTowards(hand.holdDistance, holdDistance, Time.deltaTime * 6f);
            hand.held.SetHoldTarget(muzzle.position + muzzle.forward * hand.holdDistance);
            DrawBeam(hand.beam, start, hand.held.centre, 0.03f, 1f);
            hand.lockRing.enabled = false;
        }
        else
        {
            // Lock on: keep the beam on a prop while the ring fills, then it's grabbed
            FlyingProp target = FindTarget(muzzle, hand.locking);
            if (target != hand.locking)
            {
                hand.locking = target;
                hand.lockProgress = 0;
                if (target != null) Haptics.Pulse(hand.isRight, Haptics.Strength.Light);
            }

            if (target != null)
            {
                hand.lockProgress += Time.deltaTime;
                float fraction = lockOnTime > 0 ? hand.lockProgress / lockOnTime : 1f;
                DrawBeam(hand.beam, start, target.centre, Mathf.Lerp(0.008f, 0.02f, fraction), Mathf.Lerp(0.35f, 0.8f, fraction));
                ShowLockRing(hand, target.centre, fraction);
                if (fraction >= 1f)
                {
                    Grab(hand, target, muzzle);
                    hand.locking = null;
                    hand.lockRing.enabled = false;
                }
            }
            else
            {
                // Nothing in reach: a short, faint "searching" beam
                DrawBeam(hand.beam, start, start + muzzle.forward * 0.5f, 0.01f, 0.45f);
                hand.lockRing.enabled = false;
            }
        }

        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 10f);
        Color glowColour = beamColour;
        glowColour.a = hand.held != null ? 1f : pulse;
        hand.glow.startColor = hand.glow.endColor = glowColour;
    }

    // The flying prop nearest to where the launcher points. The one already being locked on to keeps priority
    // while it's still in reach, so the lock doesn't jump between props.
    FlyingProp FindTarget(Transform muzzle, FlyingProp current)
    {
        if (current != null && Score(muzzle, current) < float.MaxValue) return current;

        FlyingProp best = null;
        float bestScore = float.MaxValue;
        foreach (FlyingProp prop in FlyingProp.airborne)
        {
            float score = Score(muzzle, prop);
            if (score < bestScore)
            {
                bestScore = score;
                best = prop;
            }
        }
        return best;
    }

    // Mostly "closest to where you're pointing", nudged towards nearer props. MaxValue = out of reach.
    float Score(Transform muzzle, FlyingProp prop)
    {
        if (prop == null || !prop.isCatchable) return float.MaxValue;
        Vector3 toProp = prop.centre - muzzle.position;
        float distance = toProp.magnitude;
        if (distance > range || distance < 0.05f) return float.MaxValue;
        float angle = Vector3.Angle(muzzle.forward, toProp);
        if (angle > aimAngle) return float.MaxValue;
        return angle + distance * 0.5f;
    }

    void ShowLockRing(Hand hand, Vector3 at, float fraction)
    {
        Transform ring = hand.lockRing.transform;
        ring.position = at;
        if (Camera.main != null) ring.rotation = Quaternion.LookRotation(at - Camera.main.transform.position);
        SpaceCafeVisuals.SetRing(hand.lockRing, LockRingRadius, fraction);
        hand.lockRing.startColor = hand.lockRing.endColor = beamColour;
        hand.lockRing.enabled = true;
    }

    void Grab(Hand hand, FlyingProp prop, Transform muzzle)
    {
        // The other hand may have just got it
        if (!prop.isCatchable) return;
        hand.held = prop;
        hand.holdDistance = Vector3.Distance(muzzle.position, prop.centre);
        prop.Hold(prop.transform.position);

        GameManager.instance.AddBonus(catchScore, prop.centre);
        Haptics.Pulse(hand.isRight, Haptics.Strength.Medium);
        if (CatChaosSettings.instance != null) CatChaosSettings.instance.PlayCatch(prop.centre);
    }

    void Let(Hand hand)
    {
        if (hand.held != null)
        {
            hand.held.Release();
            hand.held = null;
        }
    }

    void ReleaseAll()
    {
        Let(left);
        Let(right);
    }

    // A slightly wobbly line, so it reads as an energy beam rather than a laser pointer
    void DrawBeam(LineRenderer line, Vector3 start, Vector3 end, float width, float alpha)
    {
        Vector3 direction = end - start;
        Vector3 side = Vector3.Cross(direction, Vector3.up);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
        side.Normalize();
        Vector3 up = Vector3.Cross(side, direction).normalized;

        for (int i = 0; i < BeamPoints; i++)
        {
            float t = i / (float)(BeamPoints - 1);
            float wobble = Mathf.Sin(t * Mathf.PI) * 0.025f;
            Vector3 offset = side * Mathf.Sin(t * 9f - Time.time * 14f) * wobble + up * Mathf.Cos(t * 7f - Time.time * 11f) * wobble;
            line.SetPosition(i, Vector3.Lerp(start, end, t) + offset);
        }

        line.widthMultiplier = width;
        Color colour = beamColour;
        colour.a = alpha;
        Color tip = colour;
        tip.a = alpha * 0.6f;
        line.startColor = colour;
        line.endColor = tip;
    }

    Hand MakeHand(bool isRight)
    {
        Hand hand = new Hand { isRight = isRight };
        Transform muzzle = player.LaunchPoint(isRight);

        GameObject beamObject = new GameObject(isRight ? "Gravity Beam Right" : "Gravity Beam Left");
        beamObject.transform.SetParent(transform, false);
        hand.beam = beamObject.AddComponent<LineRenderer>();
        hand.beam.sharedMaterial = SpaceCafeVisuals.UnlitMaterial(Color.white);
        hand.beam.useWorldSpace = true;
        hand.beam.positionCount = BeamPoints;
        hand.beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        hand.beam.receiveShadows = false;
        hand.beam.numCapVertices = 2;

        // The purple glow on the launcher while its beam is on
        hand.glow = SpaceCafeVisuals.Ring(muzzle, "Gravity Glow", 0.045f / Mathf.Max(0.0001f, muzzle.lossyScale.x), 0.012f, beamColour);

        // The ring that fills up around a prop while locking on (kept at the scene root so it isn't scaled)
        hand.lockRing = SpaceCafeVisuals.Ring(null, isRight ? "Lock Ring Right" : "Lock Ring Left", LockRingRadius, 0.015f, beamColour);
        hand.lockRing.enabled = false;

        SetVisible(hand, false);
        return hand;
    }

    static void SetVisible(Hand hand, bool visible)
    {
        if (hand.beam.enabled != visible) hand.beam.enabled = visible;
        if (hand.glow.enabled != visible) hand.glow.enabled = visible;
    }
}
