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

    public static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
