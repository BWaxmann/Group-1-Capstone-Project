using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;

// Cat Cafe Waves: pays the "first time" costs in the lobby, a little each frame once the scene is up, so they don't
// land mid-round as a hitch on the Quest: the fonts' glyphs (a dynamic font builds each new letter into its atlas the
// first time it's shown), the sound clips (decoded the first time they play) and, with a Shader Variants collection
// set on the Rush Director, the shaders (compiled the first time something using them is drawn).
// Nothing here may hold the scene back: Shader.WarmupAllShaders in Start froze the Quest on the splash screen, since
// on Android it compiles every variant of every shader in the build, which takes minutes.
public static class Warmup
{
    // Everything the game ever writes: digits, money, punctuation, both cases
    const string Characters = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

    // How long one frame may spend on this (of the 13.9 ms a 72 Hz frame has)
    const float FrameBudgetMs = 4f;

    public static IEnumerator Run(ShaderVariantCollection shaderVariants)
    {
        // Two frames drawn first, so the splash screen is gone and the cafe is in view before any of this
        yield return null;
        yield return null;

        Stopwatch total = Stopwatch.StartNew();
        Stopwatch frame = Stopwatch.StartNew();

        int fonts = 0;
        foreach (TMP_FontAsset font in FontsInUse())
        {
            if (WarmFont(font))
            {
                fonts++;
            }
            if (frame.ElapsedMilliseconds >= FrameBudgetMs)
            {
                yield return null;
                frame = Stopwatch.StartNew();
            }
        }

        int clips = 0;
        foreach (AudioClip clip in Resources.FindObjectsOfTypeAll<AudioClip>())
        {
            if (LoadClip(clip))
            {
                clips++;
            }
            if (frame.ElapsedMilliseconds >= FrameBudgetMs)
            {
                yield return null;
                frame = Stopwatch.StartNew();
            }
        }

        string shaders = "no Shader Variants collection set";
        if (shaderVariants != null)
        {
            yield return null;   // (a frame of its own: a collection compiles in one go, so it may take a moment)
            Stopwatch clock = Stopwatch.StartNew();
            if (WarmShaders(shaderVariants))
            {
                shaders = $"{shaderVariants.variantCount} shader variant(s) in {clock.ElapsedMilliseconds} ms";
            }
            else
            {
                shaders = "the shader variants couldn't be compiled";
            }
        }

        UnityEngine.Debug.Log($"[Cat Cafe Waves] Warm-up: {fonts} font(s), {clips} sound(s), {shaders}, spread over {total.ElapsedMilliseconds} ms of lobby.");
    }

    // Every font the scene's texts use, plus TextMesh Pro's default
    static List<TMP_FontAsset> FontsInUse()
    {
        List<TMP_FontAsset> fonts = new List<TMP_FontAsset>();
        try
        {
            foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (text == null || text.font == null || !text.gameObject.scene.IsValid())
                {
                    continue;
                }
                if (!fonts.Contains(text.font))
                {
                    fonts.Add(text.font);
                }
            }
            if (TMP_Settings.defaultFontAsset != null && !fonts.Contains(TMP_Settings.defaultFontAsset))
            {
                fonts.Add(TMP_Settings.defaultFontAsset);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Cat Cafe Waves] Couldn't list the fonts to warm up: {e.Message}");
        }
        return fonts;
    }

    // Every letter into the font, the same way the game adds them: by drawing a text with them once (off to the side,
    // then gone). Adding them to the font asset directly can fail on a font that isn't ready.
    static bool WarmFont(TMP_FontAsset font)
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
            return true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Cat Cafe Waves] Couldn't warm up the font {font.name}: {e.Message}");
            return false;
        }
        finally
        {
            if (holder != null)
            {
                Object.Destroy(holder);
            }
        }
    }

    // A clip that can't be loaded now is loaded when it's first played, as before
    static bool LoadClip(AudioClip clip)
    {
        try
        {
            return clip != null && clip.loadState == AudioDataLoadState.Unloaded && clip.LoadAudioData();
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    static bool WarmShaders(ShaderVariantCollection variants)
    {
        try
        {
            variants.WarmUp();
            return true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Cat Cafe Waves] Couldn't compile the shader variants {variants.name}: {e.Message}");
            return false;
        }
    }
}
