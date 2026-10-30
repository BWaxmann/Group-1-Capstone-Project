using UnityEngine;

// Small helpers for the Space Cafe's placeholder visuals (glowing rings, simple shapes).
// These are stand-ins until proper art is made.
public static class SpaceCafeVisuals
{
    static Shader unlitShader;

    // An unlit, see-through-capable material that's always included in builds
    public static Material UnlitMaterial(Color colour)
    {
        if (unlitShader == null)
        {
            unlitShader = Shader.Find("Sprites/Default");
            if (unlitShader == null) unlitShader = Shader.Find("UI/Default");
        }
        return new Material(unlitShader) { color = colour };
    }

    // A circle drawn with a line, in the XY plane of its parent
    public static LineRenderer Ring(Transform parent, string name, float radius, float width, Color colour, int segments = 48)
    {
        LineRenderer template = CafePrefabs.RingTemplate;
        if (template != null)
        {
            // Styled in the editor (its material); the circle, size and colour from here
            LineRenderer styled = Object.Instantiate(template, parent, false);
            styled.gameObject.SetActive(true);
            styled.enabled = true;
            styled.name = name;
            styled.useWorldSpace = false;
            styled.widthMultiplier = width;
            styled.startColor = colour;
            styled.endColor = colour;
            SetRing(styled, radius, 1f);
            return styled;
        }

        GameObject ringObject = new GameObject(name);
        ringObject.transform.SetParent(parent, false);

        LineRenderer line = ringObject.AddComponent<LineRenderer>();
        line.sharedMaterial = UnlitMaterial(Color.white);
        line.useWorldSpace = false;
        line.loop = true;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.widthMultiplier = width;
        line.startColor = colour;
        line.endColor = colour;
        SetRing(line, radius, 1f);
        return line;
    }

    // Redraws a ring; fraction < 1 draws only part of it (e.g. a timer running down)
    public static void SetRing(LineRenderer line, float radius, float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        line.loop = fraction >= 0.999f;
        int segments = Mathf.Max(2, Mathf.CeilToInt(48 * fraction));
        line.positionCount = segments;
        float sweep = Mathf.PI * 2f * fraction;
        for (int i = 0; i < segments; i++)
        {
            float t = line.loop ? i / (float)segments : i / (float)(segments - 1);
            float angle = Mathf.PI * 0.5f - t * sweep;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
        }
    }

    // A simple shape with no collider, e.g. for a bubble or a coin
    public static GameObject Shape(PrimitiveType type, Transform parent, string name, Color colour)
    {
        GameObject shape = GameObject.CreatePrimitive(type);
        Object.Destroy(shape.GetComponent<Collider>());
        shape.name = name;
        shape.transform.SetParent(parent, false);
        Renderer rend = shape.GetComponent<Renderer>();
        rend.sharedMaterial = UnlitMaterial(colour);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return shape;
    }

    // Tints a model without changing its shared material (works with most shaders)
    public static void Tint(GameObject target, Color colour)
    {
        foreach (Renderer rend in target.GetComponentsInChildren<Renderer>())
        {
            foreach (Material material in rend.materials)
            {
                if (material.HasProperty("_Color")) material.color = colour;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            }
        }
    }

    // Hangs a tinted tin model under a launcher (at the bottom-middle of the launcher's model)
    public static GameObject TinUnderLauncher(Transform launcher, GameObject tinPrefab, Color colour, float size)
    {
        Vector3 attach = BottomOfModel(launcher);

        GameObject mount = new GameObject("Food Tin");
        mount.transform.SetParent(launcher, false);
        mount.transform.localPosition = attach;
        mount.transform.localRotation = Quaternion.Euler(180f, 0, 0);    // hangs downwards

        GameObject tin;
        if (tinPrefab != null)
        {
            tin = Object.Instantiate(tinPrefab, mount.transform);
            foreach (Collider col in tin.GetComponentsInChildren<Collider>()) Object.Destroy(col);
            foreach (Rigidbody body in tin.GetComponentsInChildren<Rigidbody>()) Object.Destroy(body);
            FitToSize(tin, size);
            Tint(tin, colour);
        }
        else
        {
            tin = Shape(PrimitiveType.Cylinder, mount.transform, "Tin", colour);
            tin.transform.localScale = new Vector3(size, size * 0.6f, size);
            tin.transform.localPosition = Vector3.up * size * 0.6f;
        }

        // A glowing ring around the tin so the colour reads at a glance
        LineRenderer ring = Ring(mount.transform, "Colour Ring", size * 0.75f, 0.006f, colour);
        ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
        ring.transform.localPosition = Vector3.up * size * 0.5f;
        return mount;
    }

    // Scales a model so its largest side is the given size, sitting on its parent's origin
    static void FitToSize(GameObject model, float size)
    {
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer rend in renderers) bounds.Encapsulate(rend.bounds);

        float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.0001f);
        float scale = size / largest;
        model.transform.localScale = Vector3.one * scale;

        Vector3 bottomCentre = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        model.transform.position += (model.transform.parent.position - bottomCentre) * scale;
    }

    // The bottom-middle of a model, in its own space (ignores anything already hung under it)
    static Vector3 BottomOfModel(Transform root)
    {
        bool any = false;
        Bounds local = new Bounds();
        foreach (MeshFilter mesh in root.GetComponentsInChildren<MeshFilter>())
        {
            if (mesh.sharedMesh == null) continue;
            bool partOfTin = false;
            for (Transform t = mesh.transform; t != null && t != root; t = t.parent)
            {
                if (t.name == "Food Tin") partOfTin = true;
            }
            if (partOfTin) continue;
            Bounds b = mesh.sharedMesh.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? b.min.x : b.max.x,
                    (corner & 2) == 0 ? b.min.y : b.max.y,
                    (corner & 4) == 0 ? b.min.z : b.max.z);
                Vector3 inRoot = root.InverseTransformPoint(mesh.transform.TransformPoint(point));
                if (!any) { local = new Bounds(inRoot, Vector3.zero); any = true; }
                else local.Encapsulate(inRoot);
            }
        }
        return any ? new Vector3(local.center.x, local.min.y, local.center.z) : Vector3.down * 0.1f;
    }

    public static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
