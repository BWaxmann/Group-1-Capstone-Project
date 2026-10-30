using System.Diagnostics;
using TMPro;
using UnityEngine;

// Cat Cafe Waves: pays the "first time" costs while the scene loads, so they don't land mid-round as a hitch on the
// Quest: the fonts' glyphs (a dynamic font builds each new letter into its atlas the first time it's shown), every
// shader (compiled the first time something using it is drawn) and the sound clips (decoded the first time they play).
public static class Warmup
{
    // Everything the game ever writes: digits, money, punctuation, both cases
    const string Characters = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

    public static void Run()
    {
        Stopwatch clock = Stopwatch.StartNew();
        int fonts = 0;
        int clips = 0;

        foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic) continue;
            font.TryAddCharacters(Characters);
            fonts++;
        }

        foreach (AudioClip clip in Resources.FindObjectsOfTypeAll<AudioClip>())
        {
            if (clip.loadState == AudioDataLoadState.Unloaded && clip.LoadAudioData()) clips++;
        }

        Shader.WarmupAllShaders();

        UnityEngine.Debug.Log($"[Cat Cafe Waves] Warm-up: {fonts} font(s), {clips} sound(s), shaders, in {clock.ElapsedMilliseconds} ms.");
    }
}
