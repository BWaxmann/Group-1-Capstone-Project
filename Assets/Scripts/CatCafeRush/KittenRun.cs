using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Cat Cafe Waves' kitten stampede: one kitten (a pooled cat, shrunk) galloping along a path across the room with a
// dust trail. Any treat that reaches it feeds it: a happy hop, a pop of confetti, and it's gone. One that makes it
// across just dashes off in a puff of dust. Either way the cat goes back to the pool as it was.
public class KittenRun : MonoBehaviour
{
    public static readonly List<KittenRun> running = new List<KittenRun>();

    // Kittens' dust puffs, compared with a cat's usual puff
    public const float DustSize = 0.85f;

    // Kittens squeak: the cats' own meows, pitched up. No two closer together than this (seconds), so a whole
    // herd doesn't turn into noise.
    const float MeowGap = 0.12f;
    static float lastMeow = -10f;
    float nextMeow;

    CatMovement cat;
    TrailRenderer dust;
    SphereCollider hitbox;
    List<Vector3> path;
    float offset;
    float speed;
    float travelled;
    float pathLength;
    bool fed;
    Action<KittenRun> onFed;
    Vector3 normalScale;
    Transform homeParent;
    CatRunAnimation runAnimation;
    static readonly int RunSpeed = Animator.StringToHash("RunSpeed");

    // Sets the run cycle's speed on whichever of the cat's animators has one (0 = holds still)
    public void SetLegs(float runSpeed)
    {
        foreach (Animator legs in GetComponentsInChildren<Animator>())
        {
            if (legs.runtimeAnimatorController == null) continue;
            foreach (AnimatorControllerParameter parameter in legs.parameters)
            {
                if (parameter.nameHash == RunSpeed)
                {
                    legs.SetFloat(RunSpeed, runSpeed);
                    break;
                }
            }
        }
    }

    static Material dustMaterial;

    // Where a treat should aim (the middle of its hit area)
    public Vector3 aimPoint => hitbox != null ? hitbox.transform.TransformPoint(hitbox.center) : transform.position + Vector3.up * 0.15f;
    public bool isFed => fed;
    // False for a kitten launched with no hit area (a launcher kitten): treats go straight through it
    public bool feedable { get; private set; }
    // Stopped running (another script has it now, e.g. a launcher kitten leaping), or waiting at the end of its path
    public bool held { get; private set; }
    // Waits at the end of its path instead of dashing off
    public bool holdAtEnd;

    // Sends a pooled cat (switched off) galloping along `path`, `offset` metres to the side of it
    public static KittenRun Launch(CatMovement cat, List<Vector3> path, float offset, float speed, float scale, float hitRadius,
                                   Action<KittenRun> onFed)
    {
        // Take it over before it wakes up: no navmesh agent (this moves it), no roaming
        NavMeshAgent agent = cat.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        KittenRun run = cat.GetComponent<KittenRun>();
        if (run == null) run = cat.gameObject.AddComponent<KittenRun>();
        run.cat = cat;
        run.path = path;
        run.offset = offset;
        run.speed = speed;
        run.onFed = onFed;
        run.travelled = 0;
        run.fed = false;
        run.held = false;
        run.holdAtEnd = false;
        run.homeParent = cat.transform.parent;
        run.pathLength = 0;
        for (int i = 1; i < path.Count; i++) run.pathLength += Vector3.Distance(path[i - 1], path[i]);

        run.normalScale = cat.transform.localScale;
        cat.transform.localScale = run.normalScale * scale;
        cat.transform.position = run.PointAlong(0, out Vector3 heading);
        cat.transform.rotation = Quaternion.LookRotation(heading);
        cat.gameObject.SetActive(true);
        cat.TakeOver();
        cat.treatGate = run.Eat;
        run.enabled = true;

        // Little legs going fast. The run cycle's speed is normally set from the navigation agent (which a kitten
        // doesn't use), so that's switched off and the speed set here: faster when it runs faster, and faster
        // again for being small (shorter strides)
        run.runAnimation = cat.GetComponent<CatRunAnimation>();
        if (run.runAnimation != null) run.runAnimation.enabled = false;
        run.SetLegs(speed / Mathf.Max(0.1f, scale));
        run.feedable = hitRadius > 0;
        if (run.feedable) run.SetHitbox(hitRadius);
        else if (run.hitbox != null) run.hitbox.enabled = false;
        run.SetDust(true);
        // Most squeak as they burst in; the rest a little later
        run.nextMeow = Time.time + (Random.value < 0.7f ? Random.Range(0f, 0.6f) : Random.Range(1f, 2f));
        return run;
    }

    public void TryMeow(float pitchMin, float pitchMax)
    {
        if (Time.time - lastMeow < MeowGap) return;
        lastMeow = Time.time;
        cat.Meow(Random.Range(pitchMin, pitchMax));
    }

    void OnEnable()
    {
        if (!running.Contains(this)) running.Add(this);
    }

    void OnDisable()
    {
        running.Remove(this);
    }

    // Any treat feeds a kitten (no colours in a stampede)
    bool Eat(Treat treat)
    {
        if (fed || !feedable) return false;
        fed = true;
        TryMeow(1.75f, 2.05f);   // a happy squeak
        onFed?.Invoke(this);
        StartCoroutine(HappyPop());
        return false;
    }

    void Update()
    {
        if (fed || held) return;
        if (Time.time >= nextMeow)
        {
            TryMeow(1.45f, 1.85f);
            nextMeow = Time.time + Random.Range(1.2f, 2.5f);
        }
        travelled += speed * Time.deltaTime;
        if (travelled >= pathLength)
        {
            if (holdAtEnd)
            {
                Hold();
                return;
            }
            // Made it across: off it dashes
            cat.DustPuff(DustSize);
            Finish();
            return;
        }

        Vector3 along = PointAlong(travelled, out Vector3 heading);
        Vector3 side = Vector3.Cross(Vector3.up, heading).normalized * offset;
        Vector3 position = along + side;
        // Keep to the floor (the offset could push it into furniture: then it runs on the path itself)
        if (NavMesh.SamplePosition(position, out NavMeshHit floor, 0.3f, NavMesh.AllAreas)) position = floor.position;
        else position = along;
        position.y += Mathf.Abs(Mathf.Sin(travelled * 5f)) * 0.06f;   // a little gallop
        transform.position = position;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), Time.deltaTime * 12f);
    }

    // Stops it running along its path (it stays where it is, legs and all, for whoever has it now)
    public void Hold()
    {
        held = true;
        SetDust(false);
    }

    // Off it goes in a puff of dust, back to the pool
    public void Dismiss()
    {
        if (!enabled) return;
        cat.DustPuff(DustSize);
        Finish();
    }

    IEnumerator HappyPop()
    {
        // Off whatever it was clinging to first (a launcher kitten), so it pops where it is
        if (transform.parent != homeParent) transform.SetParent(homeParent, true);
        Vector3 start = transform.position;
        Vector3 small = transform.localScale;
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.2f;
            transform.position = start + Vector3.up * Mathf.Sin(Mathf.Min(t, 1f) * Mathf.PI) * 0.3f;
            transform.localScale = small * (1f + 0.4f * t);
            yield return null;
        }
        cat.PopConfetti();
        Finish();
    }

    // Back to the pool, just as it came
    void Finish()
    {
        StopAllCoroutines();
        SetDust(false);
        if (hitbox != null) hitbox.enabled = false;
        SetLegs(0f);
        if (runAnimation != null) runAnimation.enabled = true;   // back to following the navigation agent
        cat.treatGate = null;
        if (transform.parent != homeParent) transform.SetParent(homeParent, true);
        transform.localScale = normalScale;
        enabled = false;
        cat.gameObject.SetActive(false);
    }

    // When the stampede is over: every kitten still out dashes off
    public static void EndAll()
    {
        foreach (KittenRun run in running.ToArray())
        {
            if (!run.fed) run.Dismiss();
        }
    }

    Vector3 PointAlong(float distance, out Vector3 heading)
    {
        heading = Vector3.forward;
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from = path[i - 1];
            Vector3 to = path[i];
            float length = Vector3.Distance(from, to);
            Vector3 flat = to - from;
            flat.y = 0;
            if (flat.sqrMagnitude > 0.0001f) heading = flat.normalized;
            if (distance <= length || i == path.Count - 1)
            {
                return Vector3.Lerp(from, to, length > 0.0001f ? Mathf.Clamp01(distance / length) : 1f);
            }
            distance -= length;
        }
        return path[0];
    }

    // A generous hit area: kittens are small and quick
    void SetHitbox(float worldRadius)
    {
        if (hitbox == null)
        {
            GameObject holder = new GameObject("Kitten Hitbox");
            holder.transform.SetParent(transform, false);
            hitbox = holder.AddComponent<SphereCollider>();
            hitbox.isTrigger = true;
        }
        hitbox.enabled = true;
        float scale = Mathf.Max(0.0001f, hitbox.transform.lossyScale.x);
        hitbox.radius = worldRadius / scale;
        hitbox.center = new Vector3(0, worldRadius * 0.8f / Mathf.Max(0.0001f, hitbox.transform.lossyScale.y), 0);
    }

    void SetDust(bool on)
    {
        if (on && dust == null)
        {
            if (dustMaterial == null) dustMaterial = SpaceCafeVisuals.UnlitMaterial(Color.white);
            dust = gameObject.AddComponent<TrailRenderer>();
            dust.sharedMaterial = dustMaterial;
            dust.time = 0.35f;
            dust.minVertexDistance = 0.05f;
            dust.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dust.receiveShadows = false;
            Color tan = new Color(0.78f, 0.68f, 0.52f);
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(tan, 0), new GradientColorKey(tan, 1) },
                         new[] { new GradientAlphaKey(0.55f, 0), new GradientAlphaKey(0, 1) });
            dust.colorGradient = fade;
        }
        if (dust == null) return;
        // The trail is sized in the world, whatever size the cat is
        dust.widthMultiplier = 0.19f;
        dust.Clear();
        dust.emitting = on;
    }
}
