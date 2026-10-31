using UnityEngine;

// Shows a ring where a launcher's shot will land, for both straight and arcing shots.
// The ring turns gold and grows when it's over a hungry cat.
// Created by PlayerInput for each launcher, from the Cafe Prefabs reticle if one is set (its own rings, any shape, drawn
// at a radius of 1 and scaled to size); otherwise two circles drawn in code.
public class AimReticle : MonoBehaviour
{
    const int RingSegments = 40;
    const float SimulationStep = 0.02f;
    const float MaxFlightTime = 3f;
    const float MaxStraightDistance = 50f;

    [SerializeField]
    [Tooltip("Ring size as a fraction of distance, so it looks the same size near and far.")]
    float angularSize = 0.015f;

    [SerializeField]
    [Min(0)]
    float minimumRadius = 0.02f;

    [SerializeField]
    Color normalColour = new Color(1f, 1f, 1f, 0.8f);

    [SerializeField]
    Color onCatColour = new Color(1f, 0.84f, 0f, 1f);

    [SerializeField]
    [Min(1)]
    float onCatScale = 1.4f;

    [SerializeField]
    [Min(1)]
    [Tooltip("A spread shot widens the ring, but never past this many times its usual size (a full-size cone is distracting).")]
    float maxSpreadGrowth = 1.6f;

    // e.g. each launcher's reticle in the colour that launcher fires (it still grows over a cat)
    public void SetColours(Color normal, Color onCat)
    {
        normalColour = normal;
        onCatColour = onCat;
    }

    float spreadDegrees;

    // A spread shot's cone (degrees either side): the ring widens to show where the volley can land
    public void SetSpread(float degrees)
    {
        spreadDegrees = Mathf.Max(0f, degrees);
    }

    [SerializeField]
    [Tooltip("Optional, for a reticle prefab: the ring, drawn at a radius of 1. Empty = a circle drawn in code.")]
    LineRenderer outerRing;

    [SerializeField]
    [Tooltip("Optional, for a reticle prefab: the centre dot, drawn at a radius of 1. Empty = a circle drawn in code.")]
    LineRenderer innerDot;

    Transform launchPoint;
    Transform ignoreRoot;
    float speed;
    bool useGravity;
    bool visible = true;
    bool outerFromPrefab;
    bool innerFromPrefab;

    public static AimReticle Create(Transform launchPoint, Transform ignoreRoot, string name)
    {
        AimReticle prefab = CafePrefabs.ReticlePrefab;
        AimReticle reticle;
        if (prefab != null)
        {
            reticle = Instantiate(prefab);
        }
        else
        {
            reticle = new GameObject(name).AddComponent<AimReticle>();
        }
        reticle.name = name;
        reticle.gameObject.SetActive(true);
        reticle.launchPoint = launchPoint;
        reticle.ignoreRoot = ignoreRoot;
        return reticle;
    }

    public void Configure(float shotSpeed, bool shotUsesGravity)
    {
        speed = shotSpeed;
        useGravity = shotUsesGravity;
    }

    public void SetVisible(bool show)
    {
        visible = show;
        if (!show)
        {
            ShowRings(false);
        }
    }

    void Awake()
    {
        outerFromPrefab = outerRing != null;
        innerFromPrefab = innerDot != null;
        if (outerFromPrefab && innerFromPrefab)
        {
            return;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("UI/Default");
        }
        Material material = new Material(shader);

        if (!outerFromPrefab)
        {
            outerRing = CreateRing("Ring", material, 1f, RingSegments);
        }
        if (!innerFromPrefab)
        {
            innerDot = CreateRing("Dot", material, 0.2f, 12);
        }
    }

    LineRenderer CreateRing(string ringName, Material material, float radius, int segments)
    {
        GameObject ringObject = new GameObject(ringName);
        ringObject.transform.SetParent(transform, false);

        LineRenderer line = ringObject.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.loop = true;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.positionCount = segments;
        line.numCornerVertices = 2;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
        }
        return line;
    }

    void LateUpdate()
    {
        if (!visible || launchPoint == null || !launchPoint.gameObject.activeInHierarchy)
        {
            ShowRings(false);
            return;
        }

        if (!FindLandingPoint(out RaycastHit hit))
        {
            ShowRings(false);
            return;
        }

        CatMovement cat = hit.collider.GetComponentInParent<CatMovement>();
        bool onCat = cat != null && !cat.isFullyFed;

        float distance = Vector3.Distance(launchPoint.position, hit.point);
        float usual = Mathf.Max(minimumRadius, distance * angularSize);
        float spread = Mathf.Min(distance * Mathf.Tan(spreadDegrees * Mathf.Deg2Rad), usual * maxSpreadGrowth);
        float radius = Mathf.Max(usual, spread) * (onCat ? onCatScale : 1f);

        transform.position = hit.point + hit.normal * 0.01f;
        transform.rotation = Quaternion.LookRotation(hit.normal);

        Color colour;
        if (onCat)
        {
            colour = onCatColour;
        }
        else
        {
            colour = normalColour;
        }
        float width = radius * 0.15f;
        SetLine(outerRing, colour, width, radius, outerFromPrefab);
        SetLine(innerDot, colour, width, radius * 0.2f, innerFromPrefab);
        ShowRings(true);
    }

    // Line widths don't follow the object's scale, so a ring drawn here is redrawn at its real size; a prefab's ring
    // (any shape, drawn at a radius of 1) is scaled instead, so its shape stays
    void SetLine(LineRenderer line, Color colour, float width, float radius, bool fromPrefab)
    {
        line.startColor = colour;
        line.endColor = colour;
        line.widthMultiplier = width;
        if (fromPrefab)
        {
            line.transform.localScale = Vector3.one * radius;
            return;
        }

        int segments = line.positionCount;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
        }
    }

    void ShowRings(bool show)
    {
        if (outerRing != null && outerRing.enabled != show)
        {
            outerRing.enabled = show;
        }
        if (innerDot != null && innerDot.enabled != show)
        {
            innerDot.enabled = show;
        }
    }

    // Follows the same path the treat will take and returns the first thing it would hit
    bool FindLandingPoint(out RaycastHit landing)
    {
        Vector3 position = launchPoint.position;
        Vector3 velocity = launchPoint.forward * speed;

        if (!useGravity || speed <= 0)
        {
            return CastSegment(position, launchPoint.forward, MaxStraightDistance, out landing);
        }

        Vector3 gravity = Physics.gravity;
        for (float t = 0; t < MaxFlightTime; t += SimulationStep)
        {
            Vector3 next = position + velocity * SimulationStep + 0.5f * gravity * SimulationStep * SimulationStep;
            Vector3 step = next - position;
            if (CastSegment(position, step.normalized, step.magnitude, out landing))
            {
                return true;
            }
            position = next;
            velocity += gravity * SimulationStep;
        }

        landing = default(RaycastHit);
        return false;
    }

    bool CastSegment(Vector3 origin, Vector3 direction, float distance, out RaycastHit closest)
    {
        closest = default(RaycastHit);
        // With clean shots, treats fly through furniture, so the ring shows on a cat behind it (cat hitboxes are triggers)
        bool cleanShots = Treat.cleanShots;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, Physics.DefaultRaycastLayers,
                                               cleanShots ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);

        bool found = false;
        bool foundCat = false;
        float closestDistance = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            if (ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot))
            {
                continue;
            }
            if (hit.collider.GetComponentInParent<Launchable>() != null)
            {
                continue;
            }
            bool isCat = cleanShots && hit.collider.GetComponentInParent<CatMovement>() != null;
            if (!isCat && hit.collider.isTrigger)
            {
                continue;
            }
            // A cat anywhere along the shot beats anything else; otherwise the nearest thing
            if (foundCat && !isCat)
            {
                continue;
            }
            if (hit.distance >= closestDistance && !(isCat && !foundCat))
            {
                continue;
            }

            closest = hit;
            closestDistance = hit.distance;
            found = true;
            foundCat |= isCat;
        }
        return found;
    }
}
