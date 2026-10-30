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

    [SerializeField]
    [Tooltip("Optional. The splat where a treat hits the cafe (walls, floor, furniture). Spawned facing out from the surface. Empty = a small puff of the cats' eating crumbs.")]
    ParticleSystem splat;

    [Header("Aiming")]

    [SerializeField]
    [Tooltip("Optional. The reticle shown where a shot will land: an AimReticle with its Outer Ring and Inner Dot LineRenderers set, each drawn at a radius of 1 (they're scaled to size, so any shape works). Empty = two circles drawn in code.")]
    AimReticle reticle;

    [SerializeField]
    [Tooltip("Optional. A LineRenderer that styles the catch beam from the launcher to a prop (material, width, colours). Its points are drawn by code. Empty = a plain glowing line.")]
    LineRenderer beamTemplate;

    public static FloatingWord FloatingWordPrefab => instance != null ? instance.floatingWord : null;
    public static LineRenderer RingTemplate => instance != null ? instance.ringTemplate : null;
    public static TrailRenderer KittenTrailTemplate => instance != null ? instance.kittenTrailTemplate : null;
    public static TrailRenderer PropTrailTemplate => instance != null ? instance.propTrailTemplate : null;
    public static ParticleSystem SplatPrefab => instance != null ? instance.splat : null;
    public static AimReticle ReticlePrefab => instance != null ? instance.reticle : null;
    public static LineRenderer BeamTemplate => instance != null ? instance.beamTemplate : null;

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
