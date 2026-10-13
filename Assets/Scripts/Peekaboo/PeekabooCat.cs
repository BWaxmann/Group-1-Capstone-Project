using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// A cat that pops out of a CatHole instead of walking around.
// It's still a CatMovement, so treats, scoring, combos, pickups and the end celebration all work as normal.
// If nobody feeds it in time it ducks back into its hole (no penalty).
public class PeekabooCat : CatMovement
{
    [Header("Peekaboo")]

    [SerializeField]
    [Min(0)]
    [Tooltip("Seconds of warning (meow, rattle, glowing eyes) before the cat appears.")]
    float warningDuration = 0.7f;

    [SerializeField]
    [Min(0.01f)]
    float popDuration = 0.3f;

    [SerializeField]
    [Min(0.01f)]
    float retreatDuration = 0.35f;

    [SerializeField]
    [Min(0)]
    [Tooltip("How far back inside the hole the cat waits, in metres.")]
    float hiddenDepth = 0.35f;

    [SerializeField]
    [Min(0)]
    [Tooltip("How far up the hidden cat sits below the opening, so it can't be seen before popping out.")]
    float hiddenDrop = 0.1f;

    CatHole hole;
    Vector3 baseScale;
    Vector3 hiddenPosition;
    Vector3 shownPosition;
    float stayTimer;

    void Awake()
    {
        baseScale = transform.localScale;

        // Peekaboo cats never walk, so switch off everything that drives walking
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
    }

    // Normal cats are placed with this; peekaboo cats must use PopOut instead
    public override void Initialise(Transform spawnPoint, Vector3 spawnPosition)
    {
        Debug.LogWarning("[PeekabooCat] Use PopOut(hole) to spawn peekaboo cats.", this);
        gameObject.SetActive(false);
    }

    public void PopOut(CatHole fromHole, float stayDuration)
    {
        hole = fromHole;
        hole.occupant = this;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        else if (jiggleLayerIndex >= 0)
        {
            animator.SetLayerWeight(jiggleLayerIndex, 1f);
        }
        if (voiceSource == null)
        {
            voiceSource = GetComponentInChildren<AudioSource>();
        }

        currentState = AIState.Roaming;
        isStunned = false;
        currentHealth = treatsUntilFull;
        stayTimer = stayDuration;
        if (moodCanvas != null)
        {
            moodCanvas.SetActive(false);
            moodDisplayTimer = 0;
            moodDisplayRoutineInstance = null;
        }

        // Face the player, but only turn left/right
        transform.localScale = baseScale * hole.scale;
        Vector3 toPlayer = Camera.main != null ? Camera.main.transform.position - hole.transform.position : -hole.transform.forward;
        toPlayer.y = 0;
        transform.rotation = toPlayer.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toPlayer) : hole.transform.rotation;

        shownPosition = hole.transform.position + hole.transform.forward * hole.outDistance;
        hiddenPosition = hole.transform.position - hole.transform.forward * hiddenDepth * hole.scale - Vector3.up * hiddenDrop * hole.scale;
        transform.position = hiddenPosition;

        StartCoroutine(PeekRoutine());
    }

    IEnumerator PeekRoutine()
    {
        // Warning: a loud meow from the hole, the hole shakes and eyes glow
        hole.Warn(warningDuration);
        if (roamingSounds != null && roamingSounds.Length > 0)
        {
            roamingSounds[Random.Range(0, roamingSounds.Length)].PlayOneShot(voiceSource);
        }
        yield return new WaitForSeconds(warningDuration);

        // Pop out with a springy overshoot
        yield return Move(hiddenPosition, shownPosition, popDuration, true);

        while (stayTimer > 0)
        {
            if (!isStunned)
            {
                stayTimer -= Time.deltaTime;
            }
            else
            {
                stunTimer -= Time.deltaTime;
                if (stunTimer <= 0) EndStun();
            }
            yield return null;
        }

        // Nobody fed it: duck back in, no penalty
        yield return Move(transform.position, hiddenPosition, retreatDuration, false);
        Leave();
    }

    IEnumerator Move(Vector3 from, Vector3 to, float duration, bool overshoot)
    {
        float timer = 0;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float eased = overshoot ? EaseOutBack(t) : t * t;
            transform.position = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }
        transform.position = to;
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    void Leave()
    {
        if (hole != null && hole.occupant == this) hole.occupant = null;
        transform.localScale = baseScale;
        gameObject.SetActive(false);
    }

    protected override void BeginVanish()
    {
        // Stop peeking, then use the same finisher as normal cats
        StopAllCoroutines();
        base.BeginVanish();
    }

    protected override void Vanish()
    {
        DropPickup();
        Leave();
    }

    public override void GetStunned(float duration)
    {
        isStunned = true;
        stunTimer = Mathf.Max(stunTimer, duration * stunDurationMultiplier);
    }

    protected override void EndStun()
    {
        isStunned = false;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(stunnedBoolHash, false);
        }
    }
}
