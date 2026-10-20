using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A spot in the cafe that cats pop out of (cat houses, vents, holes in the couch...).
// Place it at the bottom-middle of the opening, with the blue arrow (Z) pointing out into the room.
// Add one with GameObject > Catastrophe Cafe > Cat Hole.
public class CatHole : MonoBehaviour
{
    public static readonly List<CatHole> all = new List<CatHole>();

    [SerializeField]
    [Tooltip("How far the cat comes out of the hole, in metres.")]
    float popOutDistance = 0.12f;

    [SerializeField]
    [Min(0.1f)]
    [Tooltip("Size of the cat for this hole. Use less than 1 for small holes.")]
    float catScale = 1f;

    [SerializeField]
    [Tooltip("Optional. This object shakes just before a cat pops out (e.g. the cat house). Must not be marked Static.")]
    Transform rattleTarget;

    [SerializeField]
    [Min(0)]
    float rattleAmount = 0.015f;

    [SerializeField]
    [Tooltip("Show glowing eyes in the hole just before a cat pops out.")]
    bool showEyes = true;

    [SerializeField]
    Color eyeColour = new Color(1f, 0.85f, 0.2f);

    public PeekabooCat occupant { get; set; }
    public bool isFree => occupant == null || !occupant.gameObject.activeInHierarchy;
    public float outDistance => popOutDistance;
    public float scale => catScale;

    GameObject eyes;
    Material eyeMaterial;
    bool hinting;

    // Faint eyes: "a cat will come out of here next"
    public void SetNextHint(bool show)
    {
        hinting = show;
        if (!showEyes) return;
        ShowEyes(show);
        if (show) SetEyeBrightness(0.35f);
    }

    void SetEyeBrightness(float alpha)
    {
        if (eyeMaterial != null)
        {
            Color colour = eyeColour;
            colour.a = alpha;
            eyeMaterial.color = colour;
        }
    }

    void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    void OnDisable()
    {
        all.Remove(this);
    }

    // The warning before a cat appears: the hole shakes and eyes glow in the dark
    public void Warn(float duration)
    {
        StartCoroutine(WarnRoutine(duration));
    }

    IEnumerator WarnRoutine(float duration)
    {
        hinting = false;
        if (showEyes)
        {
            ShowEyes(true);
            SetEyeBrightness(1f);
        }

        Vector3 rattleStart = rattleTarget != null ? rattleTarget.localPosition : Vector3.zero;
        float timer = 0;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            if (rattleTarget != null)
            {
                rattleTarget.localPosition = rattleStart + Random.insideUnitSphere * rattleAmount;
            }
            yield return null;
        }

        if (rattleTarget != null)
        {
            rattleTarget.localPosition = rattleStart;
        }
        if (!hinting)
        {
            ShowEyes(false);
        }
    }

    void ShowEyes(bool show)
    {
        if (show && eyes == null)
        {
            eyes = CreateEyes();
        }
        if (eyes != null)
        {
            eyes.SetActive(show);
        }
    }

    GameObject CreateEyes()
    {
        GameObject pair = new GameObject("Glowing Eyes");
        pair.transform.SetParent(transform, false);
        pair.transform.localPosition = new Vector3(0, 0.12f * catScale, -0.02f);

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("UI/Default");
        Material material = new Material(shader) { color = eyeColour };
        eyeMaterial = material;

        for (int i = -1; i <= 1; i += 2)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(eye.GetComponent<Collider>());
            eye.name = "Eye";
            eye.transform.SetParent(pair.transform, false);
            eye.transform.localPosition = new Vector3(i * 0.035f * catScale, 0, 0);
            eye.transform.localScale = new Vector3(0.025f, 0.018f, 0.01f) * catScale;
            eye.GetComponent<Renderer>().sharedMaterial = material;
        }
        return pair;
    }

    [ContextMenu("Test: pop a cat out of this hole (Play mode)")]
    void TestPop()
    {
        if (Application.isPlaying && PeekabooPackSpawner.instance != null)
        {
            PeekabooPackSpawner.instance.PopAt(this);
        }
        else
        {
            Debug.Log("[CatHole] Press Play first, then use this to preview a cat popping out here.", this);
        }
    }

    void OnDrawGizmos()
    {
        // A rough cat-sized box and an arrow showing which way the cat comes out
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one * catScale);
        Gizmos.DrawWireCube(new Vector3(0, 0.15f, 0), new Vector3(0.22f, 0.3f, 0.22f));
        Gizmos.matrix = Matrix4x4.identity;

        Vector3 tip = transform.position + transform.forward * (0.3f + popOutDistance);
        Gizmos.DrawLine(transform.position, tip);
        Gizmos.DrawLine(tip, tip - transform.forward * 0.08f + transform.right * 0.05f);
        Gizmos.DrawLine(tip, tip - transform.forward * 0.08f - transform.right * 0.05f);
    }
}
