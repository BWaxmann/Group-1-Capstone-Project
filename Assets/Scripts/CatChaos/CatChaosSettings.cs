using UnityEngine;

// One place to tune the Cat Chaos prototype: how props fly, how many can be in the air, and their sounds.
// Lives on the "Cat Chaos" object that Catastrophe Cafe > Cat Chaos > Create Prototype Scene adds.
public class CatChaosSettings : MonoBehaviour
{
    public static CatChaosSettings instance { get; private set; }

    [Header("Flying props")]

    [Min(1)]
    [Tooltip("Most props in the air (or held) at once. Extra paw swipes just rattle the pile.")]
    public int maxAirborne = 6;

    [Min(0)]
    [Tooltip("Upward speed when knocked, in m/s. Higher = more hang time (and closer to the ceiling).")]
    public float launchUpSpeed = 3.8f;

    [Min(0)]
    [Tooltip("How fast a knocked prop drifts towards the player, in m/s.")]
    public float launchTowardPlayerSpeed = 0.9f;

    [Range(0, 60)]
    [Tooltip("Random sideways spread of the drift, in degrees either side.")]
    public float launchSpread = 30f;

    [Min(0.1f)]
    [Tooltip("Gravity on knocked props. Lower than real (9.8) so there's time to catch them.")]
    public float propGravity = 4.5f;

    [Tooltip("World height of the floor, where uncaught props land. Set by the scene generator.")]
    public float floorHeight = 0f;

    [Min(0)]
    [Tooltip("Seconds before a landed prop pops back onto its pile.")]
    public float respawnDelay = 2.5f;

    [Min(0.1f)]
    [Tooltip("Seconds a released prop takes to fly home.")]
    public float returnDuration = 0.8f;

    [Header("Sounds")]

    public SoundData knockSound = new SoundData(0.6f, 1f);
    public SoundData landedSound = new SoundData(0.7f, 1f);
    public SoundData respawnSound = new SoundData(0.4f, 1f);
    public SoundData homeSound = new SoundData(0.5f, 1f);
    public SoundData catchSound = new SoundData(0.7f, 1f);

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
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
