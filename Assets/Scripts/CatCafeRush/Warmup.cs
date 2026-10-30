using System.Collections.Generic;
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
        int fonts = WarmFonts();
        int clips = 0;

        foreach (AudioClip clip in Resources.FindObjectsOfTypeAll<AudioClip>())
        {
            try
            {
                if (clip.loadState == AudioDataLoadState.Unloaded && clip.LoadAudioData()) clips++;
            }
            catch (System.Exception)
            {
                // (a clip that can't be loaded now is loaded when it's played, as before)
            }
        }

        Shader.WarmupAllShaders();

        UnityEngine.Debug.Log($"[Cat Cafe Waves] Warm-up: {fonts} font(s), {clips} sound(s), shaders, in {clock.ElapsedMilliseconds} ms.");
    }

    // Every letter into every font the scene's texts use, the same way the game adds them: by drawing a text with them
    // once (off to the side, then gone). Adding them to the font asset directly can fail on a font that isn't ready.
    static int WarmFonts()
    {
        HashSet<TMP_FontAsset> fonts = new HashSet<TMP_FontAsset>();
        foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (text != null && text.font != null && text.gameObject.scene.IsValid()) fonts.Add(text.font);
        }
        if (TMP_Settings.defaultFontAsset != null) fonts.Add(TMP_Settings.defaultFontAsset);

        int warmed = 0;
        foreach (TMP_FontAsset font in fonts)
        {
            GameObject holder = null;
            try
            {
                holder = new GameObject("Font Warm-up");
                holder.transform.position = new Vector3(0, -100f, 0);
                TextMeshPro text = holder.AddComponent<TextMeshPro>();
                text.font = font;
                text.text = Characters;
                text.ForceMeshUpdate(true);
                warmed++;
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Cat Cafe Waves] Couldn't warm up the font {font.name}: {e.Message}");
            }
            if (holder != null) Object.Destroy(holder);
        }
        return warmed;
    }
}
