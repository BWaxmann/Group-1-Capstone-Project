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
    public int catchScore = 5;

    public Color beamColour = new Color(0.72f, 0.38f, 1f);

    class Hand
    {
        public bool isRight;
        public FlyingProp held;
        public float holdDistance;
        public LineRenderer beam;
        public LineRenderer glow;
    }

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
            SetVisible(hand, false);
            return;
        }

        SetVisible(hand, true);

        // Lost it some other way (e.g. the round ended)? Then look for something new
        if (hand.held != null && hand.held.state != FlyingProp.State.Held) hand.held = null;

        if (hand.held == null)
        {
            FlyingProp target = FindTarget(muzzle);
            if (target != null) Grab(hand, target, muzzle);
        }

        Vector3 start = muzzle.position;
        Vector3 end;
        if (hand.held != null)
        {
            // Reel the prop in to a comfortable distance, then it follows where the launcher points
            hand.holdDistance = Mathf.MoveTowards(hand.holdDistance, holdDistance, Time.deltaTime * 6f);
            hand.held.SetHoldTarget(muzzle.position + muzzle.forward * hand.holdDistance);
            end = hand.held.centre;
            DrawBeam(hand.beam, start, end, 0.03f, 1f);
        }
        else
        {
            // Nothing caught yet: a short, faint "searching" beam
            end = start + muzzle.forward * 0.5f;
            DrawBeam(hand.beam, start, end, 0.01f, 0.45f);
        }

        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 10f);
        Color glowColour = beamColour;
        glowColour.a = hand.held != null ? 1f : pulse;
        hand.glow.startColor = hand.glow.endColor = glowColour;
    }

    FlyingProp FindTarget(Transform muzzle)
    {
        FlyingProp best = null;
        float bestScore = float.MaxValue;
        foreach (FlyingProp prop in FlyingProp.airborne)
        {
            if (prop == null || !prop.isCatchable) continue;
            Vector3 toProp = prop.centre - muzzle.position;
            float distance = toProp.magnitude;
            if (distance > range || distance < 0.05f) continue;
            float angle = Vector3.Angle(muzzle.forward, toProp);
            if (angle > aimAngle) continue;

            // Mostly "closest to where you're pointing", nudged towards nearer props
            float score = angle + distance * 0.5f;
            if (score < bestScore)
            {
                bestScore = score;
                best = prop;
            }
        }
        return best;
    }

    void Grab(Hand hand, FlyingProp prop, Transform muzzle)
    {
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

        SetVisible(hand, false);
        return hand;
    }

    static void SetVisible(Hand hand, bool visible)
    {
        if (hand.beam.enabled != visible) hand.beam.enabled = visible;
        if (hand.glow.enabled != visible) hand.glow.enabled = visible;
    }
}
