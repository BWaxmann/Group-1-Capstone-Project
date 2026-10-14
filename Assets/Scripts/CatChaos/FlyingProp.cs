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

    public State state { get; private set; } = State.Home;
    public bool isCatchable => state == State.Flying;
    public Vector3 centre => propRenderer != null ? propRenderer.bounds.center : transform.position;

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

    void OnDisable()
    {
        airborne.Remove(this);
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

        velocity = Vector3.up * settings.launchUpSpeed * Random.Range(0.9f, 1.1f)
                 + drift * settings.launchTowardPlayerSpeed * Random.Range(0.8f, 1.2f);
        spinAxis = Random.onUnitSphere;
        spinSpeed = Random.Range(180f, 360f);

        // Fly free of the pile (keeping its size), so the pile can rattle without dragging it along
        StopAllCoroutines();
        transform.localScale = homeLocalScale;
        transform.SetParent(null, true);
        state = State.Flying;
        airborne.Add(this);
        settings.PlayKnock(transform.position);
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
                velocity += Vector3.down * settings.propGravity * dt;
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
