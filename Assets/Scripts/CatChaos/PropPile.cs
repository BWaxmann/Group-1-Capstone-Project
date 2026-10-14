using System.Collections.Generic;
using UnityEngine;

// A pile of cafe props (plates, cups, jars...) that replaces a defend point's treat tin.
// When a cat decides to go for it, the pile rattles and a ring under it glows, so the player gets a warning.
// Each paw swipe that lands sends one prop flying (if the air isn't already full).
[RequireComponent(typeof(DefendPoint))]
public class PropPile : MonoBehaviour
{
    [Tooltip("The object holding the props; it's what rattles.")]
    public Transform pile;

    [Tooltip("The props on this pile, knocked off top first.")]
    public List<FlyingProp> props = new List<FlyingProp>();

    [Min(0.05f)]
    public float warningRingRadius = 0.3f;

    [Tooltip("How far away (in metres) a cat heading here starts the warning.")]
    [Min(0.5f)]
    public float warningDistance = 6f;

    public Color warningColour = new Color(1f, 0.55f, 0.1f);
    public Color dangerColour = new Color(1f, 0.15f, 0.1f);

    DefendPoint defendPoint;
    LineRenderer warningRing;
    Vector3 pileRestPosition;
    Quaternion pileRestRotation;
    float warning;          // 0 = no cat coming, 1 = a cat is right here
    float checkTimer;
    float shake;            // extra rattle from a swipe that couldn't knock anything off
    Transform viewer;

    void Awake()
    {
        defendPoint = GetComponent<DefendPoint>();
        if (pile != null)
        {
            pileRestPosition = pile.localPosition;
            pileRestRotation = pile.localRotation;

            // A flat ring on the surface under the pile
            Transform ringHolder = new GameObject("Warning Ring").transform;
            ringHolder.SetParent(pile.parent, false);
            ringHolder.localPosition = pile.localPosition + Vector3.up * 0.01f;
            ringHolder.localRotation = Quaternion.Euler(90f, 0, 0);
            float scale = Mathf.Max(0.0001f, pile.parent.lossyScale.x);
            warningRing = SpaceCafeVisuals.Ring(ringHolder, "Ring", warningRingRadius / scale, 0.025f, warningColour);
            warningRing.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        if (defendPoint != null) defendPoint.Hit += OnHit;
    }

    void OnDisable()
    {
        if (defendPoint != null) defendPoint.Hit -= OnHit;
    }

    void OnHit()
    {
        Vector3 playerPosition = Viewer() != null ? viewer.position : transform.position - transform.forward;

        // The topmost prop that's still home goes flying
        FlyingProp top = null;
        foreach (FlyingProp prop in props)
        {
            if (prop == null || prop.state != FlyingProp.State.Home) continue;
            if (top == null || prop.transform.position.y > top.transform.position.y) top = prop;
        }

        if (top == null || !top.Knock(playerPosition))
        {
            shake = 1f;
        }
    }

    void Update()
    {
        if (pile == null) return;

        // Checking every few frames is plenty for a warning
        checkTimer -= Time.deltaTime;
        if (checkTimer <= 0)
        {
            checkTimer = 0.2f;
            warning = 0;
            foreach (CatMovement cat in CatMovement.active)
            {
                if (cat == null || cat.headingFor != defendPoint) continue;
                float distance = Vector3.Distance(cat.transform.position, transform.position);
                warning = Mathf.Max(warning, 1f - Mathf.Clamp01((distance - 0.8f) / warningDistance));
            }
        }

        shake = Mathf.MoveTowards(shake, 0, Time.deltaTime * 3f);
        float rattle = Mathf.Max(warning * warning * 0.6f, shake);

        if (rattle > 0.01f)
        {
            float t = Time.time * 38f;
            pile.localRotation = pileRestRotation * Quaternion.Euler(Mathf.Sin(t) * 4f * rattle, 0, Mathf.Cos(t * 1.3f) * 4f * rattle);
            pile.localPosition = pileRestPosition + new Vector3(0, Mathf.Abs(Mathf.Sin(t * 0.9f)) * 0.01f * rattle, 0);
        }
        else
        {
            pile.localRotation = pileRestRotation;
            pile.localPosition = pileRestPosition;
        }

        if (warningRing != null)
        {
            bool show = warning > 0.01f;
            if (warningRing.gameObject.activeSelf != show) warningRing.gameObject.SetActive(show);
            if (show)
            {
                // Pulses faster and redder as the cat gets closer
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(4f, 14f, warning));
                Color colour = Color.Lerp(warningColour, dangerColour, warning);
                colour.a = Mathf.Lerp(0.35f, 1f, pulse);
                warningRing.startColor = warningRing.endColor = colour;
                // Line width is in world units (it ignores scale), unlike the ring's points
                warningRing.widthMultiplier = Mathf.Lerp(0.02f, 0.05f, warning);
            }
        }
    }

    Transform Viewer()
    {
        if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
        return viewer;
    }
}
