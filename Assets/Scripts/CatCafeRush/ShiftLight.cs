using System;
using System.Collections.Generic;
using UnityEngine;

// Cat Cafe Waves: the light goes from morning to evening over the shift. Cheap enough for the Quest: it only tints what
// the scene is lit with as it was set up (the ambient light, the sky and any lights that aren't baked), nothing is
// re-lit or re-baked. Baked light can't change at runtime, so after a bake the change shows in the sky, the ambient
// light and the cats and props (which are lit live).
public class ShiftLight : MonoBehaviour
{
    [Serializable]
    public class Settings
    {
        public bool enabled = true;
        [Tooltip("The scene's light through the shift, left = start of the shift, right = the end (white = as it was set up).")]
        public Gradient tint = DefaultTint();
        [Range(0f, 2f)]
        [Tooltip("How bright the sky is at the end of the shift, compared with how it was set up.")]
        public float eveningSkyExposure = 0.7f;

        static Gradient DefaultTint()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(new[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.97f), 0f),       // morning: as it is, a touch fresh
                new GradientColorKey(new Color(1f, 0.93f, 0.8f), 0.55f),  // afternoon gold
                new GradientColorKey(new Color(1f, 0.72f, 0.52f), 0.82f), // sunset, as the Dinner Rush starts
                new GradientColorKey(new Color(0.62f, 0.55f, 0.82f), 1f), // dusk
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }
    }

    Settings settings;
    Func<float> progress;

    Color ambientSky, ambientEquator, ambientGround, ambientFlat, fog;
    Material sky;
    bool skyHasTint, skyHasExposure;
    Color skyTint;
    float skyExposure;
    readonly List<Light> lights = new List<Light>();
    readonly List<Color> lightColours = new List<Color>();

    static readonly int Tint = Shader.PropertyToID("_Tint");
    static readonly int Exposure = Shader.PropertyToID("_Exposure");

    // Starts the light following `progress` (0 = start of the shift, 1 = the end)
    public static ShiftLight Begin(GameObject on, Settings settings, Func<float> progress)
    {
        if (settings == null || !settings.enabled || progress == null) return null;
        ShiftLight light = on.GetComponent<ShiftLight>();
        if (light == null) light = on.AddComponent<ShiftLight>();
        light.settings = settings;
        light.progress = progress;
        light.Capture();
        return light;
    }

    // How the scene was set up (everything is tinted from this)
    void Capture()
    {
        ambientSky = RenderSettings.ambientSkyColor;
        ambientEquator = RenderSettings.ambientEquatorColor;
        ambientGround = RenderSettings.ambientGroundColor;
        ambientFlat = RenderSettings.ambientLight;
        fog = RenderSettings.fogColor;
        // (the sky is left alone when the shift sky is up: it has its own morning to night)
        if (RenderSettings.skybox != null && sky == null && FindObjectOfType<ShiftSky>() == null)
        {
            // A copy, so the material asset itself is never changed
            sky = new Material(RenderSettings.skybox);
            RenderSettings.skybox = sky;
            skyHasTint = sky.HasProperty(Tint);
            skyHasExposure = sky.HasProperty(Exposure);
            if (skyHasTint) skyTint = sky.GetColor(Tint);
            if (skyHasExposure) skyExposure = sky.GetFloat(Exposure);
        }
        lights.Clear();
        lightColours.Clear();
        foreach (Light light in FindObjectsOfType<Light>())
        {
            if (light.bakingOutput.lightmapBakeType == LightmapBakeType.Baked) continue;   // can't change once baked
            lights.Add(light);
            lightColours.Add(light.color);
        }
    }

    void Update()
    {
        if (settings == null || progress == null) return;
        float through = Mathf.Clamp01(progress());
        Color tint = settings.tint.Evaluate(through);

        RenderSettings.ambientSkyColor = ambientSky * tint;
        RenderSettings.ambientEquatorColor = ambientEquator * tint;
        RenderSettings.ambientGroundColor = ambientGround * tint;
        RenderSettings.ambientLight = ambientFlat * tint;
        RenderSettings.fogColor = fog * tint;
        if (sky != null)
        {
            if (skyHasTint)
            {
                Color tinted = skyTint * tint;
                tinted.a = skyTint.a;
                sky.SetColor(Tint, tinted);
            }
            if (skyHasExposure) sky.SetFloat(Exposure, skyExposure * Mathf.Lerp(1f, settings.eveningSkyExposure, through));
        }
        for (int i = 0; i < lights.Count; i++)
        {
            if (lights[i] != null) lights[i].color = lightColours[i] * tint;
        }
    }
}
