using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Cat Cafe Waves' pop-up cats: whack-a-mole in the air. A pooled cat hides in a hole in the floor; the hole puffs and
// boings, then the cat springs straight up, hangs at the top for a moment glowing gold, and drops back in. Any treat
// that reaches it while it's out pops it into confetti. A miss costs nothing: it just drops back down.
public class PopUpCat : MonoBehaviour
{
    [Serializable]
    public class Settings
    {
        [Min(0.1f)]
        [Tooltip("Seconds between the hole's puff and boing and the cat springing out (the warning).")]
        public float warning = 0.45f;
        [Min(0.3f)] public float heightMin = 1.2f;
        [Min(0.3f)] public float heightMax = 1.7f;
        [Min(0.1f)] public float riseTime = 0.45f;
        [Min(0f)]
        [Tooltip("Seconds it hangs at the top: the easiest moment to hit it.")]
        public float hang = 0.4f;
        [Min(0.1f)] public float fallTime = 0.45f;
        [Min(0.1f)]
        [Tooltip("Radius of its invisible hit area, in metres.")]
        public float hitRadius = 0.4f;
        public Color glowColour = new Color(1f, 0.8f, 0.15f);
    }

    public static readonly List<PopUpCat> active = new List<PopUpCat>();

    const float HideDepth = 0.6f;   // how far below the floor it waits

    CatMovement cat;
    Animator animator;
    SphereCollider hitbox;
    Outline glow;
    Settings settings;
    Vector3 hole;
    Transform viewer;
    Action<PopUpCat> onHit;
    bool airborne;
    bool hit;
    float normalAnimatorSpeed = 1f;

    static AudioClip boing;
    AudioSource voice;

    public bool isOut => airborne && !hit;
    public Vector3 aimPoint => hitbox != null ? hitbox.transform.TransformPoint(hitbox.center) : transform.position + Vector3.up * 0.2f;
    public Vector3 holePosition => hole;

    // Hides a pooled cat (switched off) in `hole` and pops it up once
    public static PopUpCat Launch(CatMovement cat, Vector3 hole, Transform viewer, Settings settings, Action<PopUpCat> onHit)
    {
        NavMeshAgent agent = cat.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        PopUpCat pop = cat.GetComponent<PopUpCat>();
        if (pop == null) pop = cat.gameObject.AddComponent<PopUpCat>();
        pop.cat = cat;
        pop.hole = hole;
        pop.viewer = viewer;
        pop.settings = settings;
        pop.onHit = onHit;
        pop.airborne = false;
        pop.hit = false;

        cat.transform.position = hole + Vector3.down * HideDepth;
        Vector3 look = viewer != null ? viewer.position - hole : -cat.transform.forward;
        look.y = 0;
        if (look.sqrMagnitude > 0.0001f) cat.transform.rotation = Quaternion.LookRotation(look);
        cat.gameObject.SetActive(true);
        cat.TakeOver();
        cat.treatGate = pop.Eat;
        pop.enabled = true;

        pop.animator = cat.GetComponentInChildren<Animator>();
        if (pop.animator != null) pop.normalAnimatorSpeed = pop.animator.speed;
        pop.SetHitbox(settings.hitRadius);
        pop.StartCoroutine(pop.Pop());
        return pop;
    }

    void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
    }

    void OnDisable()
    {
        active.Remove(this);
    }

    IEnumerator Pop()
    {
        // The warning: a puff of dust out of the hole and a boing from it
        cat.DustPuff(0.6f, hole);
        PlayBoing();
        yield return new WaitForSeconds(settings.warning);

        // Up it springs (fast, slowing to the top)
        airborne = true;
        SetGlow(true);
        Vector3 low = hole + Vector3.down * HideDepth;
        Vector3 top = hole + Vector3.up * Random.Range(settings.heightMin, Mathf.Max(settings.heightMin, settings.heightMax));
        float t = 0;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / settings.riseTime);
            transform.position = Vector3.Lerp(low, top, 1f - (1f - t) * (1f - t));
            Pulse();
            yield return null;
        }

        // Hangs there, paws out, wiggling
        if (animator != null) animator.speed = 0f;
        Quaternion facing = transform.rotation;
        float timer = 0;
        while (timer < settings.hang)
        {
            timer += Time.deltaTime;
            transform.position = top + Vector3.up * Mathf.Sin(timer / settings.hang * Mathf.PI) * 0.05f;
            transform.rotation = facing * Quaternion.Euler(0, 0, Mathf.Sin(timer * 30f) * 8f);
            Pulse();
            yield return null;
        }
        transform.rotation = facing;
        if (animator != null) animator.speed = normalAnimatorSpeed;

        // And back down into the hole (faster and faster)
        t = 0;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / settings.fallTime);
            transform.position = Vector3.Lerp(top, low, t * t);
            Pulse();
            if (transform.position.y < hole.y) SetGlow(false);
            yield return null;
        }
        airborne = false;
        cat.DustPuff(0.5f, hole);
        Finish();
    }

    // Any treat pops it while it's out of the hole
    bool Eat(Treat treat)
    {
        if (!airborne || hit || transform.position.y < hole.y) return false;
        hit = true;
        onHit?.Invoke(this);
        StopAllCoroutines();
        StartCoroutine(HitPop());
        return false;
    }

    IEnumerator HitPop()
    {
        Vector3 size = transform.localScale;
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.12f;
            transform.localScale = size * (1f + 0.3f * Mathf.Min(t, 1f));
            yield return null;
        }
        transform.localScale = size;
        cat.PopConfetti();
        Finish();
    }

    void Finish()
    {
        StopAllCoroutines();
        airborne = false;
        SetGlow(false);
        if (hitbox != null) hitbox.enabled = false;
        if (animator != null) animator.speed = normalAnimatorSpeed;
        cat.treatGate = null;
        enabled = false;
        cat.gameObject.SetActive(false);
    }

    // When the wave is over: any cat still out drops back into its hole
    public static void EndAll()
    {
        foreach (PopUpCat pop in active.ToArray())
        {
            if (pop.hit) continue;
            pop.cat.DustPuff(0.5f, pop.hole);
            pop.Finish();
        }
    }

    void SetHitbox(float worldRadius)
    {
        if (hitbox == null)
        {
            GameObject holder = new GameObject("Pop-up Hitbox");
            holder.transform.SetParent(transform, false);
            hitbox = holder.AddComponent<SphereCollider>();
            hitbox.isTrigger = true;
        }
        hitbox.enabled = true;
        float scale = Mathf.Max(0.0001f, hitbox.transform.lossyScale.x);
        hitbox.radius = worldRadius / scale;
        hitbox.center = new Vector3(0, 0.2f / Mathf.Max(0.0001f, hitbox.transform.lossyScale.y), 0);
    }

    // A pulsing gold outline while it's out, like a prop that can be caught: hit me now
    void SetGlow(bool on)
    {
        if (on && glow == null)
        {
            glow = gameObject.AddComponent<Outline>();
            glow.OutlineMode = Outline.Mode.OutlineAll;
        }
        if (glow != null && glow.enabled != on) glow.enabled = on;
    }

    void Pulse()
    {
        if (glow == null || !glow.enabled) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
        glow.OutlineWidth = Mathf.Lerp(3f, 7f, pulse);
        glow.OutlineColor = Color.Lerp(settings.glowColour, Color.white, pulse * 0.6f);
    }

    void PlayBoing()
    {
        if (boing == null) boing = Resources.Load<AudioClip>("CafeSounds/boing");
        if (boing == null) return;
        if (voice == null)
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 1f;   // heard from the hole
            voice.minDistance = 3f;
            voice.maxDistance = 30f;
        }
        voice.pitch = Random.Range(0.9f, 1.15f);
        voice.PlayOneShot(boing);
    }
}
