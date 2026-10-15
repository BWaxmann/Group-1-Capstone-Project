using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Cat Cafe Waves' kitten stampede: one kitten (a pooled cat, shrunk) galloping along a path across the room with a
// dust trail. Any treat that reaches it feeds it: a happy hop, a pop of confetti, and it's gone. One that makes it
// across just dashes off in a puff of dust. Either way the cat goes back to the pool as it was.
public class KittenRun : MonoBehaviour
{
    public static readonly List<KittenRun> running = new List<KittenRun>();

    CatMovement cat;
    Animator animator;
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
    float normalAnimatorSpeed = 1f;

    static Material dustMaterial;

    // Where a treat should aim (the middle of its hit area)
    public Vector3 aimPoint => hitbox != null ? hitbox.transform.TransformPoint(hitbox.center) : transform.position + Vector3.up * 0.15f;
    public bool isFed => fed;

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

        run.animator = cat.GetComponentInChildren<Animator>();
        if (run.animator != null)
        {
            run.normalAnimatorSpeed = run.animator.speed;
            run.animator.speed = 1.8f;   // little legs going fast
        }
        run.SetHitbox(hitRadius);
        run.SetDust(true);
        return run;
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
        if (fed) return false;
        fed = true;
        onFed?.Invoke(this);
        StartCoroutine(HappyPop());
        return false;
    }

    void Update()
    {
        if (fed) return;
        travelled += speed * Time.deltaTime;
        if (travelled >= pathLength)
        {
            // Made it across: off it dashes
            cat.DisappearParticleEffect();
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

    IEnumerator HappyPop()
    {
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
        if (animator != null) animator.speed = normalAnimatorSpeed;
        cat.treatGate = null;
        transform.localScale = normalScale;
        enabled = false;
        cat.gameObject.SetActive(false);
    }

    // When the stampede is over: every kitten still out dashes off
    public static void EndAll()
    {
        foreach (KittenRun run in running.ToArray())
        {
            if (run.fed) continue;
            run.cat.DisappearParticleEffect();
            run.Finish();
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
        dust.widthMultiplier = 0.22f;
        dust.Clear();
        dust.emitting = on;
    }
}
