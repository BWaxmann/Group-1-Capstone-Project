using System.Collections;
using UnityEngine;

// Placeholder portal: glowing rings that slowly spin, and flare up when a cat comes through.
// The ring faces along this object's blue arrow (Z).
public class PortalRing : MonoBehaviour
{
    [SerializeField]
    [Min(0.05f)]
    float radius = 0.3f;

    [SerializeField]
    Color colour = new Color(0.6f, 0.3f, 1f, 1f);

    [SerializeField]
    Color flareColour = new Color(0.4f, 1f, 1f, 1f);

    [SerializeField]
    [Tooltip("Degrees per second.")]
    float spinSpeed = 40f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Brightness while no cat is using the portal.")]
    float idleBrightness = 0.35f;

    LineRenderer outer;
    LineRenderer inner;
    Transform spinner;
    float flare;
    float active;

    void Awake()
    {
        spinner = new GameObject("Spinner").transform;
        spinner.SetParent(transform, false);
        outer = SpaceCafeVisuals.Ring(spinner, "Outer Ring", radius, radius * 0.08f, colour);
        inner = SpaceCafeVisuals.Ring(spinner, "Inner Ring", radius * 0.75f, radius * 0.04f, colour, 6);
        Apply();
    }

    // A cat is coming through: bright flash, then stays lit while the cat is out
    public void Open()
    {
        active = 1f;
        StopAllCoroutines();
        StartCoroutine(FlareRoutine());
    }

    public void Close()
    {
        active = 0f;
        StopAllCoroutines();
        StartCoroutine(FlareRoutine());
    }

    IEnumerator FlareRoutine()
    {
        flare = 1f;
        while (flare > 0)
        {
            flare = Mathf.Max(0, flare - Time.deltaTime * 2.5f);
            Apply();
            yield return null;
        }
    }

    void Update()
    {
        spinner.Rotate(0, 0, spinSpeed * (1f + active + flare * 4f) * Time.deltaTime, Space.Self);
    }

    void Apply()
    {
        float brightness = Mathf.Lerp(idleBrightness, 1f, Mathf.Max(active, flare));
        Color c = Color.Lerp(colour, flareColour, flare);
        c.a = brightness;
        outer.startColor = outer.endColor = c;
        inner.startColor = inner.endColor = c;
        spinner.localScale = Vector3.one * (1f + flare * 0.35f);
    }
}
