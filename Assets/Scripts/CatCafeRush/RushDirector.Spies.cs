using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Rush Director, the spy cats: lowered on ropes from the ceiling vents after the secret recipe, in Spy phases and the remix finale
public partial class RushDirector : MonoBehaviour
{
    SoundBite spyMusic;

    string SpyLine
    {
        get
        {
            return phases.Find(spyPhase => spyPhase.kind == Kind.Spy)?.line;
        }
    }

    // Spies through the phase, a new one every so often (quicker as it goes on), however many are still dangling
    IEnumerator SpyCatsRoutine(Phase phase)
    {
        float start = clock;
        float end = start + phase.seconds;
        // The first spy drops in straight away and hangs there while it's explained (a player who knows what to do can
        // wave it off at once). The wave waits until it's been dealt with and the announcer has finished.
        yield return new WaitForSeconds(0.8f);
        SpyCat first = SendSpy();
        if (first != null)
        {
            first.holdBack = () => voice != null && voice.talking;
            waveHeld = true;
            while (playing && (!first.isFinished || (voice != null && voice.talking)))
            {
                yield return null;
            }
            waveHeld = false;
            yield return WaitClockRoutine(Mathf.Lerp(spyGapStart, spyGapEnd, 0f));
        }
        while (playing && clock < end - (spy.lowerSeconds + spy.approachSeconds) * 0.6f)
        {
            // Each spy on its own timer: one still dangling doesn't hold up the next (as many as there are free vents)
            float progress = (clock - start) / phase.seconds;
            SendSpy();
            float gap = Mathf.Lerp(spyGapStart, spyGapEnd, progress);
            yield return WaitClockRoutine(gap * Random.Range(0.8f, 1.2f));
        }
        while (playing && clock < end)
        {
            yield return null;
        }
        // (any still dangling get to finish: see WrapUp)
    }

    // The finale's spies, now and then (when there's room for one)
    IEnumerator RemixSpiesRoutine(Phase phase)
    {
        if (remixSpyGapMax <= 0 || CeilingVents().Count == 0)
        {
            yield break;
        }
        float end = clock + phase.seconds;
        while (playing && current == phase && !windingDown && clock < end - (spy.lowerSeconds + spy.approachSeconds))
        {
            yield return WaitClockRoutine(Random.Range(remixSpyGapMin, Mathf.Max(remixSpyGapMin, remixSpyGapMax)));
            while (playing && current == phase && !windingDown && (!RoomForMore || !MayIntroduce(SpyLine)))
            {
                yield return null;
            }
            if (!playing || current != phase || windingDown)
            {
                yield break;
            }
            if (SendSpy() != null)
            {
                BigStarted();
            }
        }
    }

    // One spy down a free ceiling vent (null if none could go)
    SpyCat SendSpy()
    {
        List<CafeVent> free = CeilingVents().FindAll(ceiling => !ceiling.busy);
        if (free.Count == 0)
        {
            return null;
        }
        CafeVent vent = free[Random.Range(0, free.Count)];
        CatMovement cat = SpyToSend();
        if (cat == null)
        {
            return null;
        }
        return SpyCat.Begin(cat, vent, player, tipJar, spy, null);
    }

    // The spy in its gear (Resources/SpyCat/Spy Phat Cat, a few kept ready); without it, a phat cat from the pool
    const string SpyPrefabPath = "SpyCat/Spy Phat Cat";
    GameObject spyPrefab;
    bool lookedForSpyPrefab;
    readonly List<CatMovement> spies = new List<CatMovement>();

    CatMovement SpyToSend()
    {
        if (!lookedForSpyPrefab)
        {
            lookedForSpyPrefab = true;
            spyPrefab = Resources.Load<GameObject>(SpyPrefabPath);
        }
        if (spyPrefab != null)
        {
            foreach (CatMovement ready in spies)
            {
                if (ready != null && !ready.gameObject.activeSelf)
                {
                    return ready;
                }
            }
            GameObject made = Instantiate(spyPrefab, transform);
            made.SetActive(false);
            CatMovement spyCat = made.GetComponent<CatMovement>();
            if (spyCat != null)
            {
                spies.Add(spyCat);
                return spyCat;
            }
        }
        if (catPool == null)
        {
            return null;
        }
        int[] phats = catPool.VariationsNamed("Phat");
        CatMovement cat;
        if (phats.Length > 0)
        {
            cat = catPool.GetRandomObjectFromPoolFromGivenVariations<CatMovement>(phats);
        }
        else
        {
            cat = null;
        }
        if (cat != null)
        {
            return cat;
        }
        return catPool.GetRandomObjectFromPool<CatMovement>();
    }
}
