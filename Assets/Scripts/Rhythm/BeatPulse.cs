using UnityEngine;

// Makes an object bounce (and a light throb) on the beat of the music.
// Add it to anything in the Inspector, or call BeatPulse.AddTo() from code.
public class BeatPulse : MonoBehaviour
{
    [SerializeField]
    [Tooltip("How much bigger it gets on each beat. 0.08 = 8%.")]
    float scaleAmount = 0.08f;

    [SerializeField]
    [Tooltip("Squash and stretch: grows taller and thinner on the beat instead of evenly.")]
    bool squashAndStretch = false;

    [SerializeField]
    [Tooltip("How much brighter a light on this object gets on each beat. 0.5 = 50%.")]
    float lightAmount = 0.5f;

    [SerializeField]
    [Min(1)]
    [Tooltip("1 = every beat, 2 = every other beat, 4 = once per bar.")]
    int everyNBeats = 1;

    [SerializeField]
    [Min(0.1f)]
    [Tooltip("How quickly the bounce settles. Higher = snappier.")]
    float settleSpeed = 10f;

    Vector3 baseScale;
    Light pulseLight;
    float baseIntensity;
    float pulse;

    public static BeatPulse AddTo(GameObject target, float scaleAmount, bool squashAndStretch = false, int everyNBeats = 1, float lightAmount = 0.5f)
    {
        BeatPulse beatPulse = target.GetComponent<BeatPulse>();
        if (beatPulse == null)
        {
            beatPulse = target.AddComponent<BeatPulse>();
            beatPulse.scaleAmount = scaleAmount;
            beatPulse.squashAndStretch = squashAndStretch;
            beatPulse.everyNBeats = everyNBeats;
            beatPulse.lightAmount = lightAmount;
        }
        return beatPulse;
    }

    void Awake()
    {
        baseScale = transform.localScale;
        pulseLight = GetComponent<Light>();
        if (pulseLight != null)
        {
            baseIntensity = pulseLight.intensity;
        }
    }

    void OnEnable()
    {
        BeatClock.OnBeat += HandleBeat;
    }

    void OnDisable()
    {
        BeatClock.OnBeat -= HandleBeat;
        pulse = 0;
        Apply();
    }

    void HandleBeat(int beat)
    {
        if (beat % everyNBeats == 0)
        {
            pulse = 1f;
        }
    }

    void Update()
    {
        if (pulse <= 0) return;

        pulse = Mathf.Max(0, pulse - pulse * settleSpeed * Time.deltaTime - 0.01f);
        Apply();
    }

    void Apply()
    {
        float amount = pulse * scaleAmount;
        if (squashAndStretch)
        {
            transform.localScale = Vector3.Scale(baseScale, new Vector3(1f - amount * 0.5f, 1f + amount, 1f - amount * 0.5f));
        }
        else
        {
            transform.localScale = baseScale * (1f + amount);
        }

        if (pulseLight != null)
        {
            pulseLight.intensity = baseIntensity * (1f + pulse * lightAmount);
        }
    }
}
