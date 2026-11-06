using UnityEngine;

// Shows a ring where a launcher's shot will land, for both straight and arcing shots.
// The ring turns gold and grows when it's over a hungry cat.
// Created by PlayerInput for each launcher, from the Cafe Prefabs reticle if one is set (its own rings, any shape, drawn
// at a radius of 1 and scaled to size); otherwise two circles drawn in code.
public class AimReticle : MonoBehaviour
{
    const float SimulationStep = 0.02f;
    const float MaxFlightTime = 3f;
    const float MaxStraightDistance = 50f;

    [SerializeField]
    Color normalColour = new Color(1f, 1f, 1f, 0.8f);

    [SerializeField]
    Color onCatColour = new Color(1f, 0.84f, 0f, 1f);

    MeshRenderer mRend;
    Material mat;

    void Start()
    {
        mRend = GetComponent<MeshRenderer>();
        mat = mRend.material;
    }

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

    void LateUpdate()
    {
        if (!FindLandingPoint(out RaycastHit hit))
        {
            return;
        }

        CatMovement cat = hit.collider.GetComponentInParent<CatMovement>();
        bool onCat = cat != null && !cat.isFullyFed;

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
        mat.color = colour;
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

        landing = default;
        return false;
    }

    bool CastSegment(Vector3 origin, Vector3 direction, float distance, out RaycastHit closest)
    {
        closest = default;
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
