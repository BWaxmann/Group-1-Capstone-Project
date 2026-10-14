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

    // Gives a cat (just spawned) its order. Reuses the component and bubble when a pooled cat comes back.
    public static CatOrder Give(CatMovement cat, List<Food> foods, float bubbleScale)
    {
        CatOrder catOrder = cat.GetComponent<CatOrder>();
        if (catOrder == null) catOrder = cat.gameObject.AddComponent<CatOrder>();
        catOrder.Begin(foods, bubbleScale);
        return catOrder;
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
            if (rend is ParticleSystemRenderer || rend is LineRenderer || rend.GetComponentInParent<OrderBubble>() != null) continue;
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

        if (treat.foodVariation != order[step].variation)
        {
            // "That's not what I ordered!"
            bubble.Shake();
            bubble.FlashFace(cat.grumpiestFace, 0.5f);
            return false;
        }

        step++;
        if (step < order.Count)
        {
            bubble.Advance();
            return true;
        }

        // The last treat of the order: a happy face, then the cat is full and launches
        bubble.FlashFace(cat.happiestFace, 0f);
        cat.treatGate = null;
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
        StartCoroutine(RunRoutine(exit));
    }

    IEnumerator RunRoutine(Vector3 exit)
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
        if (runningOff && agent != null)
        {
            agent.speed = normalSpeed;
            agent.updateRotation = normalUpdateRotation;
        }
        runningOff = false;
        if (cat != null) cat.treatGate = null;
        if (bubble != null) bubble.Hide();
        order.Clear();
    }
}
