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

    [Header("Cats")]

    [SerializeField]
    [Tooltip("Optional. The speech bubble over a customer: an OrderBubble prefab with its Bubble (the art that pops and shakes), Patience Ring, icon and queue anchors, chip and face set as wanted. Anything left unset on it is built in code.")]
    OrderBubble orderBubble;

    public static OrderBubble OrderBubblePrefab
    {
        get
        {
            if (instance != null)
            {
                return instance.orderBubble;
            }
            return null;
        }
    }

    [Header("Tips")]

    [SerializeField]
    [Tooltip("Optional. The tip jar: a TipJar prefab with its Body, Fill, Lid and Coin set as wanted. Anything left unset on it is built from simple shapes.")]
    TipJar tipJar;

    public static TipJar TipJarPrefab
    {
        get
        {
            if (instance != null)
            {
                return instance.tipJar;
            }
            return null;
        }
    }

    [Header("Spies")]

    [SerializeField]
    [Tooltip("Optional. A LineRenderer that styles the spy's rope (its two ends are set by code).")]
    LineRenderer ropeTemplate;

    [SerializeField]
    [Tooltip("Optional. The secret recipe a spy makes off with, held out in its paws. Empty = a card with 'SECRET RECIPE' on it.")]
    GameObject recipe;

    public static LineRenderer RopeTemplate
    {
        get
        {
            if (instance != null)
            {
                return instance.ropeTemplate;
            }
            return null;
        }
    }

    public static GameObject RecipePrefab
    {
        get
        {
            if (instance != null)
            {
                return instance.recipe;
            }
            return null;
        }
    }

    [Header("Aiming")]

    [SerializeField]
    [Tooltip("Optional. The reticle shown where a shot will land: an AimReticle with its Outer Ring and Inner Dot LineRenderers set, each drawn at a radius of 1 (they're scaled to size, so any shape works). Empty = two circles drawn in code.")]
    AimReticle reticle;

    [SerializeField]
    [Tooltip("Optional. A LineRenderer that styles the catch beam from the launcher to a prop (material, width, colours). Its points are drawn by code. Empty = a plain glowing line.")]
    LineRenderer beamTemplate;

    public static FloatingWord FloatingWordPrefab
    {
        get
        {
            if (instance != null)
            {
                return instance.floatingWord;
            }
            return null;
        }
    }

    public static LineRenderer RingTemplate
    {
        get
        {
            if (instance != null)
            {
                return instance.ringTemplate;
            }
            return null;
        }
    }

    public static TrailRenderer KittenTrailTemplate
    {
        get
        {
            if (instance != null)
            {
                return instance.kittenTrailTemplate;
            }
            return null;
        }
    }

    public static TrailRenderer PropTrailTemplate
    {
        get
        {
            if (instance != null)
            {
                return instance.propTrailTemplate;
            }
            return null;
        }
    }

    public static ParticleSystem SplatPrefab
    {
        get
        {
            if (instance != null)
            {
                return instance.splat;
            }
            return null;
        }
    }

    public static AimReticle ReticlePrefab
    {
        get
        {
            if (instance != null)
            {
                return instance.reticle;
            }
            return null;
        }
    }

    public static LineRenderer BeamTemplate
    {
        get
        {
            if (instance != null)
            {
                return instance.beamTemplate;
            }
            return null;
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            Debug.Log("[Cat Cafe Waves] Cafe Prefabs: " + Summary());
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    // Which slots are filled, for the Console: a prefab that isn't showing up usually isn't in its slot
    string Summary()
    {
        return $"pop-up word {Which(floatingWord)}, rings {Which(ringTemplate)}, kitten trail {Which(kittenTrailTemplate)}, "
             + $"prop trail {Which(propTrailTemplate)}, splat {Which(splat)}, order bubble {Which(orderBubble)}, "
             + $"tip jar {Which(tipJar)}, rope {Which(ropeTemplate)}, recipe {Which(recipe)}, reticle {Which(reticle)}, "
             + $"beam {Which(beamTemplate)}.";
    }

    static string Which(Object slot)
    {
        if (slot != null)
        {
            return "from prefab";
        }
        return "built in code";
    }
}
