using UnityEngine;

// Shows a ring where a launcher's shot will land, for both straight and arcing shots.
// The ring turns gold and grows when it's over a hungry cat.
// Created by PlayerInput for each launcher; no prefab or scene setup needed.
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

    Transform launchPoint;
    Transform ignoreRoot;
    float speed;
    bool useGravity;

    LineRenderer outerRing;
    LineRenderer innerDot;
    bool visible = true;

    public static AimReticle Create(Transform launchPoint, Transform ignoreRoot, string name)
    {
        AimReticle reticle = new GameObject(name).AddComponent<AimReticle>();
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
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("UI/Default");
        Material material = new Material(shader);

        outerRing = CreateRing("Ring", material, 1f, RingSegments);
        innerDot = CreateRing("Dot", material, 0.2f, 12);
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
        float radius = Mathf.Max(minimumRadius, distance * angularSize, distance * Mathf.Tan(spreadDegrees * Mathf.Deg2Rad)) * (onCat ? onCatScale : 1f);

        transform.position = hit.point + hit.normal * 0.01f;
        transform.rotation = Quaternion.LookRotation(hit.normal);

        Color colour = onCat ? onCatColour : normalColour;
        float width = radius * 0.15f;
        SetLine(outerRing, colour, width, radius);
        SetLine(innerDot, colour, width, radius * 0.2f);
        ShowRings(true);
    }

    // Line widths don't follow the object's scale, so the ring is redrawn at its real size instead
    void SetLine(LineRenderer line, Color colour, float width, float radius)
    {
        line.startColor = colour;
        line.endColor = colour;
        line.widthMultiplier = width;

        int segments = line.positionCount;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
        }
    }

    void ShowRings(bool show)
    {
        if (outerRing != null && outerRing.enabled != show) outerRing.enabled = show;
        if (innerDot != null && innerDot.enabled != show) innerDot.enabled = show;
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
            if (ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot)) continue;
            if (hit.collider.GetComponentInParent<Launchable>() != null) continue;
            bool isCat = cleanShots && hit.collider.GetComponentInParent<CatMovement>() != null;
            if (!isCat && hit.collider.isTrigger) continue;
            // A cat anywhere along the shot beats anything else; otherwise the nearest thing
            if (foundCat && !isCat) continue;
            if (hit.distance >= closestDistance && !(isCat && !foundCat)) continue;

            closest = hit;
            closestDistance = hit.distance;
            found = true;
            foundCat |= isCat;
        }
        return found;
    }
}
