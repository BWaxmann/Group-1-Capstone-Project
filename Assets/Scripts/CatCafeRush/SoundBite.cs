using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Cat Cafe Waves: a big sound bite from the speakers (the Mafia cat's theme, the spy music). It eases in, the music dips
// smoothly underneath while it plays, and it eases out (at the end of the clip, or when it's stopped) as the music comes
// back up. A bite can have an intro that plays once before the main clip loops.
public class SoundBite : MonoBehaviour
{
    static readonly List<SoundBite> playing = new List<SoundBite>();
    internal static void ResetStatics()
    {
        playing.Clear();
    }   // (a new run: see RunReset)

    AudioSource main;
    AudioSource intro;
    float volume;
    float musicLevel;
    bool stopping;

    // Plays `clip` at `volume` with the music at `musicLevel` of its volume underneath. `introClip` (optional) plays first,
    // straight into `clip`; `loop` keeps `clip` going until Stop. Null if there's nothing to play.
    public static SoundBite Play(string name, AudioClip clip, float volume, float musicLevel, AudioClip introClip,
                                 bool loop = false, float fadeIn = 0.15f, float fadeOut = 1f)
    {
        if (clip == null || volume <= 0)
        {
            return null;
        }
        SoundBite bite = new GameObject(name).AddComponent<SoundBite>();
        bite.volume = volume;
        bite.musicLevel = musicLevel;
        bite.main = bite.Source(clip, loop);

        // Scheduled on the audio clock, so the intro runs into the main clip without a gap
        double start = AudioSettings.dspTime + 0.05;
        if (introClip != null)
        {
            bite.intro = bite.Source(introClip, false);
            bite.intro.PlayScheduled(start);
            start += (double)introClip.samples / introClip.frequency;
        }
        bite.main.PlayScheduled(start);

        playing.Add(bite);
        Duck();
        float length;
        if (loop)
        {
            length = -1f;
        }
        else
        {
            length = (float)(start - AudioSettings.dspTime) + clip.length;
        }
        bite.StartCoroutine(bite.Run(fadeIn, fadeOut, length));
        return bite;
    }

    // Every bite eases out (the round's over)
    public static void StopAll(float fadeOut = 1f)
    {
        foreach (SoundBite bite in playing.ToArray())
        {
            bite.Stop(fadeOut);
        }
    }

    AudioSource Source(AudioClip clip, bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.volume = 0f;
        return source;
    }

    IEnumerator Run(float fadeIn, float fadeOut, float length)
    {
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, fadeIn);
            SetVolume(volume * Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        if (length < 0)
        {
            yield break;   // loops until stopped
        }
        yield return new WaitForSeconds(Mathf.Max(0, length - fadeIn - fadeOut));
        Stop(fadeOut);
    }

    // Eases out over `seconds` (the music comes back up as it goes), then it's gone
    public void Stop(float seconds = 1f)
    {
        if (stopping)
        {
            return;
        }
        stopping = true;
        playing.Remove(this);
        Duck();
        StopAllCoroutines();
        StartCoroutine(FadeAway(seconds));
    }

    IEnumerator FadeAway(float seconds)
    {
        float from;
        if (main != null)
        {
            from = main.volume;
        }
        else
        {
            from = 0f;
        }
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, seconds);
            SetVolume(from * Mathf.Cos(Mathf.Clamp01(t) * Mathf.PI * 0.5f));   // an even-sounding fade, not a sudden drop
            yield return null;
        }
        Destroy(gameObject);
    }

    void SetVolume(float level)
    {
        if (main != null)
        {
            main.volume = level;
        }
        if (intro != null)
        {
            intro.volume = level;
        }
    }

    void OnDestroy()
    {
        if (stopping)
        {
            return;
        }
        playing.Remove(this);
        Duck();
    }

    // The music sits under the quietest-wanted bite still playing, and comes back up when there are none
    static void Duck()
    {
        GameManager game = GameManager.instance;
        if (game == null)
        {
            return;
        }
        float level = 1f;
        foreach (SoundBite bite in playing)
        {
            level = Mathf.Min(level, bite.musicLevel);
        }
        if (playing.Count > 0 && level < 1f)
        {
            game.DuckMusic(level, float.MaxValue);
        }
        else
        {
            game.EndDuck();
        }
    }
}
