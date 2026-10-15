using UnityEngine;

// One place to tune the Cat Chaos prototype: how props fly, how many can be in the air, and their sounds.
// Lives on the "Cat Chaos" object that Catastrophe Cafe > Cat Chaos > Create Prototype Scene adds.
public class CatChaosSettings : MonoBehaviour
{
    public static CatChaosSettings instance { get; private set; }

    [Header("Flying props")]

    [Min(1)]
    [Tooltip("Most props in the air (or held) at once. Kept low so there's time to feed cats too.")]
    public int maxAirborne = 4;

    [Min(0)]
    [Tooltip("Upward speed of the burst when knocked, in m/s (capped so props never reach the ceiling).")]
    public float launchUpSpeed = 5.5f;

    [Min(0)]
    [Tooltip("How fast the burst throws a prop out towards the player, in m/s.")]
    public float launchTowardPlayerSpeed = 2.2f;

    [Range(0, 60)]
    [Tooltip("Random sideways spread of the burst, in degrees either side.")]
    public float launchSpread = 30f;

    [Min(0.1f)]
    [Tooltip("Gravity on knocked props while they burst upwards (high = a short, snappy burst).")]
    public float propGravity = 9f;

    [Min(0.5f)]
    [Tooltip("Props never fly higher than this above the floor, in metres (keeps them under the ceiling).")]
    public float maxHeightAboveFloor = 2.6f;

    [Min(0)]
    [Tooltip("Seconds after being knocked before a prop can be caught. 0 = straight away.")]
    public float catchableAfter = 0f;

    [Min(0.05f)]
    [Tooltip("Once a prop stops rising it drifts and falls slowly: its fall speed, in m/s.")]
    public float driftFallSpeed = 0.6f;

    [Min(0)]
    [Tooltip("How fast a drifting prop swoops about sideways, in m/s.")]
    public float driftSpeed = 0.8f;

    [Min(0.1f)]
    [Tooltip("Seconds between a drifting prop's changes of direction (picked at random up to this).")]
    public float driftTurnTime = 0.9f;

    [Tooltip("World height of the floor, where uncaught props land. Set by the scene generator.")]
    public float floorHeight = 0f;

    [Min(1f)]
    [Tooltip("How much bigger props get while they're in the air, so they're easy to spot (1 = no change).")]
    public float airborneScale = 1f;

    [Tooltip("A bright trail behind props while they're in the air.")]
    public bool airborneTrail = false;

    public Color trailColour = new Color(1f, 0.9f, 0.4f, 0.9f);

    [Tooltip("Props pulse with a bright outline while they can be caught (seen through furniture too).")]
    public bool catchableGlow = false;

    public Color glowColour = new Color(1f, 0.8f, 0.15f);

    [Min(0)]
    [Tooltip("Seconds before a landed prop pops back onto its pile.")]
    public float respawnDelay = 2.5f;

    [Min(0.1f)]
    [Tooltip("Seconds a released prop takes to fly home.")]
    public float returnDuration = 0.8f;

    [Header("Cats")]

    [Range(0f, 1f)]
    [Tooltip("Chance, at each stop while roaming, that a cat marches over to a pile to swipe at it. " +
             "0 = cats just wander (and knock things as they pass). Brian's cats use 0.2.")]
    public float pileTargetingChance = 0f;

    [Min(0.1f)]
    [Tooltip("How close (in metres, sideways) a wandering cat has to come to a pile or loose prop for a chance to knock it.")]
    public float knockReach = 0.7f;

    [Min(0.1f)]
    [Tooltip("How far above or below a cat a prop can be and still get knocked (a cat bumping a table knocks what's on it).")]
    public float knockHeightReach = 1.2f;

    [Range(0f, 1f)]
    [Tooltip("Chance that a cat coming near a pile or prop sends something flying.")]
    public float knockChance = 0.45f;

    [Min(0)]
    [Tooltip("Fewest seconds between any two knocks, so it never all goes up at once.")]
    public float knockGap = 1.5f;

    [Min(0)]
    [Tooltip("Seconds a prop has to be back home before it can be knocked again.")]
    public float propRestTime = 4f;

    [Header("Sounds")]

    public SoundData knockSound = new SoundData(0.6f, 1f);
    public SoundData landedSound = new SoundData(0.7f, 1f);
    public SoundData respawnSound = new SoundData(0.4f, 1f);
    public SoundData homeSound = new SoundData(0.5f, 1f);
    public SoundData catchSound = new SoundData(0.7f, 1f);

    void Awake()
    {
        instance = this;
        CatMovement.targetingChanceOverride = pileTargetingChance;
    }

    void OnValidate()
    {
        if (instance == this) CatMovement.targetingChanceOverride = pileTargetingChance;
    }

    void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        // These are shared by every scene, so put them back for CC_LEVEL and the rest
        CatMovement.targetingChanceOverride = -1f;
        SpawnManager.holdSpawns = false;
    }

    public void PlayKnock(Vector3 position) => Play(knockSound, position, Random.Range(0.9f, 1.15f));
    public void PlayLanded(Vector3 position) => Play(landedSound, position, Random.Range(0.85f, 1.1f));
    public void PlayRespawn(Vector3 position) => Play(respawnSound, position, Random.Range(0.95f, 1.1f));
    public void PlayHome(Vector3 position) => Play(homeSound, position, Random.Range(1f, 1.2f));
    public void PlayCatch(Vector3 position) => Play(catchSound, position, Random.Range(0.95f, 1.1f));

    static void Play(SoundData sound, Vector3 position, float pitch)
    {
        if (sound.soundClip == null) return;
        // A short-lived positional source, so the sound comes from where the prop is
        GameObject go = new GameObject("Cat Chaos Sound");
        go.transform.position = position;
        AudioSource source = go.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.minDistance = 1.5f;
        source.pitch = sound.pitch * pitch;
        source.PlayOneShot(sound.soundClip, sound.volume);
        Destroy(go, sound.soundClip.length / Mathf.Max(0.1f, Mathf.Abs(source.pitch)) + 0.1f);
    }
}
