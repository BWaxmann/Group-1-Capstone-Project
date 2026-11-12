using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Cat Cafe Waves: the Mafia cat's heist. He swaggers in through a door and strolls straight for the tip jar, while a red
// ring pulses round the jar (faster as he gets closer). Feed him his whole order before he gets there (a wrong treat and
// he starts his order over) and he's busted. If he makes it, he hops up, scoops out a big handful of coins and swaggers
// off with them.
public class MafiaHeist : MonoBehaviour
{
    [Serializable]
    public class Settings
    {
        [Min(0.2f)]
        [Tooltip("His strolling speed, in metres a second (he's impatient: he wants that money now).")]
        public float walkSpeed = 1.6f;

        [Min(1)]
        [Tooltip("Treats in his order.")]
        public int treats = 3;

        [Min(0)]
        [Tooltip("Coins he gets out of the jar if he's left to pillage it the whole time.")]
        public int steal = 10;

        [Min(0.5f)]
        [Tooltip("Seconds he pillages the jar once he gets to it (he can still be busted all the while).")]
        public float pillageSeconds = 5f;

        [Min(0.2f)]
        [Tooltip("Seconds between his swats at the jar (a few coins spill each time).")]
        public float swatEvery = 0.8f;

        [Min(0)]
        public int bustedScore = 15;

        [Min(0)]
        [Tooltip("Coins into the jar for busting him.")]
        public int bustedCoins = 3;

        [Range(0f, 15f)]
        [Tooltip("How much he rocks side to side as he walks, in degrees.")]
        public float swagger = 7f;

        public string bustedText = "Busted!";

        public string heistText = "Heist!";

        public Color ringColour = new Color(1f, 0.15f, 0.1f);

        [Range(0f, 1f)]
        [Tooltip("How loud his theme (Resources/CafeSounds/mafia_theme) plays as he walks in. The music dips under it.")]
        public float themeVolume = 0.8f;

        [Range(0f, 1f)]
        [Tooltip("How far the music dips under his theme (1 = not at all).")]
        public float musicUnderTheme = 0.25f;
    }

    public enum Result { Busted, Stole, Cancelled }

    public static MafiaHeist current;

    CatMovement cat;
    CatOrder order;
    NavMeshAgent agent;
    Animator animator;
    TipJar jar;
    Settings settings;
    Action<MafiaHeist, Result> done;
    Vector3 jarSpot;
    LineRenderer ring;
    GameObject warning;
    Transform exitDoor;
    bool finished;
    float stagger;
    SoundBite theme;
    static AudioClip themeClip;

    // Brings a Mafia cat in at `door` (`position` on the floor there) to go after `jar`. Null if he can't get to it.
    public static MafiaHeist Begin(BossCat prefab, Transform parent, Transform door, Vector3 position, TipJar jar, List<CatOrder.Food> foods,
                                   float bubbleScale, float hitboxGenerosity, Settings settings, Action<MafiaHeist, Result> done)
    {
        if (prefab == null || jar == null)
        {
            return null;
        }
        BossCat boss = Instantiate(prefab, parent);
        boss.Initialise(door, position);
        boss.TakeOver();   // he isn't doing his usual boss things (bringing in his gang): he's here for the money

        MafiaHeist heist = boss.gameObject.AddComponent<MafiaHeist>();
        heist.cat = boss;
        heist.agent = boss.GetComponent<NavMeshAgent>();
        heist.animator = boss.GetComponentInChildren<Animator>();
        heist.jar = jar;
        heist.settings = settings;
        heist.done = done;
        heist.exitDoor = door;
        if (heist.agent == null || !heist.agent.isOnNavMesh || !CatRaid.FloorSpotNear(jar.transform.position, heist.agent, out heist.jarSpot))
        {
            Destroy(boss.gameObject);
            return null;
        }

        heist.order = CatOrder.Give(boss, foods, bubbleScale, hitboxGenerosity);
        heist.order.wrongTreat = heist.WrongTreat;
        heist.order.rightTreat += heist.RightTreat;
        heist.order.served = heist.Busted;
        current = heist;
        heist.StartCoroutine(heist.StrollRoutine());
        return heist;
    }

    IEnumerator StrollRoutine()
    {
        // A swagger: a slow walk with a big side-to-side rock
        CatWaddle waddle = GetComponent<CatWaddle>();
        if (waddle == null)
        {
            waddle = gameObject.AddComponent<CatWaddle>();
        }
        waddle.rockDegrees = settings.swagger;
        waddle.twistDegrees = settings.swagger * 0.5f;
        waddle.waddlesPerMetre = 1.1f;
        waddle.fullSpeed = settings.walkSpeed;

        PlayTheme();
        ShowRing(true);
        warning = FloatingWord.Make("!", settings.ringColour, 5f);
        cat.Meow(0.8f);   // a low, knowing mrrow
        agent.speed = settings.walkSpeed;
        agent.isStopped = false;
        agent.SetDestination(jarSpot);

        float timer = 0;
        float startDistance = Mathf.Max(0.5f, Flat(jarSpot - transform.position).magnitude);
        while (!finished)
        {
            timer += Time.deltaTime;
            float left = Flat(jarSpot - transform.position).magnitude;
            if (!agent.pathPending && left < 0.5f)
            {
                break;
            }
            if (timer > 30f)
            {
                // Couldn't get there: off he goes
                Finish(Result.Cancelled);
                yield break;
            }

            // Each right treat staggers him for a moment
            stagger = Mathf.MoveTowards(stagger, 0, Time.deltaTime);
            agent.speed = settings.walkSpeed * (stagger > 0 ? 0.15f : 1f);

            UpdateRing(1f - Mathf.Clamp01(left / startDistance));
            if (warning != null)
            {
                warning.SetActive(timer < 2f);
                warning.transform.position = order.aboveBubble + Vector3.up * (0.15f + Mathf.Abs(Mathf.Sin(timer * 9f)) * 0.06f);
                FloatingWord.FacePlayer(warning.transform);
            }
            yield return null;
        }
        if (finished)
        {
            yield break;
        }

        // Made it: up to the jar, and he pillages it for a few seconds (still bustable all the while), then off with the coins
        agent.isStopped = true;
        agent.enabled = false;
        Vector3 from = transform.position;
        Vector3 towards = Flat(jar.transform.position - from);
        float reach = towards.magnitude;
        if (reach > 0.001f)
        {
            towards = towards / reach;
        }
        else
        {
            towards = transform.forward;
        }
        transform.rotation = Quaternion.LookRotation(towards);
        bool upOnTop = jar.transform.position.y - from.y > 0.35f;
        Vector3 perch;
        if (upOnTop)
        {
            perch = jar.transform.position - towards * 0.25f;
        }
        else
        {
            perch = from + towards * Mathf.Max(0, reach - 0.3f);
        }
        yield return HopRoutine(from, perch, upOnTop ? 0.45f : 0.25f, upOnTop ? 0.45f : 0.15f);

        // Swat after swat at the jar, a few coins spilling each time (a right treat puts him off his swing for a moment)
        if (warning != null)
        {
            warning.SetActive(true);
        }
        int swats = Mathf.Max(1, Mathf.RoundToInt(settings.pillageSeconds / settings.swatEvery));
        int stolen = 0;
        float pillage = 0;
        for (int swat = 0; swat < swats && !finished; swat++)
        {
            float wait = settings.swatEvery;
            while (wait > 0 && !finished)
            {
                wait -= Time.deltaTime;
                pillage += Time.deltaTime;
                if (stagger > 0)
                {
                    wait += Time.deltaTime;   // put off his swing
                    stagger = Mathf.MoveTowards(stagger, 0, Time.deltaTime);
                }
                UpdateRing(1f);
                if (warning != null)
                {
                    warning.transform.position = order.aboveBubble + Vector3.up * (0.15f + Mathf.Abs(Mathf.Sin(pillage * 12f)) * 0.06f);
                    FloatingWord.FacePlayer(warning.transform);
                }
                yield return null;
            }
            if (finished)
            {
                yield break;
            }
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetTrigger("AngryCat");
            }
            int share = Mathf.CeilToInt((settings.steal - stolen) / (float)(swats - swat));
            stolen += jar.Spill(transform.position, share);
            Haptics.PulseBoth(Haptics.Strength.Light);
            if (swat % 2 == 0)
            {
                cat.Meow(Random.Range(0.72f, 0.82f));
            }
        }
        if (finished)
        {
            yield break;
        }

        order.busy = true;   // too late now
        if (warning != null)
        {
            warning.SetActive(false);
        }
        FloatingWord.Show(stolen > 0 ? $"{settings.heistText} -{stolen}" : settings.heistText, jar.top + Vector3.up * 0.25f, settings.ringColour, 4f, 1.6f);
        Haptics.PulseBoth(Haptics.Strength.Strong);
        cat.Meow(0.75f);
        ShowRing(false);
        yield return new WaitForSeconds(0.5f);
        if (upOnTop)
        {
            yield return HopRoutine(transform.position, from, 0.4f, 0.3f);
        }

        // Swaggers off back the way he came, then he's gone
        finished = true;
        if (current == this)
        {
            current = null;
        }
        if (done != null)
        {
            done(this, Result.Stole);
        }
        done = null;
        yield return WalkOffRoutine();
    }

    IEnumerator WalkOffRoutine()
    {
        ShowRing(false);
        if (warning != null)
        {
            warning.SetActive(false);
        }
        if (order != null && order.isActiveAndEnabled)
        {
            order.busy = true;
        }
        cat.treatGate = t => false;
        cat.wantsTreats = () => false;
        if (!agent.enabled)
        {
            agent.enabled = true;
        }
        if (agent.isOnNavMesh && exitDoor != null && NavMesh.SamplePosition(exitDoor.position, out NavMeshHit door, 1.5f, NavMesh.AllAreas))
        {
            agent.speed = settings.walkSpeed * 1.3f;
            agent.isStopped = false;
            agent.SetDestination(door.position);
            float timer = 0;
            while (timer < 6f && (agent.pathPending || agent.remainingDistance > 0.4f))
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }
        Vanish();
    }

    void RightTreat(CatOrder fed)
    {
        if (finished || !fed.isHungry)
        {
            return;
        }
        stagger = 0.5f;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetTrigger("AngryCat");
        }
    }

    // A wrong treat: he shakes it off and starts his order over
    void WrongTreat(CatOrder fed)
    {
        if (finished)
        {
            return;
        }
        fed.Restart();
        cat.Meow(0.7f);
    }

    // His whole order: busted
    void Busted(CatOrder fed)
    {
        if (finished)
        {
            return;
        }
        StopAllCoroutines();
        Vector3 at = fed.aboveBubble;
        FloatingWord.Show(settings.bustedText, at, new Color(0.4f, 1f, 0.5f), 4f, 1.4f);
        if (settings.bustedScore > 0)
        {
            GameManager.instance.AddBonus(settings.bustedScore, at);
        }
        if (settings.bustedCoins > 0)
        {
            jar.AddCoins(at, settings.bustedCoins);
        }
        Haptics.PulseBoth(Haptics.Strength.Medium);
        Finish(Result.Busted);
    }

    void Finish(Result result)
    {
        if (finished)
        {
            return;
        }
        finished = true;
        StopTheme();
        ShowRing(false);
        if (warning != null)
        {
            Destroy(warning);
        }
        if (done != null)
        {
            done(this, result);
        }
        done = null;
        if (current == this)
        {
            current = null;
        }
        StartCoroutine(result == Result.Busted ? PoofSoonRoutine() : WalkOffRoutine());
    }

    IEnumerator PoofSoonRoutine()
    {
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
        cat.treatGate = t => false;
        cat.wantsTreats = () => false;
        yield return new WaitForSeconds(0.6f);
        cat.PopConfetti();
        Vanish();
    }

    // Gone in a puff (and not back to any pool: he was only borrowed)
    void Vanish()
    {
        cat.DustPuff(1f);
        if (current == this)
        {
            current = null;
        }
        Destroy(cat.gameObject);
    }

    // The round or phase is over: he leaves
    public void Cancel()
    {
        if (!finished)
        {
            Finish(Result.Cancelled);
        }
    }

    void OnDestroy()
    {
        if (ring != null)
        {
            Destroy(ring.transform.parent.gameObject);
        }
        if (warning != null)
        {
            Destroy(warning);
        }
        if (current == this)
        {
            current = null;
        }
    }

    // His theme, from the speakers (not from him), with the music dipped underneath
    void PlayTheme()
    {
        if (themeClip == null)
        {
            themeClip = CafeResources.Load<AudioClip>("CafeSounds/mafia_theme");
        }
        theme = SoundBite.Play("Mafia Theme", themeClip, settings.themeVolume, settings.musicUnderTheme, null, fadeIn: 0.1f, fadeOut: 1.5f);
    }

    // Busted (or sent off): his theme eases out and the music comes back
    void StopTheme()
    {
        if (theme != null)
        {
            theme.Stop(1.2f);
        }
        theme = null;
    }

    IEnumerator HopRoutine(Vector3 from, Vector3 to, float seconds, float height)
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

    // A red ring round the jar's base: what he's after, pulsing faster as he closes in
    void ShowRing(bool show)
    {
        if (show && ring == null)
        {
            Transform holder = new GameObject("Heist Ring").transform;
            holder.position = jar.transform.position + Vector3.up * 0.01f;
            holder.rotation = Quaternion.Euler(90f, 0, 0);
            ring = SpaceCafeVisuals.Ring(holder, "Ring", 0.18f * Mathf.Max(0.5f, jar.transform.lossyScale.x), 0.03f, settings.ringColour);
        }
        if (ring != null && ring.transform.parent.gameObject.activeSelf != show)
        {
            ring.transform.parent.gameObject.SetActive(show);
        }
    }

    void UpdateRing(float closeness)
    {
        if (ring == null)
        {
            return;
        }
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(4f, 16f, closeness));
        Color colour = settings.ringColour;
        colour.a = Mathf.Lerp(0.4f, 1f, pulse);
        ring.startColor = ring.endColor = colour;
        ring.widthMultiplier = Mathf.Lerp(0.02f, 0.06f, closeness);
    }

    static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0, v.z);
    }
}
