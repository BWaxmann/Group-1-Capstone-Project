using System;
using UnityEngine;

// Keeps time with the level music so things can happen on the beat.
// Created automatically when the level loads, so nothing needs adding to the scene.
// Reads the music's own playback position, so it stays in sync through the intro → loop
// switch, every loop, and the finale speed-up.
public class BeatClock : MonoBehaviour
{
    public enum ShotQuality
    {
        Good,
        Great,
        Perfect
    }

    [SerializeField]
    [Min(1)]
    float bpm = 119f;

    [SerializeField]
    [Tooltip("Where the first beat falls in each music clip, in seconds. Matched by clip name.")]
    ClipOffset[] clipOffsets = new ClipOffset[]
    {
        new ClipOffset("LevelMusic", 0.23f),
        new ClipOffset("LevelMusicLoop", 0.19f)
    };

    [SerializeField]
    [Tooltip("Used for any clip not listed above.")]
    float defaultFirstBeat = 0f;

    [SerializeField]
    [Range(-0.3f, 0.3f)]
    [Tooltip("If on-beat shots feel late, increase this. If they feel early, decrease it.")]
    float calibrationOffset = 0f;

    [SerializeField]
    [Min(0)]
    [Tooltip("Seconds either side of a beat that count as Perfect.")]
    float perfectWindow = 0.07f;

    [SerializeField]
    [Min(0)]
    [Tooltip("Seconds either side of a beat that count as Great.")]
    float greatWindow = 0.14f;

    [SerializeField]
    [Min(0)]
    [Tooltip("How much the cafe's real-time lights brighten on each beat. 0 = off.")]
    float lightPulseAmount = 0.35f;

    bool lightsAttached;

    public static BeatClock instance;

    // Fired on every beat with the total number of beats so far
    public static event Action<int> OnBeat;

    public bool isRunning { get; private set; }
    public int totalBeats { get; private set; }

    // 0 right on the beat, rising to 1 just before the next one
    public float beatPhase { get; private set; }

    // Real seconds between beats (shorter when the music is sped up)
    public float secondsPerBeat => 60f / bpm / Mathf.Max(0.01f, music != null ? music.pitch : 1f);

    AudioSource music;
    AudioClip currentClip;
    float currentFirstBeat;
    double songTime;
    int lastBeatInClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateIfNeeded()
    {
        if (instance == null && FindObjectOfType<GameManager>() != null && FindObjectOfType<BeatClock>() == null)
        {
            new GameObject("Beat Clock").AddComponent<BeatClock>();
        }
    }

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

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (music == null && GameManager.instance != null)
        {
            music = GameManager.instance.musicAudioSource;
        }

        if (music == null || music.clip == null || !music.isPlaying)
        {
            isRunning = false;
            return;
        }

        double actualTime = (double)music.timeSamples / music.clip.frequency;

        if (music.clip != currentClip)
        {
            currentClip = music.clip;
            currentFirstBeat = GetFirstBeat(currentClip);
            songTime = actualTime;
            lastBeatInClip = Mathf.FloorToInt(GetBeatPosition());
        }

        // The audio position only updates in small chunks, so move smoothly between updates
        songTime += Time.unscaledDeltaTime * music.pitch;
        if (Math.Abs(actualTime - songTime) > 0.1)
        {
            // The music looped or jumped: start counting from the new position
            bool looped = actualTime < songTime;
            songTime = actualTime;
            if (looped)
            {
                lastBeatInClip = Mathf.FloorToInt(GetBeatPosition());
            }
        }
        else
        {
            songTime += (actualTime - songTime) * 0.1;
        }

        isRunning = true;
        if (!lightsAttached)
        {
            AttachToLights();
        }

        float position = GetBeatPosition();
        int beat = Mathf.FloorToInt(position);
        beatPhase = position - beat;

        // Never fire a burst of beats after a hitch
        if (beat - lastBeatInClip > 2)
        {
            lastBeatInClip = beat - 1;
        }

        while (lastBeatInClip < beat)
        {
            lastBeatInClip++;
            totalBeats++;
            OnBeat?.Invoke(totalBeats);
        }
    }

    void AttachToLights()
    {
        lightsAttached = true;
        if (lightPulseAmount <= 0) return;

        foreach (Light sceneLight in FindObjectsOfType<Light>())
        {
            // Baked lights can't change at runtime, so only real-time ones can pulse
            if (sceneLight.bakingOutput.lightmapBakeType != LightmapBakeType.Baked)
            {
                BeatPulse.AddTo(sceneLight.gameObject, 0f, false, 1, lightPulseAmount);
            }
        }
    }

    float GetBeatPosition()
    {
        return (float)((songTime - currentFirstBeat + calibrationOffset) * bpm / 60.0);
    }

    float GetFirstBeat(AudioClip clip)
    {
        if (clipOffsets != null)
        {
            foreach (ClipOffset offset in clipOffsets)
            {
                if (offset.clipName == clip.name)
                {
                    return offset.firstBeat;
                }
            }
        }
        return defaultFirstBeat;
    }

    // How far the current moment is from the nearest beat, in seconds
    public float SecondsFromNearestBeat()
    {
        if (!isRunning) return float.MaxValue;
        return Mathf.Min(beatPhase, 1f - beatPhase) * secondsPerBeat;
    }

    public ShotQuality JudgeTiming()
    {
        float error = SecondsFromNearestBeat();
        if (error <= perfectWindow) return ShotQuality.Perfect;
        if (error <= greatWindow) return ShotQuality.Great;
        return ShotQuality.Good;
    }

    [Serializable]
    public struct ClipOffset
    {
        public string clipName;
        public float firstBeat;

        public ClipOffset(string clipName, float firstBeat)
        {
            this.clipName = clipName;
            this.firstBeat = firstBeat;
        }
    }
}
