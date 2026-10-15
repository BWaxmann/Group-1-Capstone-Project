using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Cat Cafe Waves: a hungry cat makes a run at something (the tip jar on the counter, or a pile). It winds up first
// (a "!" pops up, a rising sound plays from where it is, it wiggles), then sprints over, leaps up and swats it.
// The right treat during the wind-up or the sprint stops it ("Saved!"); once it's in the air it's too late.
public class CatRaid : MonoBehaviour
{
    public class Target
    {
        public Vector3 position;            // what gets swatted (its base)
        public Action<Vector3> swat;        // called with where the cat swats from
    }

    [Serializable]
    public class Settings
    {
        [Min(0.2f)]
        [Tooltip("Seconds the cat winds up (with a \"!\") before it runs: the first part of the window to stop it.")]
        public float windUp = 1.2f;
        [Min(1f)]
        [Tooltip("How much faster than its walk the cat sprints at the target.")]
        public float sprintMultiplier = 2.5f;
        [Min(0)]
        [Tooltip("Points for stopping a raid with the right treat.")]
        public int saveBonus = 5;
        public string savedText = "Saved!";
        public Color warningColour = new Color(1f, 0.35f, 0.1f);
    }

    public enum Result { Saved, Swatted, Served, Cancelled }

    public static readonly List<CatRaid> active = new List<CatRaid>();

    CatOrder order;
    CatMovement cat;
    NavMeshAgent agent;
    Animator animator;
    Target target;
    Settings settings;
    Action<CatRaid, Result> done;
    Vector3 floorSpot;
    Coroutine routine;
    bool committed;   // in the air: too late to stop
    bool running;

    float savedSpeed, savedAcceleration, savedAngularSpeed;
    bool savedUpdateRotation;

    GameObject warning;
    AudioSource voice;
    static AudioClip windUpClip;

    public CatOrder catOrder => order;
    public bool isRunning => running;

    // Sends `order`'s cat after `target`. Returns null if it can't get there.
    public static CatRaid Begin(CatOrder order, Target target, Settings settings, Action<CatRaid, Result> done)
    {
        NavMeshAgent agent = order.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return null;
        if (!FloorSpotNear(target.position, agent, out Vector3 spot)) return null;

        CatRaid raid = order.GetComponent<CatRaid>();
        if (raid == null) raid = order.gameObject.AddComponent<CatRaid>();
        raid.Setup(order, agent, target, settings, done, spot);
        return raid;
    }

    // A floor spot near the target that this cat can actually run to (the target itself may be up on the counter)
    static bool FloorSpotNear(Vector3 target, NavMeshAgent agent, out Vector3 spot)
    {
        spot = Vector3.zero;
        float floorY = agent.transform.position.y;
        float best = float.MaxValue;
        NavMeshPath path = new NavMeshPath();
        for (float radius = 0.5f; radius <= 2.01f; radius += 0.5f)
        {
            for (int angle = 0; angle < 360; angle += 30)
            {
                Vector3 probe = target + Quaternion.Euler(0, angle, 0) * Vector3.forward * radius;
                probe.y = floorY;
                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 0.4f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(hit.position.y - floorY) > 0.3f) continue;
                float off = new Vector2(hit.position.x - target.x, hit.position.z - target.z).magnitude;
                if (off >= best) continue;
                if (!NavMesh.CalculatePath(agent.transform.position, hit.position, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete) continue;
                best = off;
                spot = hit.position;
            }
            if (best < float.MaxValue) return true;   // the nearest ring that works
        }
        return false;
    }

    void Setup(CatOrder order, NavMeshAgent agent, Target target, Settings settings, Action<CatRaid, Result> done, Vector3 spot)
    {
        this.order = order;
        this.agent = agent;
        this.target = target;
        this.settings = settings;
        this.done = done;
        floorSpot = spot;
        cat = GetComponent<CatMovement>();
        animator = GetComponentInChildren<Animator>();
        committed = false;
        running = true;
        if (!active.Contains(this)) active.Add(this);
        order.rightTreat -= OnRightTreat;
        order.rightTreat += OnRightTreat;
        routine = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // Wind-up: stops, faces its target, wiggles, with a "!" and a rising sound
        cat.StopAllCoroutines();   // stops its roaming
        savedSpeed = agent.speed;
        savedAcceleration = agent.acceleration;
        savedAngularSpeed = agent.angularSpeed;
        savedUpdateRotation = agent.updateRotation;
        agent.isStopped = true;
        agent.updateRotation = false;
        ShowWarning(true);
        PlayWindUp();

        Vector3 look = target.position - transform.position;
        look.y = 0;
        Quaternion facing = look.sqrMagnitude > 0.001f ? Quaternion.LookRotation(look) : transform.rotation;
        Quaternion turning = transform.rotation;
        float timer = 0;
        while (timer < settings.windUp)
        {
            timer += Time.deltaTime;
            // Turns to face it, then a quick side-to-side wiggle (getting ready to pounce)
            turning = Quaternion.Slerp(turning, facing, Time.deltaTime * 10f);
            float wiggle = Mathf.Sin(timer * 38f) * 9f * Mathf.Clamp01(timer / 0.3f);
            transform.rotation = turning * Quaternion.Euler(0, wiggle, 0);
            yield return null;
        }
        transform.rotation = facing;

        // Sprint
        agent.speed = savedSpeed * settings.sprintMultiplier;
        agent.acceleration = Mathf.Max(savedAcceleration, 25f);
        agent.angularSpeed = Mathf.Max(savedAngularSpeed, 720f);
        agent.updateRotation = true;
        agent.isStopped = false;
        agent.SetDestination(floorSpot);
        timer = 0;
        float left = float.MaxValue;
        while (timer < 6f)
        {
            timer += Time.deltaTime;
            Vector3 gap = floorSpot - transform.position;
            gap.y = 0;
            left = gap.magnitude;
            if (!agent.pathPending && left < 0.35f) break;
            yield return null;
        }
        if (left > 1.5f)
        {
            // Got stuck on the way: it gives up and wanders off
            Finish(Result.Cancelled, true);
            yield break;
        }

        // Too late now: up it goes and swats
        committed = true;
        order.busy = true;
        ShowWarning(false);
        RestoreAgentSettings();
        agent.isStopped = true;
        agent.enabled = false;

        Vector3 from = transform.position;
        Vector3 towards = target.position - from;
        towards.y = 0;
        float reach = towards.magnitude;
        towards = reach > 0.001f ? towards / reach : transform.forward;
        transform.rotation = Quaternion.LookRotation(towards);
        bool upOnTop = target.position.y - from.y > 0.35f;
        Vector3 landing = upOnTop
            ? new Vector3(target.position.x, target.position.y, target.position.z) - towards * 0.25f
            : from + towards * Mathf.Max(0, reach - 0.3f);
        yield return Hop(from, landing, upOnTop ? 0.45f : 0.25f, upOnTop ? 0.45f : 0.15f);

        if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger("AngryCat");
        yield return new WaitForSeconds(0.15f);
        target.swat?.Invoke(transform.position);
        yield return new WaitForSeconds(0.45f);

        if (upOnTop) yield return Hop(transform.position, floorSpot, 0.4f, 0.3f);
        Finish(Result.Swatted, true);
    }

    IEnumerator Hop(Vector3 from, Vector3 to, float seconds, float height)
    {
        float timer = 0;
        while (timer < seconds)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / seconds);
            transform.position = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * (height + Mathf.Max(0, to.y - from.y) * 0.5f);
            yield return null;
        }
        transform.position = to;
    }

    // The right treat while it's winding up or sprinting stops the raid
    void OnRightTreat(CatOrder fed)
    {
        if (!running || committed) return;
        bool stillHungry = fed.isHungry;
        if (settings.saveBonus > 0) GameManager.instance.AddBonus(settings.saveBonus, fed.aboveBubble);
        FloatingWord.Show(settings.savedText, fed.aboveBubble, new Color(0.4f, 1f, 0.5f));
        // A finished order goes off happy on its own; otherwise it forgets the raid and wanders off again
        Finish(stillHungry ? Result.Saved : Result.Served, stillHungry);
    }

    // Stops the raid where it is (e.g. the cats are being sent away), putting the cat back on the floor
    public void Cancel()
    {
        if (running) Finish(Result.Cancelled, false);
    }

    void Finish(Result result, bool resumeRoaming)
    {
        if (!running) return;
        running = false;
        if (routine != null) StopCoroutine(routine);
        routine = null;
        ShowWarning(false);
        if (voice != null) voice.Stop();
        order.busy = false;
        order.rightTreat -= OnRightTreat;
        active.Remove(this);

        if (agent != null)
        {
            if (!agent.enabled)
            {
                agent.enabled = true;
                agent.Warp(committed ? floorSpot : transform.position);
            }
            if (!committed) RestoreAgentSettings();
        }
        committed = false;
        if (resumeRoaming && cat != null) cat.ResumeRoaming();
        done?.Invoke(this, result);
    }

    void RestoreAgentSettings()
    {
        agent.speed = savedSpeed;
        agent.acceleration = savedAcceleration;
        agent.angularSpeed = savedAngularSpeed;
        agent.updateRotation = savedUpdateRotation;
    }

    void OnDestroy()
    {
        if (warning != null) Destroy(warning);
    }

    void OnDisable()
    {
        // Switched off mid-raid (e.g. sent back to the pool): just tidy up
        if (running)
        {
            running = false;
            ShowWarning(false);
            if (order != null)
            {
                order.busy = false;
                order.rightTreat -= OnRightTreat;
            }
            active.Remove(this);
            if (agent != null && agent.speed != savedSpeed && savedSpeed > 0) RestoreAgentSettings();
            done?.Invoke(this, Result.Cancelled);
        }
    }

    public static void CancelAll()
    {
        foreach (CatRaid raid in active.ToArray()) raid.Cancel();
    }

    // ---------- Tells ----------

    void ShowWarning(bool show)
    {
        if (show)
        {
            if (warning == null) warning = FloatingWord.Make("!", settings.warningColour, 5f);
            warning.SetActive(true);
            StartCoroutine(BounceWarning());
        }
        else if (warning != null)
        {
            warning.SetActive(false);
        }
    }

    IEnumerator BounceWarning()
    {
        float timer = 0;
        while (warning != null && warning.activeSelf)
        {
            timer += Time.deltaTime;
            warning.transform.position = order.aboveBubble + Vector3.up * Mathf.Abs(Mathf.Sin(timer * 9f)) * 0.08f;
            warning.transform.localScale = Vector3.one * (1f + 0.25f * Mathf.Clamp01(1f - timer * 4f));
            FloatingWord.FacePlayer(warning.transform);
            yield return null;
        }
    }

    void PlayWindUp()
    {
        if (windUpClip == null) windUpClip = Resources.Load<AudioClip>("CafeSounds/windup");
        if (windUpClip == null) return;
        if (voice == null)
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 1f;   // heard from where the cat is
            voice.minDistance = 3f;
            voice.maxDistance = 30f;
        }
        voice.pitch = Random.Range(0.95f, 1.08f);
        voice.PlayOneShot(windUpClip);
    }
}
