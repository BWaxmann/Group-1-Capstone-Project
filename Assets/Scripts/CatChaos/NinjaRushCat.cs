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

    // Changes how it behaves for its next appearance (Cat Cafe Rush uses it both as a quick knocker and as the boss)
    public void Configure(bool canBeFed, int knocks, float sitMin, float sitMax)
    {
        feedable = canBeFed;
        knocksPerStop = Mathf.Max(1, knocks);
        minimumSit = Mathf.Max(0.2f, sitMin);
        maximumSit = Mathf.Max(minimumSit, sitMax);
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

            if (stops.Count > 0)
            {
                int next = Random.Range(0, stops.Count);
                if (stops.Count > 1 && next == lastStop) next = (next + 1 + Random.Range(0, stops.Count - 1)) % stops.Count;
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
                    FlyingProp.KnockNear(transform.position, reach, knocksPerStop, ViewerPosition());
                }
            }

            yield return new WaitForSeconds(Random.Range(minimumSit, maximumSit));
        }
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
        return player != null ? player.transform.position : Vector3.zero;
    }
}
