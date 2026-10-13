using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// A Space Cafe customer: comes out of its table's portal, hops onto the table and orders a food.
// Right food: happy, drops coins and hops back through the portal.
// Wrong food: a funny "blep", no penalty. Not served in time: shrugs and leaves, no penalty.
public class CustomerCat : CatMovement
{
    [Header("Customer")]

    [SerializeField]
    [Min(0.05f)]
    float arriveDuration = 0.5f;

    [SerializeField]
    [Min(0.05f)]
    float leaveDuration = 0.45f;

    [SerializeField]
    [Min(0)]
    float hopHeight = 0.35f;

    [SerializeField]
    [Min(0)]
    [Tooltip("How far above the cat's head the order bubble floats.")]
    float bubbleHeight = 0.2f;

    [SerializeField]
    [Min(0)]
    [Tooltip("How long the angry face shows after a wrong treat.")]
    float wrongFoodFaceDuration = 0.5f;

    [SerializeField]
    [Min(0)]
    [Tooltip("How long the happy face shows before a served cat portals out.")]
    float happyFaceDuration = 0.4f;

    CustomerTable table;
    OrderBubble bubble;
    Vector3 baseScale;
    float patience;
    float patienceTimer;
    bool waitingForOrder;
    bool leaving;
    int baseFeedingScore;

    // The treats this cat wants, in order (as treat pool variations), and how far through it the player is
    int[] order = new int[0];
    int orderStep;

    public int wantsNow => orderStep < order.Length ? order[orderStep] : -1;

    void Awake()
    {
        baseScale = transform.localScale;
        baseFeedingScore = feedingScore;

        // Customers never walk, so switch off everything that drives walking
        NavMeshAgent navAgent = GetComponent<NavMeshAgent>();
        if (navAgent != null) navAgent.enabled = false;
        agent = navAgent;

        foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour is CatRunAnimation || behaviour is CatLinkJump || behaviour is CatFootsteps)
            {
                behaviour.enabled = false;
            }
        }

        // The order bubble replaces the old hunger faces
        if (moodCanvas != null)
        {
            moodCanvas.SetActive(false);
            moodCanvas = null;
        }

        bubble = OrderBubble.Create(transform);
    }

    public override void Initialise(Transform spawnPoint, Vector3 spawnPosition)
    {
        Debug.LogWarning("[CustomerCat] Customers are sent by the Space Cafe Service.", this);
        gameObject.SetActive(false);
    }

    // foodVariations: the treats wanted, in order. icons: how to show each one in the bubble.
    public void Arrive(CustomerTable atTable, int[] foodVariations, List<OrderBubble.Item> icons, float patienceSeconds)
    {
        table = atTable;
        table.customer = this;
        order = foodVariations;
        orderStep = 0;
        // Bigger orders are worth more
        feedingScore = baseFeedingScore * Mathf.Max(1, order.Length);
        patience = patienceSeconds;
        patienceTimer = patienceSeconds;
        waitingForOrder = false;
        leaving = false;

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (voiceSource == null) voiceSource = GetComponentInChildren<AudioSource>();
        if (animator != null) animator.speed = 1f;
        PrepareFeel();

        currentState = AIState.Roaming;
        isStunned = false;
        currentHealth = treatsUntilFull;
        transform.localScale = baseScale * 0.01f;
        transform.position = table.portalRing.transform.position;
        FacePlayer();

        StartCoroutine(ArriveRoutine(icons));
    }

    IEnumerator ArriveRoutine(List<OrderBubble.Item> icons)
    {
        table.portalRing.Open();
        SpaceCafeService.instance.PlayPortalSound(transform.position);

        // Pop out of the portal and hop onto the table
        Vector3 start = table.portalRing.transform.position;
        Vector3 end = table.stand.position;
        float timer = 0;
        while (timer < arriveDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / arriveDuration);
            transform.position = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * hopHeight;
            transform.localScale = baseScale * Mathf.Max(0.01f, SpaceCafeVisuals.EaseOutBack(Mathf.Clamp01(t * 1.6f)));
            yield return null;
        }
        transform.position = end;
        transform.localScale = baseScale;
        juice.Hit(0.6f);

        // Order up!
        if (roamingSounds != null && roamingSounds.Length > 0)
        {
            roamingSounds[Random.Range(0, roamingSounds.Length)].PlayOneShot(voiceSource);
        }
        bubble.Show(icons, Vector3.up * (HeadHeight() + bubbleHeight));
        waitingForOrder = true;

        while (patienceTimer > 0)
        {
            patienceTimer -= Time.deltaTime;
            bubble.SetPatience(patienceTimer / patience);
            yield return null;
        }

        // Nobody served it: a little shrug, then back through the portal (no penalty)
        waitingForOrder = false;
        bubble.Hide();
        juice.Hit(0.5f);
        yield return new WaitForSeconds(0.25f);
        yield return LeaveRoutine(false);
    }

    float HeadHeight()
    {
        float top = 0.3f;
        foreach (Renderer rend in GetComponentsInChildren<Renderer>())
        {
            if (rend is SkinnedMeshRenderer || rend is MeshRenderer)
            {
                if (rend.transform.IsChildOf(bubble.transform)) continue;
                top = Mathf.Max(top, rend.bounds.max.y - transform.position.y);
            }
        }
        return top;
    }

    void FacePlayer()
    {
        Camera cam = Camera.main;
        Vector3 toPlayer = cam != null ? cam.transform.position - table.stand.position : -table.stand.forward;
        toPlayer.y = 0;
        if (toPlayer.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(toPlayer);
    }

    public override void HitByTreat(Treat treat)
    {
        if (!waitingForOrder || leaving) return;

        int wanted = wantsNow;
        if (wanted >= 0 && treat.foodVariation != wanted)
        {
            Debug.Log($"[Space Cafe] {Time.timeSinceLevelLoad:F1}s: {name} wanted food {wanted}, got {treat.foodVariation}");
            WrongFood();
            return;
        }

        orderStep++;
        if (orderStep >= order.Length)
        {
            // Whole order served
            FeedCat(treat.isInstaKill);
        }
        else
        {
            // One down, more to go: a happy squish and the next treat moves into the big bubble
            juice.Hit(0.9f);
            GameManager.instance.PlayCatHitSound(bodySource != null ? bodySource : voiceSource, 1f + orderStep * 0.15f);
            Haptics.PulseBoth(Haptics.Strength.Light);
            bubble.Advance();
        }
    }

    void WrongFood()
    {
        // "Blep": head shake, bubble wobble with an angry face, a silly sound. No penalty.
        juice.Hit(0.8f);
        bubble.Shake();
        bubble.FlashFace(AngryFace(), wrongFoodFaceDuration);
        SpaceCafeService.instance.PlayWrongFoodSound(transform.position);
    }

    // The cat's mood faces run from happiest (first) to grumpiest (last)
    Sprite HappyFace() => moodSprites != null && moodSprites.Length > 0 ? moodSprites[0] : null;
    Sprite AngryFace() => moodSprites != null && moodSprites.Length > 0 ? moodSprites[moodSprites.Length - 1] : null;

    // Served! (Called by FeedCat once full, and at the end of the round)
    protected override void BeginVanish()
    {
        if (leaving)
        {
            // Already on its way out (e.g. the round ended mid-exit)
            Leave();
            return;
        }
        currentState = AIState.Full;
        StopAllCoroutines();
        waitingForOrder = false;
        SpaceCafeService.instance.CustomerServed(this);
        StartCoroutine(HappyExitRoutine());
    }

    IEnumerator HappyExitRoutine()
    {
        // A happy bounce and a happy face in the bubble, then back through the portal
        juice.Hit(1.6f);
        bubble.FlashFace(HappyFace(), 0f);
        if (animator != null) animator.speed = 0f;
        yield return new WaitForSeconds(finisherFreeze);
        if (animator != null) animator.speed = 1f;
        yield return new WaitForSeconds(happyFaceDuration);
        bubble.Hide();
        yield return LeaveRoutine(true);
    }

    IEnumerator LeaveRoutine(bool happy)
    {
        leaving = true;
        table.portalRing.Open();

        Vector3 start = transform.position;
        Vector3 end = table.portalRing.transform.position;
        float spin = happy ? 720f : 0f;
        float timer = 0;
        while (timer < leaveDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / leaveDuration);
            transform.position = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * hopHeight * (happy ? 1.5f : 0.6f);
            transform.Rotate(0, spin * Time.deltaTime, 0, Space.World);
            transform.localScale = baseScale * Mathf.Max(0.01f, 1f - t * t);
            yield return null;
        }

        SpaceCafeService.instance.PlayPortalSound(end);
        table.portalRing.Close();
        if (happy && ParticleManager.instance != null)
        {
            ParticleManager.instance.ShowParticleEffect(launchPopParticleIndex >= 0 ? launchPopParticleIndex : disappearParticleIndex, end);
        }
        Leave();
    }

    void Leave()
    {
        if (table != null && table.customer == this) table.customer = null;
        transform.localScale = baseScale;
        leaving = false;
        gameObject.SetActive(false);
    }

    public override void GetStunned(float duration)
    {
        // Customers don't get stunned
    }

    protected override void EndStun()
    {
        isStunned = false;
    }
}
