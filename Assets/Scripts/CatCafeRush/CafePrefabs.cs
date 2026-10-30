using UnityEngine;

// Cat Cafe Waves: the prefabs the game's scripts spawn, set up in the editor on one scene object ("Cafe Prefabs").
// A slot left empty is built in code at runtime instead, as before, so the game keeps working while prefabs are made.
public class CafePrefabs : MonoBehaviour
{
    public static CafePrefabs instance;

    [Header("UI")]

    [SerializeField]
    [Tooltip("A pop-up word (score, Shake!, Shoo!, tips...). Its TextMeshPro sets the font and style; the text, colour and size are set per word.")]
    FloatingWord floatingWord;

    [Header("Effects")]

    [SerializeField]
    [Tooltip("Optional. A LineRenderer whose material styles every ring the game draws (patience, warning, drop zone, spy nerve, heist). The circle, its size and colour are set by code.")]
    LineRenderer ringTemplate;

    [SerializeField]
    [Tooltip("Optional. A TrailRenderer that styles the kittens' dust trail.")]
    TrailRenderer kittenTrailTemplate;

    [SerializeField]
    [Tooltip("Optional. A TrailRenderer that styles the trail behind a flying prop.")]
    TrailRenderer propTrailTemplate;

    public static FloatingWord FloatingWordPrefab => instance != null ? instance.floatingWord : null;
    public static LineRenderer RingTemplate => instance != null ? instance.ringTemplate : null;
    public static TrailRenderer KittenTrailTemplate => instance != null ? instance.kittenTrailTemplate : null;
    public static TrailRenderer PropTrailTemplate => instance != null ? instance.propTrailTemplate : null;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
}
