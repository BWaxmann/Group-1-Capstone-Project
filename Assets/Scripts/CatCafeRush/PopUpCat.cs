using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Cat Cafe Waves' pop-up cats: whack-a-mole in the air. Pooled cats sit on the couches and chairs; now and then one
// crouches, boings, and springs straight up, showing a colour, hangs at the top for a moment glowing gold, and drops
// back onto its seat. The right colour while it's up pops it into confetti (and its seat is taken by a new cat); the
// wrong colour gets an angry face. A miss costs nothing: it just lands and waits to jump again.
public class PopUpCat : MonoBehaviour
{
    [Serializable]
    public class Settings
    {
        [Min(0.1f)]
        [Tooltip("Seconds of crouch and boing before the cat springs up (the warning).")]
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

    CatMovement cat;
    Animator animator;
    SphereCollider hitbox;
    Outline glow;
    OrderBubble bubble;
    Settings settings;
    Vector3 seat;
    Transform viewer;
    Action<PopUpCat> onHit;
    bool jumping;
    bool airborne;
    bool hit;
    int food = -1;
    float normalAnimatorSpeed = 1f;
    Vector3 normalScale;

    static AudioClip boing;
    AudioSource voice;

    public bool isIdle => !jumping && !hit && isActiveAndEnabled;
    public bool isOut => airborne && !hit;
    public int wantedFood => isOut ? food : -1;
    public Vector3 aimPoint => hitbox != null ? hitbox.transform.TransformPoint(hitbox.center) : transform.position + Vector3.up * 0.2f;

    // Sits a pooled cat (switched off) on `seat`, facing the player
    public static PopUpCat Seat(CatMovement cat, Vector3 seat, Transform viewer, Settings settings, Action<PopUpCat> onHit)
    {
        NavMeshAgent agent = cat.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        PopUpCat pop = cat.GetComponent<PopUpCat>();
        if (pop == null) pop = cat.gameObject.AddComponent<PopUpCat>();
        pop.cat = cat;
        pop.seat = seat;
        pop.viewer = viewer;
        pop.settings = settings;
        pop.onHit = onHit;
        pop.jumping = false;
        pop.airborne = false;
        pop.hit = false;
        pop.normalScale = cat.transform.localScale;

        cat.transform.position = seat;
        pop.FaceViewer();
        cat.gameObject.SetActive(true);
        cat.TakeOver();
        cat.treatGate = pop.Eat;
        pop.enabled = true;

        pop.animator = cat.GetComponentInChildren<Animator>();
        if (pop.animator != null)
        {
            pop.normalAnimatorSpeed = pop.animator.speed;
            pop.animator.speed = 0.4f;   // sitting about, not going anywhere
        }
        pop.SetHitbox(settings.hitRadius);
        cat.DustPuff(0.5f, seat);
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

    void FaceViewer()
    {
        Vector3 look = viewer != null ? viewer.position - seat : -transform.forward;
        look.y = 0;
        if (look.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(look);
    }

    // Springs up once, wanting `wanted` (a treat pool position), shown by `icon` in a bubble while it's up
    public void Jump(int wanted, OrderBubble.Item icon, float bubbleScale)
    {
        if (!isIdle) return;
        food = wanted;
        StartCoroutine(JumpRoutine(icon, bubbleScale));
    }

    IEnumerator JumpRoutine(OrderBubble.Item icon, float bubbleScale)
    {
        jumping = true;

        // The warning: it crouches down, wiggling, with a boing
        PlayBoing();
        float timer = 0;
        while (timer < settings.warning)
        {
            timer += Time.deltaTime;
            float crouch = Mathf.Sin(Mathf.Clamp01(timer / settings.warning) * Mathf.PI * 0.5f);
            transform.localScale = new Vector3(normalScale.x * (1f + 0.1f * crouch), normalScale.y * (1f - 0.25f * crouch), normalScale.z * (1f + 0.1f * crouch));
            transform.position = seat + transform.right * Mathf.Sin(timer * 40f) * 0.02f;
            yield return null;
        }
        transform.localScale = normalScale;

        // Up it springs (fast, slowing to the top), showing what it wants
        airborne = true;
        SetGlow(true);
        ShowWant(icon, bubbleScale);
        if (animator != null) animator.speed = normalAnimatorSpeed;
        Vector3 top = seat + Vector3.up * Random.Range(settings.heightMin, Mathf.Max(settings.heightMin, settings.heightMax));
        float t = 0;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / settings.riseTime);
            transform.position = Vector3.Lerp(seat, top, 1f - (1f - t) * (1f - t));
            Pulse();
            yield return null;
        }

        // Hangs there, paws out, wiggling
        if (animator != null) animator.speed = 0f;
        Quaternion facing = transform.rotation;
        timer = 0;
        while (timer < settings.hang)
        {
            timer += Time.deltaTime;
            transform.position = top + Vector3.up * Mathf.Sin(timer / settings.hang * Mathf.PI) * 0.05f;
            transform.rotation = facing * Quaternion.Euler(0, 0, Mathf.Sin(timer * 30f) * 8f);
            Pulse();
            yield return null;
        }
        transform.rotation = facing;

        // And back down onto its seat (faster and faster)
        t = 0;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / settings.fallTime);
            transform.position = Vector3.Lerp(top, seat, t * t);
            Pulse();
            yield return null;
        }
        transform.position = seat;
        airborne = false;
        SetGlow(false);
        if (bubble != null) bubble.Hide();
        if (animator != null) animator.speed = 0.4f;
        jumping = false;
    }

    // While it's up: the right colour pops it, the wrong one gets an angry face
    bool Eat(Treat treat)
    {
        if (!airborne || hit) return false;
        if (food >= 0 && treat.foodVariation != food)
        {
            if (bubble != null)
            {
                bubble.Shake();
                bubble.FlashFace(cat.grumpiestFace, 0.5f);
            }
            return false;
        }
        hit = true;
        onHit?.Invoke(this);
        StopAllCoroutines();
        StartCoroutine(HitPop());
        return false;
    }

    IEnumerator HitPop()
    {
        if (bubble != null) bubble.FlashFace(cat.happiestFace, 0f);
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.12f;
            transform.localScale = normalScale * (1f + 0.3f * Mathf.Min(t, 1f));
            yield return null;
        }
        cat.PopConfetti();
        Finish();
    }

    // Off its seat in a puff (e.g. the wave is over)
    public void Leave()
    {
        if (!isActiveAndEnabled) return;
        cat.DustPuff(0.5f, transform.position);
        Finish();
    }

    void Finish()
    {
        StopAllCoroutines();
        airborne = false;
        jumping = false;
        SetGlow(false);
        if (bubble != null) bubble.Hide();
        if (hitbox != null) hitbox.enabled = false;
        if (animator != null) animator.speed = normalAnimatorSpeed;
        transform.localScale = normalScale;
        cat.treatGate = null;
        enabled = false;
        cat.gameObject.SetActive(false);
    }

    public static void EndAll()
    {
        foreach (PopUpCat pop in active.ToArray())
        {
            if (!pop.hit) pop.Leave();
        }
    }

    void ShowWant(OrderBubble.Item icon, float bubbleScale)
    {
        if (food < 0) return;
        if (bubble == null) bubble = OrderBubble.Create(transform);
        bubble.gameObject.SetActive(true);
        bubble.transform.localScale = Vector3.one * bubbleScale;
        bubble.Show(new List<OrderBubble.Item> { icon }, new Vector3(0, BubbleHeight(bubbleScale), 0));
    }

    // Just above its head
    float BubbleHeight(float bubbleScale)
    {
        float top = 0.45f;
        bool any = false;
        foreach (Renderer rend in GetComponentsInChildren<Renderer>())
        {
            if (rend is ParticleSystemRenderer || rend is LineRenderer || rend is TrailRenderer) continue;
            if (rend.GetComponentInParent<OrderBubble>() != null || rend.GetComponentInParent<Canvas>() != null) continue;
            float height = rend.bounds.max.y - transform.position.y;
            top = any ? Mathf.Max(top, height) : height;
            any = true;
        }
        return Mathf.Clamp(top, 0.3f, 1.2f) + 0.2f * bubbleScale;
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

    // A pulsing gold outline while it's up, like a prop that can be caught: hit me now
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
            voice.spatialBlend = 1f;   // heard from the seat
            voice.minDistance = 3f;
            voice.maxDistance = 30f;
        }
        voice.pitch = Random.Range(0.9f, 1.15f);
        voice.PlayOneShot(boing);
    }
}
