using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Cat Cafe Waves: a spy from the competition, after the cafe's secret recipe. A ceiling vent clanks open and a cat
// lowers down on a rope, spread-eagled Mission Impossible style, creeping down towards a recipe card on the table (or
// floor) under the vent. The player scares it off by looking at it and waving their arms: a ring round it fills, it
// yelps and zips back up. If it reaches the recipe it snatches it, knocks some tips out of the jar, and zips off.
public class SpyCat : MonoBehaviour
{
    [Serializable]
    public class Settings
    {
        [Min(0.3f)]
        [Tooltip("Seconds it takes to lower itself to just under the player's head height.")]
        public float lowerSeconds = 2f;
        [Min(0f)]
        [Tooltip("How far below the player's eyes it hangs at the bottom of its swing, in metres.")]
        public float hangBelowEyes = 0.3f;
        [Min(1f)]
        [Tooltip("Seconds of swinging, closer each time, before a swing reaches the player (the time to scare it off).")]
        public float approachSeconds = 8f;
        [Min(0.5f)]
        [Tooltip("Seconds for one swing there and back.")]
        public float swingPeriod = 2.2f;
        [Range(10f, 75f)]
        [Tooltip("How far out it swings at most, in degrees from straight down.")]
        public float swingMaxAngle = 50f;
        [Min(0.3f)]
        [Tooltip("How close it has to get to the player's head to snatch the recipe, in metres.")]
        public float reachDistance = 0.9f;
        [Range(0.3f, 1.5f)] public float scale = 0.85f;
        [Range(5f, 60f)]
        [Tooltip("How directly the player has to be looking at it for waving to count, in degrees.")]
        public float lookAngle = 25f;
        [Min(0.2f)]
        [Tooltip("How fast the hands have to be moving (m/s, the two averaged) for waving at full strength.")]
        public float waveSpeed = 1.6f;
        [Min(0.2f)]
        [Tooltip("Seconds of full-strength waving it takes to scare it off.")]
        public float scareSeconds = 2.2f;
        [Min(0f)]
        [Tooltip("How fast its nerve comes back when the player stops waving (ring per second).")]
        public float calmDown = 0.5f;
        [Min(1)]
        [Tooltip("Back-and-forth swings of the hands in the last second for it to count as waving (so swinging round to aim doesn't).")]
        public int swingsNeeded = 3;
        [Min(0)] public int scareScore = 10;
        [Min(0)] public int scareCoins = 2;
        [Min(0)]
        [Tooltip("Coins knocked out of the tip jar when it gets away with the recipe.")]
        public int stealCoins = 6;
        public string scaredText = "Shoo!";
        public string stolenText = "Recipe stolen!";
        public string hint = "WAVE YOUR ARMS!";
        public Color ringColour = new Color(1f, 0.85f, 0.2f);
        public Color ropeColour = new Color(0.08f, 0.08f, 0.1f);
    }

    public enum Result { Scared, Stole, Cancelled }

    public static readonly List<SpyCat> active = new List<SpyCat>();

    // While this says so, it hangs there swinging gently instead of coming in (it can still be scared off): e.g. the first
    // spy of the wave, while the announcer explains what to do
    public Func<bool> holdBack;
    public bool isFinished
    {
        get
        {
            return finished;
        }
    }
    static int hintsShown;
    internal static void ResetStatics()   // (a new run: see RunReset)
    {
        active.Clear();
        hintsShown = 0;
    }
    const int Hints = 2;
    static readonly int Spying = Animator.StringToHash("Spying");

    CatMovement cat;
    CafeVent vent;
    PlayerInput player;
    TipJar jar;
    Settings settings;
    Action<SpyCat, Result> done;
    LineRenderer rope;
    LineRenderer ring;
    GameObject recipe;
    GameObject hintWord;
    Vector3 ownScale;
    Vector3 lastLeft, lastRight;
    float leftSpeed, rightSpeed;
    float nerve;            // 0 calm, 1 scared off
    float nextBuzz;
    bool finished;
    float backHeight;       // from its feet up to where the rope ties on
    Vector3 anchor;         // the top of the rope (it creeps along the ceiling towards the player as the cat swings)
    Transform ropePoint;    // the clip on its harness (a child called "Rope Point"), if it has one

    public bool isDangling
    {
        get
        {
            return !finished;
        }
    }

    // Lowers a pooled cat (switched off) out of a ceiling vent, to swing in at the player
    public static SpyCat Begin(CatMovement cat, CafeVent vent, PlayerInput player, TipJar jar, Settings settings,
                               Action<SpyCat, Result> done)
    {
        if (cat == null || vent == null || player == null)
        {
            return null;
        }

        SpyCat spy = cat.GetComponent<SpyCat>();
        if (spy == null)
        {
            spy = cat.gameObject.AddComponent<SpyCat>();
        }
        spy.cat = cat;
        spy.vent = vent;
        spy.player = player;
        spy.jar = jar;
        spy.settings = settings;
        spy.done = done;
        spy.finished = false;
        spy.nerve = 0;
        spy.holdBack = null;
        spy.enabled = true;
        // Switched on (tucked up inside the vent, out of sight) before anything runs: a pooled cat starts off switched off,
        // and nothing can run on a switched-off cat
        NavMeshAgent walker = cat.GetComponent<NavMeshAgent>();
        if (walker != null)
        {
            walker.enabled = false;
        }
        cat.transform.position = vent.opening + Vector3.up * 0.6f;
        cat.gameObject.SetActive(true);
        spy.StartCoroutine(spy.Run());
        return spy;
    }

    void OnEnable()
    {
        if (!active.Contains(this))
        {
            active.Add(this);
        }
    }

    void OnDisable()
    {
        active.Remove(this);
        if (!finished)
        {
            finished = true;
            CleanUp();
            done?.Invoke(this, Result.Cancelled);
        }
    }

    IEnumerator Run()
    {
        // Out of the pool, hidden in the vent until the lid's open
        NavMeshAgent agent = cat.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }
        ownScale = cat.transform.localScale;
        cat.transform.localScale = ownScale * settings.scale;
        cat.treatGate = treat => false;   // it's not here to eat
        cat.wantsTreats = () => false;
        Transform eye = player.eyePoint;
        Vector3 head;
        if (eye != null)
        {
            head = eye.position;
        }
        else
        {
            head = player.transform.position + Vector3.up * 1.6f;
        }
        anchor = vent.opening;
        Vector3 toward = new Vector3(head.x - anchor.x, 0, head.z - anchor.z);
        float distance = toward.magnitude;
        if (distance > 0.01f)
        {
            toward = toward / distance;
        }
        else
        {
            toward = Vector3.forward;
        }
        cat.transform.SetPositionAndRotation(anchor + Vector3.up * 0.4f, Quaternion.LookRotation(toward));
        cat.gameObject.SetActive(true);
        cat.TakeOver();
        CatWaddle waddle = cat.GetComponent<CatWaddle>();
        if (waddle != null)
        {
            waddle.enabled = false;   // hanging, not walking
        }
        SetSpying(true);
        ropePoint = FindRopePoint(cat.transform);
        if (ropePoint != null)
        {
            backHeight = Mathf.Max(0.1f, ropePoint.position.y - cat.transform.position.y);
        }
        else
        {
            backHeight = BackHeight();
        }

        yield return vent.Open();
        Sting();
        MakeRope();
        StartWatchingHands();
        if (hintsShown < Hints && !string.IsNullOrEmpty(settings.hint))
        {
            hintsShown++;
            hintWord = FloatingWord.Make(settings.hint, settings.ringColour, 2.5f);
        }

        // Down to just under head height first, paying out the rope
        float hangY = Mathf.Min(anchor.y - 0.8f, head.y - settings.hangBelowEyes);
        float rope = Mathf.Max(0.5f, anchor.y - hangY);   // from the anchor down to the clip on its back
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / settings.lowerSeconds;
            Hang(anchor, Mathf.Lerp(0.2f, rope, Mathf.SmoothStep(0, 1, t)), 0, toward);
            Scare(false);   // can't be scared yet: the player gets to see it arrive
            yield return null;
        }

        // Held back for now: a gentle sway where it is (and it can be scared off already)
        float sway = 0;
        while (holdBack != null && holdBack())
        {
            sway += Time.deltaTime;
            Hang(anchor, rope, 8f * Mathf.Deg2Rad * Mathf.Sin(sway * 2f * Mathf.PI / settings.swingPeriod), toward);
            if (Scare())
            {
                yield return Scared();
                yield break;
            }
            yield return null;
        }

        // Then it swings in at the player, bigger and closer each time: the top of the rope creeps along the ceiling
        // towards them, until a swing reaches them
        float swingMax = settings.swingMaxAngle * Mathf.Deg2Rad;
        float creepTo = Mathf.Max(0, distance - rope * Mathf.Sin(swingMax) - settings.reachDistance * 0.5f);
        Vector3 start = anchor;
        float phase = 0;
        float progress = 0;
        float lastSwing = 0;
        while (true)
        {
            progress = Mathf.Min(1f, progress + Time.deltaTime / settings.approachSeconds);
            anchor = start + toward * (creepTo * progress);
            phase += Time.deltaTime * 2f * Mathf.PI / settings.swingPeriod;
            float amplitude = Mathf.Lerp(8f * Mathf.Deg2Rad, swingMax, progress);
            float swing = amplitude * Mathf.Sin(phase);
            Hang(anchor, rope, swing, toward);

            // A whoosh past: a buzz each time it swings in close
            if (swing > amplitude * 0.9f && lastSwing <= amplitude * 0.9f && progress > 0.5f)
            {
                Haptics.PulseBoth(Haptics.Strength.Light);
            }
            lastSwing = swing;

            if (Scare())
            {
                yield return Scared();
                yield break;
            }
            Vector3 middle = cat.transform.position + Vector3.up * backHeight * 0.5f;
            bool reached = (middle - head).magnitude <= settings.reachDistance;
            if (reached || (progress >= 1f && swing >= swingMax * 0.97f))
            {
                break;
            }
            yield return null;
        }

        // Got to the player: snatches the recipe (and knocks some tips loose), then off it zips
        Haptics.PulseBoth(Haptics.Strength.Strong);
        cat.Meow(0.9f);
        MakeRecipeInPaws();
        FloatingWord.Show(settings.stolenText, cat.transform.position + Vector3.up * 0.6f, new Color(1f, 0.35f, 0.25f), 3.5f, 1.6f);
        if (jar != null && settings.stealCoins > 0)
        {
            jar.Spill(jar.transform.position + (cat.transform.position - jar.transform.position).normalized * 0.3f, settings.stealCoins);
        }
        HideRing();
        yield return ZipUp(0.6f);
        Finish(Result.Stole);
    }

    // The player looking at it and waving their arms: its nerve goes; true once it's scared off (never while it `can't`
    // be yet, though the hands are still watched)
    bool Scare(bool can = true)
    {
        Transform eye = player.eyePoint;
        Vector3 middle = cat.transform.position + cat.transform.up * backHeight * 0.5f;
        float waving = HandSpeed();
        if (!can || SwingsLately() < settings.swingsNeeded)
        {
            waving = 0;   // a real wave goes back and forth
        }
        bool looking = eye != null && Vector3.Angle(eye.forward, middle - eye.position) <= settings.lookAngle;
        if (looking && waving > settings.waveSpeed * 0.35f)
        {
            nerve += Time.deltaTime / settings.scareSeconds * Mathf.Clamp01(waving / settings.waveSpeed);
            if (Time.time >= nextBuzz)
            {
                Haptics.PulseBoth(Haptics.Strength.Light);
                nextBuzz = Time.time + 0.3f;
            }
        }
        else
        {
            nerve = Mathf.MoveTowards(nerve, 0, settings.calmDown * Time.deltaTime);
        }
        ShowRing(middle, eye, nerve);
        if (hintWord != null)
        {
            hintWord.transform.position = middle + Vector3.up * (0.65f + Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 0.05f);
            FloatingWord.FacePlayer(hintWord.transform);
        }
        return nerve >= 1f;
    }

    // Scared off: a yelp, and it shoots back up the rope
    IEnumerator Scared()
    {
        HideRing();
        if (hintWord != null)
        {
            Destroy(hintWord);
        }
        cat.Meow(1.7f);
        Haptics.PulseBoth(Haptics.Strength.Medium);
        FloatingWord.Show(settings.scaredText, cat.transform.position + Vector3.up * 0.6f, new Color(0.4f, 1f, 0.5f), 3.5f, 1.2f);
        if (settings.scareScore > 0)
        {
            GameManager.instance.AddBonus(settings.scareScore, cat.transform.position);
        }
        if (jar != null && settings.scareCoins > 0)
        {
            jar.AddCoins(cat.transform.position, settings.scareCoins);
        }
        if (recipe != null)
        {
            Destroy(recipe);
        }
        yield return ZipUp(0.45f);
        Finish(Result.Scared);
    }

    IEnumerator ZipUp(float seconds)
    {
        // Back up the rope to its vent (the top of the rope sliding back along the ceiling with it)
        Vector3 from = cat.transform.position;
        Vector3 to = vent.opening + Vector3.up * 0.3f;
        Vector3 anchorFrom = anchor;
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / seconds;
            anchor = Vector3.Lerp(anchorFrom, vent.opening, t);
            cat.transform.position = Vector3.Lerp(from, to, t * t);
            UpdateRope();
            yield return null;
        }
    }

    void Finish(Result result)
    {
        if (finished)
        {
            return;
        }
        finished = true;
        CleanUp();
        if (vent != null)
        {
            vent.StartCoroutine(vent.Close());
        }
        done?.Invoke(this, result);
        // Back to the pool as it came
        cat.transform.localScale = ownScale;
        cat.treatGate = null;
        cat.wantsTreats = null;
        enabled = false;
        cat.gameObject.SetActive(false);
    }

    // The phase or round is over: it zips off (where it is)
    public void Cancel()
    {
        if (finished)
        {
            return;
        }
        StopAllCoroutines();
        StartCoroutine(CancelRoutine());
    }

    IEnumerator CancelRoutine()
    {
        HideRing();
        yield return ZipUp(0.4f);
        Finish(Result.Cancelled);
    }

    public static void EndAll()
    {
        foreach (SpyCat spy in active.ToArray())
        {
            spy.Cancel();
        }
    }

    void CleanUp()
    {
        SetSpying(false);
        CatWaddle waddle;
        if (cat != null)
        {
            waddle = cat.GetComponent<CatWaddle>();
        }
        else
        {
            waddle = null;
        }
        if (waddle != null)
        {
            waddle.enabled = true;
        }
        if (rope != null)
        {
            Destroy(rope.gameObject);
        }
        if (ring != null)
        {
            Destroy(ring.transform.parent.gameObject);
        }
        if (hintWord != null)
        {
            Destroy(hintWord);
        }
        if (recipe != null)
        {
            Destroy(recipe);
        }
    }

    // ---------- The hands ----------

    void StartWatchingHands()
    {
        lastLeft = Hand(false);
        lastRight = Hand(true);
        leftSpeed = rightSpeed = 0;
        leftSwing = rightSwing = Vector3.zero;
        swings.Clear();
    }

    Vector3 Hand(bool right)
    {
        if (player.hasLaunchers)
        {
            return player.Launcher(right).position;
        }
        return player.transform.position;
    }

    // How fast the hands are moving, the two averaged (smoothed)
    float HandSpeed()
    {
        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        Vector3 left = Hand(false), right = Hand(true);
        float l = Mathf.Min((left - lastLeft).magnitude / dt, 8f);
        float r = Mathf.Min((right - lastRight).magnitude / dt, 8f);
        CountSwing(ref leftSwing, ref leftVelocity, (left - lastLeft) / dt, dt);
        CountSwing(ref rightSwing, ref rightVelocity, (right - lastRight) / dt, dt);
        lastLeft = left;
        lastRight = right;
        leftSpeed = Mathf.Lerp(leftSpeed, l, Mathf.Clamp01(dt * 10f));
        rightSpeed = Mathf.Lerp(rightSpeed, r, Mathf.Clamp01(dt * 10f));
        return (leftSpeed + rightSpeed) * 0.5f;
    }

    // A hand moving fast one way, then fast back the other, is one swing of a wave (swinging round to aim is just the one
    // way). `swing` is the way it last moved fast; `velocity` is smoothed so a jittery controller doesn't count.
    Vector3 leftSwing, rightSwing, leftVelocity, rightVelocity;
    readonly List<float> swings = new List<float>();

    void CountSwing(ref Vector3 swing, ref Vector3 velocity, Vector3 now, float dt)
    {
        velocity = Vector3.Lerp(velocity, Vector3.ClampMagnitude(now, 8f), Mathf.Clamp01(dt * 15f));
        if (velocity.magnitude < settings.waveSpeed * 0.5f)
        {
            return;
        }
        Vector3 way = velocity.normalized;
        if (swing != Vector3.zero && Vector3.Dot(way, swing) < -0.3f)
        {
            swings.Add(Time.time);
        }
        swing = way;
    }

    // Swings (either hand) in the last second
    int SwingsLately()
    {
        swings.RemoveAll(at => Time.time - at > 1f);
        return swings.Count;
    }

    // ---------- Looks ----------

    void SetSpying(bool on)
    {
        foreach (Animator animator in cat.GetComponentsInChildren<Animator>())
        {
            if (animator.runtimeAnimatorController == null)
            {
                continue;
            }
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == Spying)
                {
                    animator.SetBool(Spying, on);
                }
            }
        }
    }

    static Transform FindRopePoint(Transform under)
    {
        foreach (Transform child in under.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "Rope Point")
            {
                return child;
            }
        }
        return null;
    }

    // From its feet to the top of its back (where the rope's tied)
    float BackHeight()
    {
        float top = 0.3f;
        foreach (Renderer rend in cat.GetComponentsInChildren<Renderer>())
        {
            if (rend is ParticleSystemRenderer || rend is LineRenderer || rend is TrailRenderer || !rend.enabled)
            {
                continue;
            }
            if (rend.GetComponentInParent<Canvas>() != null)
            {
                continue;
            }
            top = Mathf.Max(top, rend.bounds.max.y - cat.transform.position.y);
        }
        return Mathf.Clamp(top * 0.8f, 0.2f, 1f);
    }

    void MakeRope()
    {
        LineRenderer template = CafePrefabs.RopeTemplate;
        if (template != null)
        {
            // Styled in the editor; its ends set here
            rope = Instantiate(template);
            rope.gameObject.SetActive(true);
            rope.name = "Spy Rope";
            rope.useWorldSpace = true;
            rope.positionCount = 2;
            UpdateRope();
            return;
        }

        GameObject holder = new GameObject("Spy Rope");
        rope = holder.AddComponent<LineRenderer>();
        rope.sharedMaterial = SpaceCafeVisuals.UnlitMaterial(Color.white);
        rope.startColor = rope.endColor = settings.ropeColour;
        rope.widthMultiplier = 0.015f;
        rope.positionCount = 2;
        rope.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rope.receiveShadows = false;
        UpdateRope();
    }

    void UpdateRope()
    {
        if (rope == null)
        {
            return;
        }
        rope.SetPosition(0, anchor + Vector3.up * 0.2f);
        rope.SetPosition(1, ropePoint != null ? ropePoint.position : cat.transform.position + Vector3.up * backHeight);
    }

    void ShowRing(Vector3 at, Transform eye, float fill)
    {
        if (ring == null)
        {
            Transform holder = new GameObject("Spy Nerve Ring").transform;
            ring = SpaceCafeVisuals.Ring(holder, "Ring", 0.5f, 0.035f, settings.ringColour);
        }
        Transform h = ring.transform.parent;
        h.position = at;
        if (eye != null)
        {
            h.rotation = Quaternion.LookRotation(at - eye.position);
        }
        bool show = fill > 0.01f;
        if (h.gameObject.activeSelf != show)
        {
            h.gameObject.SetActive(show);
        }
        if (!show)
        {
            return;
        }
        SpaceCafeVisuals.SetRing(ring, 0.5f, fill);
        Color colour = Color.Lerp(settings.ringColour, new Color(0.4f, 1f, 0.5f), fill);
        ring.startColor = ring.endColor = colour;
    }

    void HideRing()
    {
        if (ring != null)
        {
            ring.transform.parent.gameObject.SetActive(false);
        }
    }

    // Hangs the cat on the rope, `swing` radians out from straight down, swinging in the plane towards the player
    void Hang(Vector3 top, float length, float swing, Vector3 toward)
    {
        Vector3 clip = top + (toward * Mathf.Sin(swing) - Vector3.up * Mathf.Cos(swing)) * length;
        // The rope pulls straight up from its back; it faces the player, with a little twist on the rope
        Vector3 up = (top - clip).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(toward, up);
        Quaternion rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0.001f ? forward : toward, up)
                              * Quaternion.Euler(0, Mathf.Sin(Time.time * 0.8f) * 6f, 0);
        cat.transform.rotation = rotation;
        Vector3 offset;
        if (ropePoint != null)
        {
            offset = ropePoint.position - cat.transform.position;
        }
        else
        {
            offset = rotation * Vector3.up * backHeight;
        }
        cat.transform.position = clip - offset;
        UpdateRope();
    }

    // The recipe it snatched, held out in front of it as it makes off
    void MakeRecipeInPaws()
    {
        float catScale = Mathf.Max(0.01f, cat.transform.lossyScale.x);
        GameObject prefab = CafePrefabs.RecipePrefab;
        if (prefab != null)
        {
            // Made in the editor; held out in front, sized as authored whatever size the cat is
            recipe = Instantiate(prefab, cat.transform, false);
            recipe.name = "Secret Recipe";
            recipe.SetActive(true);
            recipe.transform.position = cat.transform.position + cat.transform.forward * 0.35f + cat.transform.up * 0.15f;
            recipe.transform.rotation = cat.transform.rotation * Quaternion.Euler(-70f, 0, 0);
            recipe.transform.localScale = prefab.transform.localScale / catScale;
            return;
        }

        recipe = new GameObject("Secret Recipe");
        recipe.transform.SetParent(cat.transform, false);
        recipe.transform.position = cat.transform.position + cat.transform.forward * 0.35f + cat.transform.up * 0.15f;
        recipe.transform.rotation = cat.transform.rotation * Quaternion.Euler(-70f, 0, 0);
        GameObject card = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(card.GetComponent<Collider>());
        card.transform.SetParent(recipe.transform, false);
        card.transform.localScale = new Vector3(0.22f, 0.3f, 0.01f) * (1f / Mathf.Max(0.01f, cat.transform.lossyScale.x));
        card.GetComponent<Renderer>().sharedMaterial = SpaceCafeVisuals.UnlitMaterial(new Color(1f, 0.95f, 0.8f));

        GameObject words = new GameObject("Recipe Words");
        words.transform.SetParent(recipe.transform, false);
        words.transform.localPosition = new Vector3(0, 0, -0.008f / Mathf.Max(0.01f, cat.transform.lossyScale.x));
        words.transform.localScale = Vector3.one * (1f / Mathf.Max(0.01f, cat.transform.lossyScale.x));
        TextMeshPro text = words.AddComponent<TextMeshPro>();
        text.text = "SECRET\nRECIPE";
        text.fontSize = 0.6f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.75f, 0.1f, 0.1f);
        text.rectTransform.sizeDelta = new Vector2(0.2f, 0.25f);
    }

    static AudioClip sting;
    void Sting()
    {
        if (sting == null)
        {
            sting = Resources.Load<AudioClip>("CafeSounds/spy_sting");
        }
        if (sting == null)
        {
            return;
        }
        AudioSource.PlayClipAtPoint(sting, vent.opening, 0.8f);
    }
}
