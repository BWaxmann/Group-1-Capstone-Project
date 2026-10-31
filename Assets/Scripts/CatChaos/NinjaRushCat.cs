using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Cat Chaos boss phase: a ninja cat that blinks between the prop piles and loose props in front of the player,
// swatting things into the air at every stop. It sits still for a moment after each blink, which is the chance to
// feed it; once it's full the phase ends. If it isn't fed in time it vanishes anyway (Despawn After), so there's no fail.
// The CatChaosDirector spawns it and tells it where it can stop.
public class NinjaRushCat : BossCat
{
    [Header("Ninja Rush")]

    [SerializeField]
    [Min(0.2f)]
    [Tooltip("How long it sits at each stop (seconds): the window for feeding it.")]
    float minimumSit = 1.2f;

    [SerializeField]
    [Min(0.2f)]
    float maximumSit = 1.8f;

    [SerializeField]
    [Min(0)]
    [Tooltip("Pause after appearing before the swipe lands, so the player sees it coming.")]
    float swipeDelay = 0.45f;

    [SerializeField]
    [Min(1)]
    int knocksPerStop = 2;

    [SerializeField]
    [Min(0.1f)]
    [Tooltip("How far from its stop it can reach props, in metres.")]
    float reach = 1f;

    // Places on the NavMesh next to piles and loose props (set by the director)
    public List<Vector3> stops = new List<Vector3>();

    // Cat Cafe Rush's catch phases use it purely as a knocker: treats don't feed it then
    public bool feedable = true;

    // Cat Cafe Waves: it only goes for the prop piles (stops[i] is beside piles[i]) and sends a whole pile flying at
    // once, choosing piles that still have props on them
    public List<PropPile> piles = new List<PropPile>();
    public bool explodePiles;

    // While true it stays where it is instead of blinking about (Cat Cafe Waves: while the first catch is explained)
    public bool holdStill;

    // Cat Cafe Waves: before blinking to its next stop it waits (up to this many seconds) while the player still has props
    // in the air or on the beam from its last one. 0 = it doesn't wait
    public float waitForCatches = 0f;

    // Appears at `position` in a puff, facing the player
    public void BlinkTo(Vector3 position)
    {
        Blink(position);
    }

    // A paw swipe with its sound (the director decides what it sends flying)
    public void Swipe()
    {
        if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger(attackTriggerHash);
        hitDefendPointSound.PlayOneShot(attackSource);
    }

    // Changes how it behaves for its next appearance (Cat Cafe Rush uses it both as a quick knocker and as the boss)
    public void Configure(bool canBeFed, int knocks, float sitMin, float sitMax, bool explode = false)
    {
        feedable = canBeFed;
        knocksPerStop = Mathf.Max(1, knocks);
        minimumSit = Mathf.Max(0.2f, sitMin);
        maximumSit = Mathf.Max(minimumSit, sitMax);
        explodePiles = explode;
    }

    // The next stop: a different one from last time, and (blowing up piles) one whose pile still has props on it
    int NextStop(int lastStop)
    {
        List<int> choices = new List<int>();
        for (int i = 0; i < stops.Count; i++)
        {
            if (i == lastStop && stops.Count > 1) continue;
            if (explodePiles && i < piles.Count && piles[i] != null && piles[i].propsHome == 0) continue;
            choices.Add(i);
        }
        if (choices.Count == 0)
        {
            for (int i = 0; i < stops.Count; i++)
            {
                if (i != lastStop || stops.Count == 1) choices.Add(i);
            }
        }
        return choices[Random.Range(0, choices.Count)];
    }

    int lastShotEaten;

    public override void HitByTreat(Treat treat)
    {
        if (!feedable) return;
        // A scatter or buckshot volley counts once
        if (treat.shotId != 0)
        {
            if (treat.shotId == lastShotEaten) return;
            lastShotEaten = treat.shotId;
        }
        base.HitByTreat(treat);
    }

    // Disappears in a puff (no score, no pickup), e.g. when its phase ends
    public void Leave()
    {
        if (!gameObject.activeInHierarchy) return;
        DisappearParticleEffect();
        gameObject.SetActive(false);
    }

    protected override IEnumerator MoveToTargetRoutine()
    {
        WaitForSeconds updateWait = new WaitForSeconds(targetUpdateFrequency);
        WaitForSeconds swipeWait = new WaitForSeconds(swipeDelay);
        WaitForSeconds pawWait = new WaitForSeconds(0.25f);
        currentState = AIState.Roaming;
        int lastStop = -1;

        while (true)
        {
            if (currentState == AIState.Full) yield break;

            if (isStunned)
            {
                stunTimer -= targetUpdateFrequency;
                if (stunTimer <= 0) EndStun();
                yield return updateWait;
                continue;
            }

            if (holdStill)
            {
                yield return updateWait;
                continue;
            }

            if (stops.Count > 0)
            {
                float waited = 0;
                while (waitForCatches > 0 && waited < waitForCatches && PropsStillUp())
                {
                    waited += targetUpdateFrequency;
                    yield return updateWait;
                }
                if (currentState == AIState.Full) yield break;
                int next = NextStop(lastStop);
                lastStop = next;
                Blink(stops[next]);

                yield return swipeWait;
                if (currentState == AIState.Full) yield break;

                if (!isStunned)
                {
                    if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger(attackTriggerHash);
                    yield return pawWait;
                    if (currentState == AIState.Full) yield break;
                    hitDefendPointSound.PlayOneShot(attackSource);
                    if (explodePiles && next < piles.Count && piles[next] != null) piles[next].Explode(ViewerPosition());
                    else FlyingProp.KnockNear(transform.position, reach, knocksPerStop, ViewerPosition());
                }
            }

            yield return new WaitForSeconds(Random.Range(minimumSit, maximumSit));
        }
    }

    static bool PropsStillUp()
    {
        foreach (FlyingProp prop in FlyingProp.airborne)
        {
            if (prop != null && (prop.state == FlyingProp.State.Flying || prop.state == FlyingProp.State.Held)) return true;
        }
        return false;
    }

    protected override IEnumerator RotationRoutine()
    {
        WaitWhile wait = new WaitWhile(() => isStunned);
        while (true)
        {
            Vector3 face = ViewerPosition() - transform.position;
            face.y = 0;
            if (face.sqrMagnitude > 0.001f)
            {
                transform.forward = Vector3.RotateTowards(transform.forward, face, rotationSpeed * Time.deltaTime, 0);
            }
            yield return wait;
        }
    }

    void Blink(Vector3 position)
    {
        DisappearParticleEffect();
        agent.Warp(position);
        if (agent.isOnNavMesh) agent.isStopped = true;
        Vector3 face = ViewerPosition() - position;
        face.y = 0;
        if (face.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(face);
        DisappearParticleEffect();
    }

    Vector3 ViewerPosition()
    {
        if (Camera.main != null) return Camera.main.transform.position;
        if (player != null)
        {
            return player.transform.position;
        }
        return Vector3.zero;
    }
}
