using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// Cat Cafe Waves: a cheeky kitten bolts across the room at the player and leaps onto one of their launchers.
// There's no stopping it (treats go straight through it: it's a gag everyone should get). Once it's on, that
// launcher can't fire: the player shakes it off with a few quick shakes of that hand (pulling the blocked trigger
// loosens it a little too), or it gets bored and hops off by itself after a while. The kitten itself is a KittenRun
// (a pooled cat, shrunk).
public class LauncherKitten : MonoBehaviour
{
    [Serializable]
    public class Settings
    {
        [Min(0.5f)] public float runSpeed = 2.6f;
        [Range(0.2f, 1f)]
        [Tooltip("Its size while it runs in, compared with a normal cat.")]
        public float runScale = 0.5f;
        [Range(0.1f, 1f)]
        [Tooltip("Its size once it's on the launcher, compared with a normal cat (it shrinks as it leaps).")]
        public float latchScale = 0.3f;
        [Min(0.5f)]
        [Tooltip("How close it gets to a launcher (along the floor, in metres) before it leaps.")]
        public float leapDistance = 1.6f;
        [Min(0.1f)] public float leapSeconds = 0.45f;
        [Tooltip("Where it clings, from the launcher's muzzle (x right, y up, z forward), in metres. Can be tuned in Play mode.")]
        public Vector3 latchOffset = new Vector3(0, 0.03f, 0.02f);
        [Min(1)]
        [Tooltip("Quick shakes (changes of direction) of that hand it takes to throw it off.")]
        public int shakesToFree = 3;
        [Min(0.2f)]
        [Tooltip("How fast the hand has to be moving (m/s) for a change of direction to count as a shake.")]
        public float shakeSpeed = 1.1f;
        [Range(0f, 1f)]
        [Tooltip("How much each pull of the blocked trigger loosens its grip (1 = all of it).")]
        public float triggerLoosens = 0.12f;
        [Min(1f)]
        [Tooltip("Seconds on the launcher before it gets bored and hops off by itself.")]
        public float boredAfter = 8f;
        [Range(0f, 1f)]
        [Tooltip("The chance a kitten goes for a launcher that already has a kitten on it, and climbs on top of that one.")]
        public float stackChance = 0.35f;
        [Min(0f)]
        [Tooltip("How far up a stacked kitten sits on the one below, in metres.")]
        public float stackHeight = 0.08f;
        public string hint = "SHAKE!";
        public Color hintColour = new Color(1f, 0.85f, 0.2f);
        public Color warningColour = new Color(1f, 0.55f, 0.1f);
        public string shooText = "Shoo!";
    }

    public enum Result
    {
        ShakenOff,
        GotBored,
        Cancelled   // the phase or the round ended
    }

    // Every launcher kitten out (running in, leaping or clinging on)
    public static readonly List<LauncherKitten> active = new List<LauncherKitten>();

    KittenRun run;
    CatMovement cat;
    PlayerInput player;
    Settings settings;
    Action<LauncherKitten> onLatched;
    Action<LauncherKitten, Result> done;
    bool finished;
    bool scrabbling;

    bool latched;
    bool rightHand;
    int stack;          // kittens already on that launcher when it got there (it sits on top of them)
    Transform launcher;
    Transform muzzle;
    float grip;
    float lastShake = -10f;
    float lastFast = -10f;
    float latchedAt;
    float scrabbleUntil;
    float nextPurr;
    float nextMeow;
    Vector3 lastHandPosition;
    Vector3 handVelocity;
    Vector3 lastShakeDirection;
    Vector2 tilt;
    GameObject hint;
    TMPro.TMP_Text hintText;
    GameObject warning;
    float startedAt;
    readonly List<Collider> switchedOff = new List<Collider>();

    public bool isLatched
    {
        get
        {
            return latched;
        }
    }
    public bool onRightHand
    {
        get
        {
            return rightHand;
        }
    }

    // Sends a pooled cat (switched off) running along `path` (a door to just in front of the player) to leap at them
    public static LauncherKitten Send(CatMovement cat, List<Vector3> path, PlayerInput player, Settings settings,
                                      Action<LauncherKitten> onLatched, Action<LauncherKitten, Result> done)
    {
        KittenRun run = KittenRun.Launch(cat, path, 0, settings.runSpeed, settings.runScale, 0f, null);
        run.holdAtEnd = true;
        LauncherKitten kitten = cat.GetComponent<LauncherKitten>();
        if (kitten == null) kitten = cat.gameObject.AddComponent<LauncherKitten>();
        kitten.run = run;
        kitten.cat = cat;
        kitten.player = player;
        kitten.settings = settings;
        kitten.onLatched = onLatched;
        kitten.done = done;
        kitten.finished = false;
        kitten.latched = false;
        kitten.launcher = null;
        kitten.startedAt = Time.time;
        // Nothing hits it (treats fly straight through, so it can't soak up shots meant for the cats behind it)
        kitten.switchedOff.Clear();
        foreach (Collider body in cat.GetComponentsInChildren<Collider>())
        {
            if (!body.enabled) continue;
            body.enabled = false;
            kitten.switchedOff.Add(body);
        }
        kitten.enabled = true;
        kitten.ShowWarning();
        run.TryMeow(1.45f, 1.85f);
        return kitten;
    }

    // Pulling the trigger of a launcher with a kitten on: nothing comes out, and the kitten's grip slips a little.
    // PlayerInput asks this before every shot.
    public static bool BlocksFire(bool rightHand)
    {
        if (IsKittenLatched(rightHand, out LauncherKitten kitten))
        {
            kitten.BlockedShot();
            return true;
        }
        return false;
    }

    public static bool IsKittenLatched(bool rightHand)
    {
        return IsKittenLatched(rightHand, out _);
    }

    public static bool IsKittenLatched(bool rightHand, out LauncherKitten latched)
    {
        foreach (LauncherKitten kitten in active)
        {
            if (kitten.latched && kitten.rightHand == rightHand)
            {
                latched = kitten;
                return true;
            }
        }
        latched = null;
        return false;
    }

    // When the phase or round is over: every launcher kitten still out hops off
    public static void EndAll()
    {
        foreach (LauncherKitten kitten in active.ToArray()) kitten.Finish(Result.Cancelled, true);
    }

    void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
    }

    void OnDisable()
    {
        // The cat went back to the pool some other way (e.g. the round ended)
        if (!finished) Finish(Result.Cancelled, false);
        active.Remove(this);
    }

    void Update()
    {
        if (finished) return;
        if (run == null || !run.enabled)
        {
            Finish(Result.Cancelled, false);
            return;
        }
        if (warning != null) UpdateWarning();
        if (latched || launcher != null) return;

        // Running in: leaps at whichever launcher it gets near first (or the nearer one once it's as close as it gets)
        bool armed = player != null && player.hasLaunchers;
        float left;
        if (armed)
        {
            left = FlatDistance(player.Launcher(false).position);
        }
        else
        {
            left = float.MaxValue;
        }
        float right;
        if (armed)
        {
            right = FlatDistance(player.Launcher(true).position);
        }
        else
        {
            right = float.MaxValue;
        }
        if (armed && (Mathf.Min(left, right) <= settings.leapDistance || run.held))
        {
            // The nearer launcher, unless another kitten has already gone for that one
            bool toRight = right < left;
            if (Claimed(toRight) && !Claimed(!toRight) && Random.value >= settings.stackChance) toRight = !toRight;
            StartCoroutine(Leap(toRight));
        }
        else if (Time.time - startedAt > 12f)
        {
            // Couldn't get there (blocked somewhere): gives up
            Finish(Result.Cancelled, true);
        }
    }

    bool Claimed(bool hand)
    {
        foreach (LauncherKitten other in active)
        {
            if (other != this && !other.finished && other.launcher != null && other.rightHand == hand) return true;
        }
        return false;
    }

    float FlatDistance(Vector3 to)
    {
        Vector3 between = to - transform.position;
        between.y = 0;
        return between.magnitude;
    }

    // Where it clings on the launcher, and which way it faces there (looking back at the player)
    Vector3 LatchPosition(float slip)
    {
        Vector3 offset = settings.latchOffset + new Vector3(0, settings.stackHeight * stack, slip);
        return muzzle.position + muzzle.rotation * offset;
    }

    Quaternion LatchRotation()
    {
        return Quaternion.LookRotation(-muzzle.forward, muzzle.up);
    }

    IEnumerator Leap(bool right)
    {
        rightHand = right;
        launcher = player.Launcher(right);
        stack = 0;
        foreach (LauncherKitten other in active)
        {
            if (other != this && !other.finished && other.launcher != null && other.rightHand == right) stack++;
        }
        muzzle = player.LaunchPoint(right);
        if (muzzle == null) muzzle = launcher;
        run.Hold();
        HideWarning();
        run.SetLegs(3f);
        run.TryMeow(1.8f, 2.1f);

        Vector3 start = transform.position;
        Quaternion startRotation = transform.rotation;
        Vector3 startScale = transform.localScale;
        Vector3 endScale = startScale * (settings.latchScale / Mathf.Max(0.05f, settings.runScale));
        float t = 0;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / settings.leapSeconds);
            // Homes in on the launcher wherever the hand goes
            Vector3 target = LatchPosition(0);
            float eased = 1f - (1f - t) * (1f - t);
            Vector3 position = Vector3.Lerp(start, target, eased) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.35f;
            transform.position = position;
            transform.rotation = Quaternion.Slerp(startRotation, LatchRotation(), eased);
            transform.localScale = Vector3.Lerp(startScale, endScale, eased);
            yield return null;
            if (finished) yield break;
        }

        // On! That launcher's out of action until it's shaken off
        transform.SetParent(launcher, true);
        latched = true;
        latchedAt = Time.time;
        grip = 1f;
        tilt = Vector2.zero;
        lastHandPosition = launcher.position;
        handVelocity = Vector3.zero;
        lastShakeDirection = Vector3.zero;
        nextPurr = Time.time + 0.6f;
        nextMeow = Time.time + Random.Range(1f, 1.8f);
        run.SetLegs(0f);
        Haptics.Pulse(rightHand, Haptics.Strength.Medium);
        hint = FloatingWord.Make(settings.hint, settings.hintColour, 0.9f);
        hintText = hint.GetComponent<TMPro.TMP_Text>();
        scrabbling = false;
        player.ChangeFaceExpression(Expression.Scared, rightHand);
        onLatched?.Invoke(this);
    }

    void LateUpdate()
    {
        if (!latched || finished) return;
        float dt = Time.deltaTime;
        if (dt <= 0) return;

        // How the hand's moving
        Vector3 hand = launcher.position;
        Vector3 velocity = (hand - lastHandPosition) / dt;
        lastHandPosition = hand;
        if (velocity.magnitude > 20f) velocity = Vector3.zero;   // tracking jump, not a shake
        handVelocity = Vector3.Lerp(handVelocity, velocity, Mathf.Clamp01(dt * 25f));

        // A shake is the hand changing direction while it's moving fast
        float speed = handVelocity.magnitude;
        if (speed > settings.shakeSpeed)
        {
            lastFast = Time.time;
            Vector3 direction = handVelocity / speed;
            if (lastShakeDirection == Vector3.zero)
            {
                lastShakeDirection = direction;
            }
            else if (Vector3.Dot(direction, lastShakeDirection) < -0.2f)
            {
                lastShakeDirection = direction;
                Loosen(1f / settings.shakesToFree);
            }
        }
        else if (Time.time - lastFast > 0.4f)
        {
            lastShakeDirection = Vector3.zero;   // the hand's been still a moment: the next fast move starts afresh
        }
        // Settles back in when the shaking stops
        if (Time.time - lastShake > 0.6f) grip = Mathf.MoveTowards(grip, 1f, dt * 0.2f);

        if (grip <= 0f)
        {
            StartCoroutine(Fly(handVelocity));
            return;
        }
        if (Time.time - latchedAt > settings.boredAfter)
        {
            StartCoroutine(HopOff());
            return;
        }

        // Clings on, swaying against the hand's movement and slipping towards the end as its grip goes
        Vector3 local = Quaternion.Inverse(muzzle.rotation) * handVelocity;
        Vector2 sway = new Vector2(Mathf.Clamp(local.z * 8f, -30f, 30f), Mathf.Clamp(-local.x * 8f, -30f, 30f));
        tilt = Vector2.Lerp(tilt, sway, Mathf.Clamp01(dt * 10f));
        float loose = 1f - grip;
        float wobble = loose * 12f * Mathf.Sin(Time.time * 30f);
        transform.position = LatchPosition(loose * 0.05f);
        transform.rotation = LatchRotation() * Quaternion.Euler(tilt.x + loose * 20f, 0, tilt.y + wobble);

        // Scrabbles when it's being shaken, otherwise sits there looking pleased with itself (and purring)
        bool scrabble = Time.time < scrabbleUntil;
        if (scrabble != scrabbling)
        {
            scrabbling = scrabble;
            run.SetLegs(scrabble ? 4f : 0f);
        }
        if (Time.time >= nextPurr)
        {
            Haptics.Pulse(rightHand, 0.12f, 0.1f);
            nextPurr = Time.time + 0.7f;
        }
        if (Time.time >= nextMeow)
        {
            run.TryMeow(1.45f, 1.85f);
            nextMeow = Time.time + Random.Range(1.4f, 2.4f);
        }

        if (hint != null)
        {
            hint.transform.position = transform.position + Vector3.up * (0.14f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.03f);
            FloatingWord.FacePlayer(hint.transform);
            // Throbs and flashes, so it reads as urgent (faster the longer it's been on)
            float beat = Mathf.Lerp(7f, 11f, Mathf.Clamp01((Time.time - latchedAt) / 4f));
            float pulse = Mathf.Abs(Mathf.Sin((Time.time - latchedAt) * beat));
            hint.transform.localScale = Vector3.one * (1f + 0.35f * pulse);
            if (hintText != null) hintText.color = Color.Lerp(settings.hintColour, new Color(1f, 0.2f, 0.15f), pulse);
        }
    }

    void Loosen(float amount)
    {
        grip -= amount;
        lastShake = Time.time;
        scrabbleUntil = Time.time + 0.35f;
        Haptics.Pulse(rightHand, Haptics.Strength.Light);
        if (grip > 0) run.TryMeow(1.6f, 2f);
    }

    void BlockedShot()
    {
        Loosen(settings.triggerLoosens);
    }

    // Shaken off: it goes flying the way the hand was going, tumbling, and lands in a puff of dust
    IEnumerator Fly(Vector3 throwVelocity)
    {
        Let();
        FloatingWord.Show(settings.shooText, transform.position + Vector3.up * 0.1f, settings.hintColour, 1.2f);
        Haptics.Pulse(rightHand, Haptics.Strength.Strong);
        run.TryMeow(1.2f, 1.4f);   // an indignant mrrow
        run.SetLegs(4f);

        // Flung the way the hand was going (off the end of the launcher if the hand was still)
        Vector3 direction;
        if (throwVelocity.sqrMagnitude > 0.04f)
        {
            direction = throwVelocity.normalized;
        }
        else
        {
            direction = muzzle.forward;
        }
        Vector3 velocity = direction * Mathf.Clamp(throwVelocity.magnitude * 1.5f, 2.5f, 6f) + Vector3.up * 1.5f;
        Vector3 spinAxis = Vector3.Cross(Vector3.up, velocity.sqrMagnitude > 0.01f ? velocity : transform.forward);
        if (spinAxis.sqrMagnitude < 0.001f) spinAxis = transform.right;
        float floorY;
        if (player != null)
        {
            floorY = player.transform.position.y;
        }
        else
        {
            floorY = transform.position.y - 1f;
        }
        float timer = 0;
        while (timer < 1.2f)
        {
            timer += Time.deltaTime;
            velocity += Physics.gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.Rotate(spinAxis, 720f * Time.deltaTime, Space.World);
            if (transform.position.y <= floorY && velocity.y < 0) break;
            yield return null;
        }
        Finish(Result.ShakenOff, true);
    }

    // Got bored: a little hop off the launcher and away
    IEnumerator HopOff()
    {
        Vector3 away = muzzle.forward;   // off the end of the launcher, away from the player
        Let();
        run.TryMeow(1.5f, 1.8f);
        Vector3 start = transform.position;
        away.y = 0;
        Vector3 end = start + away.normalized * 0.4f + Vector3.down * 0.5f;
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.35f;
            transform.position = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(Mathf.Min(t, 1f) * Mathf.PI) * 0.2f;
            yield return null;
        }
        Finish(Result.GotBored, true);
    }

    // Lets go of the launcher (it can fire again)
    void Let()
    {
        player.ChangeFaceExpression(Expression.Happy, rightHand);
        latched = false;
        if (hint != null) Destroy(hint);
        transform.SetParent(null, true);
    }

    void Finish(Result result, bool dismiss)
    {
        if (finished) return;
        player.ChangeFaceExpression(Expression.Default, rightHand);
        finished = true;
        latched = false;
        StopAllCoroutines();
        HideWarning();
        if (hint != null) Destroy(hint);
        foreach (Collider body in switchedOff)
        {
            if (body != null) body.enabled = true;
        }
        switchedOff.Clear();
        if (dismiss && run != null && run.enabled) run.Dismiss();
        enabled = false;
        done?.Invoke(this, result);
    }

    // ---------- The "!" as it bursts in ----------

    void ShowWarning()
    {
        if (warning == null) warning = FloatingWord.Make("!", settings.warningColour, 4f);
        warning.SetActive(true);
    }

    void UpdateWarning()
    {
        float age = Time.time - startedAt;
        if (age > 1.2f)
        {
            HideWarning();
            return;
        }
        warning.transform.position = transform.position + Vector3.up * (0.45f + Mathf.Abs(Mathf.Sin(age * 9f)) * 0.06f);
        FloatingWord.FacePlayer(warning.transform);
    }

    void HideWarning()
    {
        if (warning != null) warning.SetActive(false);
    }

    void OnDestroy()
    {
        if (warning != null) Destroy(warning);
        if (hint != null) Destroy(hint);
    }
}
