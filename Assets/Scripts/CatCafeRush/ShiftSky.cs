using System;
using System.Collections.Generic;
using Borodar.FarlandSkies.LowPoly;
using UnityEngine;

// Cat Cafe Waves: the sky outside goes from morning to night over the shift (Farland Skies - Low Poly, its prefabs kept
// in Resources/ShiftSky). Its time of day follows the shift instead of its own timer. Its sun and moon are real-time
// lights, which would re-light the whole cafe (and cost a lot on the Quest), so they're switched off: only the sky
// changes. It gets its own copy of the sky material, so playing never changes the asset.
public class ShiftSky : MonoBehaviour
{
    [Serializable]
    public class Settings
    {
        public bool enabled = true;

        [Range(0f, 100f)]
        [Tooltip("The sky's time of day (Farland's 0-100: dawn about 25, day 40-65, sunset 75-80, night from 90) at the start of the shift.")]
        public float startTimeOfDay = 38f;

        [Range(0f, 100f)]
        [Tooltip("The sky's time of day at the end of the shift.")]
        public float endTimeOfDay = 88f;

        [Tooltip("Keep the sky's sun and moon lights (real-time, with shadows: they light the whole cafe and are costly on the Quest).")]
        public bool sunAndMoonLights = false;

        [Range(0f, 100f)]
        [Tooltip("The sky's time of day when the painted night sky around the cafe (Sky Layer below) starts fading in over the real sky.")]
        public float duskFrom = 78f;

        [Range(0f, 100f)]
        [Tooltip("...and when it's all painted night sky.")]
        public float nightFrom = 88f;

        [Tooltip("The moon in the roof hole (hidden in daylight; it comes out at dusk, when the player isn't looking up).")]
        public string moonName = "1226 Moon";

        [Tooltip("The material of the painted sky around the cafe: faded out by day, so the real sky shows behind the buildings.")]
        public string skyLayer = "Parallax_Sky";
    }

    const string ControllerPath = "ShiftSky/Shift Sky Controller";
    const string CyclePath = "ShiftSky/Shift Sky Cycle";

    Settings settings;
    Func<float> progress;
    SkyboxDayNightCycle cycle;
    Renderer[] moon;
    bool moonOut;
    readonly List<Material> skyLayer = new List<Material>();
    readonly List<Color> skyLayerColour = new List<Color>();

    // Puts the sky up, following `progress` (0 = start of the shift, 1 = the end). Null if it's off or missing.
    public static ShiftSky Begin(Settings settings, Func<float> progress)
    {
        if (settings == null || !settings.enabled || progress == null)
        {
            return null;
        }
        GameObject controllerPrefab = CafeResources.Load<GameObject>(ControllerPath);
        GameObject cyclePrefab = CafeResources.Load<GameObject>(CyclePath);
        if (controllerPrefab == null || cyclePrefab == null)
        {
            Debug.LogWarning("[Cat Cafe Waves] The shift sky's prefabs (Resources/ShiftSky) weren't found: the sky stays as it is.");
            return null;
        }

        // Made switched off, so everything can be set up before any of it wakes up
        GameObject holder = new GameObject("Shift Sky");
        holder.SetActive(false);
        GameObject controllerObject = Instantiate(controllerPrefab, holder.transform);
        SkyboxController controller = controllerObject.GetComponent<SkyboxController>();
        if (controller != null && controller.SkyboxMaterial != null)
        {
            controller.SkyboxMaterial = new Material(controller.SkyboxMaterial);
        }
        if (!settings.sunAndMoonLights)
        {
            foreach (Light light in controllerObject.GetComponentsInChildren<Light>(true))
            {
                light.enabled = false;
            }
        }
        GameObject cycleObject = Instantiate(cyclePrefab, holder.transform);

        ShiftSky sky = holder.AddComponent<ShiftSky>();
        sky.settings = settings;
        sky.progress = progress;
        sky.cycle = cycleObject.GetComponent<SkyboxDayNightCycle>();
        if (!string.IsNullOrEmpty(settings.skyLayer))
        {
            foreach (Renderer layer in FindObjectsOfType<Renderer>())
            {
                if (layer.sharedMaterial == null || layer.sharedMaterial.name != settings.skyLayer || !layer.sharedMaterial.HasProperty("_Color"))
                {
                    continue;
                }
                Material own = layer.material;   // its own copy (the asset is never changed)
                sky.skyLayer.Add(own);
                sky.skyLayerColour.Add(own.color);
            }
        }
        GameObject moon;
        if (string.IsNullOrEmpty(settings.moonName))
        {
            moon = null;
        }
        else
        {
            moon = GameObject.Find(settings.moonName);
        }
        if (moon != null)
        {
            sky.moon = moon.GetComponentsInChildren<Renderer>();
            sky.ShowMoon(false);
        }
        if (sky.cycle != null)
        {
            sky.cycle.TimeOfDay = settings.startTimeOfDay;
        }
        holder.SetActive(true);
        return sky;
    }

    void Update()
    {
        if (cycle == null || progress == null)
        {
            return;
        }
        float time = Mathf.Lerp(settings.startTimeOfDay, settings.endTimeOfDay, Mathf.Clamp01(progress()));
        cycle.TimeOfDay = time;

        // The painted night sky around the cafe: faded out by day (the real sky shows behind the buildings, matching the
        // roof), fading in at dusk
        float day = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.duskFrom, settings.nightFrom, time));
        SetSkyLayer(1f - day);

        // The moon comes out once it's getting dark, when nobody's looking (so it never pops in)
        if (moon != null && !moonOut && day < 0.5f && !LookingUp())
        {
            ShowMoon(true);
        }
    }

    static bool LookingUp()
    {
        Camera eye = Camera.main;
        return eye != null && Vector3.Angle(eye.transform.forward, Vector3.up) < 60f;
    }

    void ShowMoon(bool show)
    {
        moonOut = show;
        foreach (Renderer part in moon)
        {
            if (part != null)
            {
                part.enabled = show;
            }
        }
    }

    void SetSkyLayer(float shown)
    {
        for (int i = 0; i < skyLayer.Count; i++)
        {
            if (skyLayer[i] == null)
            {
                continue;
            }
            Color colour = skyLayerColour[i];
            colour.a *= shown;
            skyLayer[i].color = colour;
        }
    }

    // Back to the painted sky and the moon (e.g. leaving play mode in the editor)
    void OnDestroy()
    {
        SetSkyLayer(1f);
        if (moon != null)
        {
            ShowMoon(true);
        }
    }
}
