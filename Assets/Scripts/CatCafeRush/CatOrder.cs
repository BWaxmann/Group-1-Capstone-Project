using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Cat Cafe Rush: a Feeding Frenzy style colour order on one of Brian's roaming cats.
// Its bubble shows the treat it wants now, with the rest of the order queued after. The right colour moves the order
// on (the last one fills the cat up and it happy-launches); the wrong colour gets a shake and an angry face.
// A cat can also be told to run off (when a catch phase starts), which sends it out of the room without being fed.
public class CatOrder : MonoBehaviour
{
    public struct Food
    {
        public int variation;           // position in the treat pool, as fired by PlayerInput
        public OrderBubble.Item icon;   // what the bubble shows
    }

    CatMovement cat;
    NavMeshAgent agent;
    OrderBubble bubble;
    readonly List<Food> order = new List<Food>();
    int step;
    bool runningOff;
    float normalSpeed;
    bool normalUpdateRotation;

    public bool isHungry => cat != null && cat.isActiveAndEnabled && !cat.isFullyFed && !runningOff && step < order.Count;

    // The treat this cat wants right now (its position in the treat pool), or -1
    public int wantedFood => isHungry ? order[step].variation : -1;

    // The middle of the cat's body, for the aim assist to steer towards
    public Vector3 aimPoint => hitbox != null ? hitbox.transform.TransformPoint(hitbox.center) : transform.position + Vector3.up * 0.2f;

    // Every cat with an order, for the aim assist to look through
    public static readonly List<CatOrder> all = new List<CatOrder>();

    SphereCollider hitbox;
    int lastShotEaten;

    // Called when the cat gets the last treat of its order (Cat Cafe Waves pays out tips)
    public System.Action<CatOrder> served;

    // If set, a served cat runs to the hole this gives (from where it is) and dives in, instead of happy-launching
    public System.Func<Vector3, Vector3?> holeFor;

    // How many treats the order was
    public int orderLength => order.Count;

    // Called for every right treat the cat eats (including the last), before the cat reacts to it
    public event System.Action<CatOrder> rightTreat;

    // While true the cat ignores treats (e.g. mid-leap in a raid: too late to stop it)
    public bool busy { get; set; }

    // Just above the order bubble
    public Vector3 aboveBubble => bubble != null
        ? bubble.transform.position + Vector3.up * (0.15f + 0.26f * bubble.transform.lossyScale.y)
        : transform.position + Vector3.up * 1f;

    float baseSpeed = -1f;

    Vector3 scaleBeforeDive;
    bool diving;

    // A generous, invisible hit area around a cat's body, so a treat that looks like a hit counts as one
    // (some of the cats' own colliders are much smaller than their bodies). It's a trigger: it blocks nothing.
    public static SphereCollider AddHitbox(GameObject catObject, float generosity)
    {
        Transform existing = catObject.transform.Find("Rush Hitbox");
        if (existing != null) return existing.GetComponent<SphereCollider>();

        Bounds body = new Bounds(catObject.transform.position + Vector3.up * 0.2f, Vector3.one * 0.3f);
        bool any = false;
        foreach (Renderer rend in catObject.GetComponentsInChildren<Renderer>())
        {
            if (!(rend is SkinnedMeshRenderer) && !(rend is MeshRenderer)) continue;
            if (rend.GetComponentInParent<Canvas>() != null || rend.GetComponentInParent<OrderBubble>() != null) continue;
            if (!any) { body = rend.bounds; any = true; }
            else body.Encapsulate(rend.bounds);
        }

        // A body measured somewhere odd (e.g. before the cat was placed) gets a sensible default instead
        Vector3 root = catObject.transform.position;
        if (!any || Vector3.Distance(body.center, root) > 1.2f)
        {
            body = new Bounds(root + Vector3.up * 0.25f, new Vector3(0.4f, 0.4f, 0.6f));
        }

        GameObject holder = new GameObject("Rush Hitbox");
        holder.transform.SetParent(catObject.transform, false);
        SphereCollider sphere = holder.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.center = holder.transform.InverseTransformPoint(body.center);
        float scale = Mathf.Max(0.0001f, holder.transform.lossyScale.x);
        sphere.radius = Mathf.Clamp(Mathf.Max(body.extents.x, body.extents.y, body.extents.z) * generosity, 0.2f, 0.6f) / scale;
        return sphere;
    }

    // The point a treat should curve towards: the cat wanting its colour that's closest to where it's heading,
    // if that's within maxAngle of its path
    public static Vector3? AssistTarget(Treat treat, float maxAngle)
    {
        Vector3 heading = treat.velocity;
        if (heading.sqrMagnitude < 0.01f) return null;
        Vector3 from = treat.transform.position;
        Vector3? best = null;
        float bestAngle = maxAngle;
        foreach (CatOrder catOrder in all)
        {
            if (catOrder == null || catOrder.wantedFood != treat.foodVariation) continue;
            Vector3 point = catOrder.aimPoint;
            float angle = Vector3.Angle(heading, point - from);
            if (angle <= bestAngle)
            {
                bestAngle = angle;
                best = point;
            }
        }
        return best;
    }

    // Gives a cat (just spawned) its order. Reuses the component and bubble when a pooled cat comes back.
    public static CatOrder Give(CatMovement cat, List<Food> foods, float bubbleScale, float hitboxGenerosity = 0f, float speedMultiplier = 1f)
    {
        CatOrder catOrder = cat.GetComponent<CatOrder>();
        if (catOrder == null) catOrder = cat.gameObject.AddComponent<CatOrder>();
        if (hitboxGenerosity > 0 && catOrder.hitbox == null) catOrder.hitbox = AddHitbox(cat.gameObject, hitboxGenerosity);
        catOrder.Begin(foods, bubbleScale);
        catOrder.SetSpeed(speedMultiplier);
        return catOrder;
    }

    // Walking speed compared with the cat's own (it starts from the cat's own speed each time, so pooled cats don't speed up)
    void SetSpeed(float multiplier)
    {
        if (agent == null) return;
        if (baseSpeed < 0) baseSpeed = agent.speed;
        agent.speed = baseSpeed * multiplier;
    }

    void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    void Begin(List<Food> foods, float bubbleScale)
    {
        cat = GetComponent<CatMovement>();
        agent = GetComponent<NavMeshAgent>();
        order.Clear();
        order.AddRange(foods);
        step = 0;
        runningOff = false;
        cat.treatGate = Eat;

        if (bubble == null) bubble = OrderBubble.Create(transform);
        bubble.transform.localScale = Vector3.one * bubbleScale;
        List<OrderBubble.Item> icons = new List<OrderBubble.Item>();
        foreach (Food food in order) icons.Add(food.icon);
        bubble.Show(icons, new Vector3(0, BubbleHeight(bubbleScale), 0));
    }

    // Just above the cat's head (the bubble is a child of the cat, so it follows it about)
    float BubbleHeight(float bubbleScale)
    {
        float top = 0.45f;
        bool any = false;
        foreach (Renderer rend in GetComponentsInChildren<Renderer>())
        {
            if (rend is ParticleSystemRenderer || rend is LineRenderer || rend is TrailRenderer || rend.GetComponentInParent<OrderBubble>() != null) continue;
            if (rend.GetComponentInParent<Canvas>() != null) continue;
            float height = rend.bounds.max.y - transform.position.y;
            top = any ? Mathf.Max(top, height) : height;
            any = true;
        }
        return Mathf.Clamp(top, 0.3f, 1.2f) + 0.2f * bubbleScale;
    }

    // Decides what a treat does to this cat. True = eat it as normal (one bite).
    bool Eat(Treat treat)
    {
        if (runningOff || step >= order.Count) return false;
        if (busy)
        {
            // Too late: it's mid-raid and not listening
            bubble.Shake();
            return false;
        }

        // A scatter or buckshot volley counts once per cat (the other pellets just vanish)
        if (treat.shotId != 0)
        {
            if (treat.shotId == lastShotEaten) return false;
            lastShotEaten = treat.shotId;
        }

        if (treat.foodVariation != order[step].variation)
        {
            // "That's not what I ordered!"
            bubble.Shake();
            bubble.FlashFace(cat.grumpiestFace, 0.5f);
            return false;
        }

        step++;
        rightTreat?.Invoke(this);
        if (step < order.Count)
        {
            bubble.Advance();
            return true;
        }

        // The last treat of the order: a happy face, then the cat is full and launches (or runs off to a hole)
        bubble.FlashFace(cat.happiestFace, 0f);
        cat.treatGate = null;
        served?.Invoke(this);
        Vector3? hole = holeFor != null ? holeFor(transform.position) : null;
        if (hole.HasValue) cat.leaveOverride = fedCat => LeaveThroughHole(hole.Value);
        cat.FeedCat(true);
        StartCoroutine(HideBubbleSoon());
        return false;
    }

    IEnumerator HideBubbleSoon()
    {
        yield return new WaitForSeconds(0.35f);
        if (bubble != null) bubble.Hide();
    }

    // Leaves the room through `exit` without being fed (no score), then goes back to the pool
    public void RunOff(Vector3 exit)
    {
        if (!isActiveAndEnabled || cat == null || cat.isFullyFed || runningOff) return;
        runningOff = true;
        cat.treatGate = t => false;
        if (bubble != null) bubble.Hide();
        cat.StopAllCoroutines();   // stops its roaming
        StartCoroutine(RunRoutine(exit, false));
    }

    // A served cat scampers off happily and dives into the hole (the cat has already scored)
    void LeaveThroughHole(Vector3 hole)
    {
        runningOff = true;
        StartCoroutine(RunRoutine(hole, true));
    }

    IEnumerator RunRoutine(Vector3 exit, bool served)
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            normalSpeed = agent.speed;
            normalUpdateRotation = agent.updateRotation;
            agent.speed = normalSpeed * 1.8f;
            agent.updateRotation = true;
            agent.isStopped = false;
            agent.SetDestination(exit);

            float timer = 0;
            while (timer < 5f && agent.isOnNavMesh && (agent.pathPending || agent.remainingDistance > 0.4f))
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }

        if (served)
        {
            // Shrinks down into the hole, then a little pop of confetti
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = true;
            diving = true;
            scaleBeforeDive = transform.localScale;
            Vector3 start = transform.position;
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.25f;
                transform.localScale = scaleBeforeDive * Mathf.Lerp(1f, 0.1f, t * t);
                transform.position = start + Vector3.down * 0.2f * t;
                yield return null;
            }
            cat.FinishLeaving();   // back to the pool
            yield break;
        }

        cat.DisappearParticleEffect();
        gameObject.SetActive(false);   // back to the pool
    }

    void LateUpdate()
    {
        // The bubble shows how the cat feels, so its own pop-up mood display stays hidden
        if (cat != null && cat.moodDisplay != null && cat.moodDisplay.activeSelf && (isHungry || runningOff))
        {
            cat.moodDisplay.SetActive(false);
        }
    }

    void OnDisable()
    {
        all.Remove(this);
        if (runningOff && agent != null)
        {
            agent.speed = normalSpeed;
            agent.updateRotation = normalUpdateRotation;
        }
        runningOff = false;
        busy = false;
        if (diving)
        {
            transform.localScale = scaleBeforeDive;
            diving = false;
        }
        if (cat != null)
        {
            cat.treatGate = null;
            cat.leaveOverride = null;
        }
        if (bubble != null) bubble.Hide();
        order.Clear();
    }
}
