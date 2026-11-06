using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Rush Director, the ninja and the piles: summoning it to its stops, the first catch's intro, a prop grabbed, and the tidy bonus the piles pay at the end
public partial class RushDirector : MonoBehaviour
{
    NinjaRushCat ninja;
    SphereCollider ninjaHitbox;
    readonly List<DefendPoint> piles = new List<DefendPoint>();
    readonly List<PropPile> propPiles = new List<PropPile>();
    bool catchIntroDone;

    void PropGrabbed(FlyingProp prop)
    {
        if (!FlyingProp.dragHome || !playing)
        {
            return;
        }
        // The catch phase's own line already says to carry things back: no need for both
        Phase catchPhase = phases.Find(p => p.kind == Kind.Catch);
        string taught;
        if (catchPhase != null)
        {
            taught = catchPhase.line;
        }
        else
        {
            taught = null;
        }
        if (!string.IsNullOrEmpty(taught) && (heardLines.Contains(taught) || pendingLines.Contains(taught)))
        {
            return;
        }
        SayOnce(carryLine);
    }

    // The first catch of the run, taught by doing: the clock stops, one prop in front of the player pops up and hangs
    // in the air with a hint by it, and play carries on once it's caught (or after a while anyway, no fail)
    IEnumerator CatchIntroRoutine()
    {
        catchIntroDone = true;
        introPaused = true;
        yield return new WaitForSeconds(1.2f);

        Transform eye;
        if (player != null)
        {
            eye = player.eyePoint;
        }
        else
        {
            eye = null;
        }
        Vector3 viewer;
        if (eye != null)
        {
            viewer = eye.position;
        }
        else if (player != null)
        {
            viewer = player.transform.position;
        }
        else
        {
            viewer = transform.position;
        }
        FlyingProp prop = PropInView(eye);

        // The ninja appears right beside it and flicks it up: its opening move (then it waits until it's caught)
        if (prop != null && ninja != null)
        {
            Vector3 towards = Flat(viewer - prop.centre);
            if (towards.sqrMagnitude > 0.001f)
            {
                towards = towards.normalized;
            }
            else
            {
                towards = Vector3.forward;
            }
            if (NavMesh.SamplePosition(prop.centre + towards * 0.4f, out NavMeshHit beside, 1f, NavMesh.AllAreas))
            {
                SummonNinja(false, beside.position);
                yield return new WaitForSeconds(0.6f);
                if (ninja.gameObject.activeInHierarchy)
                {
                    ninja.Swipe();
                }
                yield return new WaitForSeconds(0.15f);
            }
        }

        if (prop != null && prop.Knock(new Vector3(viewer.x, prop.transform.position.y, viewer.z), true))
        {
            GameObject hint = FloatingWord.Make(catchIntroHint, new Color(1f, 0.85f, 0.2f), 2.2f);
            ninjaInputTutorialImage?.SetActive(true);
            float timer = 0;
            float nextBuzz = 0;
            while (playing && timer < catchIntroTimeout && prop.state == FlyingProp.State.Flying)
            {
                timer += Time.deltaTime;
                hint.transform.position = prop.centre + Vector3.up * (0.35f + Mathf.Abs(Mathf.Sin(timer * 4f)) * 0.06f);
                FloatingWord.FacePlayer(hint.transform);
                // A nudge in both hands, the hands that do the catching
                if (timer >= nextBuzz)
                {
                    Haptics.PulseBoth(Haptics.Strength.Light);
                    nextBuzz = timer + 1.5f;
                }
                yield return null;
            }
            Destroy(hint);
            ninjaInputTutorialImage?.SetActive(false);
            if (prop.state == FlyingProp.State.Held || prop.state == FlyingProp.State.Returning)
            {
                FloatingWord.Show(catchIntroPraise, prop.centre + Vector3.up * 0.3f, new Color(0.4f, 1f, 0.5f));
                yield return new WaitForSeconds(0.6f);
            }
        }
        introPaused = false;
    }

    // A prop at home that's in front of the player and not too far: the one nearest the middle of their view
    FlyingProp PropInView(Transform eye)
    {
        if (eye == null)
        {
            return null;
        }
        // In front of where the player stands to play (the rig's forward, not wherever they happen to be looking), and
        // off a pile on a table: never a loose prop like a plant on the floor beside them
        Vector3 ahead;
        if (player != null)
        {
            ahead = Flat(player.transform.forward);
        }
        else
        {
            ahead = Flat(eye.forward);
        }
        if (ahead.sqrMagnitude < 0.001f)
        {
            ahead = Flat(eye.forward);
        }
        FlyingProp best = null;
        float bestAngle = 50f;
        foreach (FlyingProp prop in FlyingProp.all)
        {
            if (prop == null || prop.state != FlyingProp.State.Home || prop.isLoose)
            {
                continue;
            }
            Vector3 to = prop.centre - eye.position;
            float distance = Flat(to).magnitude;
            if (distance < 1.2f || distance > 5f)
            {
                continue;
            }
            float angle = Vector3.Angle(ahead, Flat(to));
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = prop;
            }
        }
        return best;
    }

    // Brings the ninja in at one of its stops; or at `holdAt`, where it stays put until told to go (holdStill)
    // (no default parameter: Liminal doesn't allow ones that use Unity types)
    void SummonNinja(bool boss)
    {
        SummonNinja(boss, null);
    }

    void SummonNinja(bool boss, Vector3? holdAt)
    {
        if (ninja == null)
        {
            return;
        }
        List<Vector3> stops;
        ninja.piles.Clear();
        if (ninjaExplodesPiles && PileStops(out stops, ninja.piles))
        {
            // Only the piles, each blown up whole (the finale's too, if it's making a mess)
            if (boss)
            {
                ninja.Configure(!bossMakesMess, bossKnocksPerStop, bossSitMin, bossSitMax, true);
            }
            else
            {
                ninja.Configure(false, catchKnocksPerStop, catchSitMin, catchSitMax, true);
            }
        }
        else
        {
            if (ninjaStops.Count == 0)
            {
                return;
            }
            stops = StopsAwayFromPlayer();
            if (boss)
            {
                ninja.Configure(true, bossKnocksPerStop, bossSitMin, bossSitMax);
            }
            else
            {
                ninja.Configure(false, catchKnocksPerStop, catchSitMin, catchSitMax);
            }
        }
        ninja.stops = stops;
        ninja.holdStill = holdAt.HasValue;   // before it starts, or its first move happens straight away
        // Blowing up piles, its phase sends it away when it's over (a long phase or a slow catch intro mustn't time it
        // out early); otherwise it keeps its own timer, as in Cat Cafe Rush
        if (ninjaExplodesPiles)
        {
            ninja.despawnAfterOverride = 300f;
        }
        else
        {
            ninja.despawnAfterOverride = 0f;
        }
        ninja.waitForCatches = ninjaWaitsForCatches;
        ninja.wantsTreats = () => ninja.feedable;   // a ninja that can't be fed lets treats past
        ninja.gameObject.SetActive(true);
        ninja.Initialise(transform, holdAt ?? stops[Random.Range(0, stops.Count)]);
        if (holdAt.HasValue)
        {
            ninja.BlinkTo(holdAt.Value);
        }
        // The boss gets the same generous hit area as the other cats
        if (cleanShots && ninjaHitbox == null)
        {
            ninjaHitbox = CatOrder.AddHitbox(ninja.gameObject, hitboxGenerosity);
        }
    }

    // A spot beside each prop pile (on its table, on the player's side) for the ninja to appear at
    bool PileStops(out List<Vector3> stops, List<PropPile> forPiles)
    {
        stops = new List<Vector3>();
        Vector3 viewer;
        if (player != null)
        {
            viewer = player.transform.position;
        }
        else
        {
            viewer = transform.position;
        }
        foreach (PropPile pile in propPiles)
        {
            if (pile == null || !pile.isActiveAndEnabled)
            {
                continue;
            }
            Vector3 at = pile.transform.position;
            Vector3 towards = Flat(viewer - at);
            if (towards.sqrMagnitude > 0.001f)
            {
                towards = towards.normalized;
            }
            else
            {
                towards = Vector3.forward;
            }
            Vector3 beside = at + towards * 0.35f;
            if (!NavMesh.SamplePosition(beside, out NavMeshHit hit, 0.6f, NavMesh.AllAreas) &&
                !NavMesh.SamplePosition(at, out hit, 1.5f, NavMesh.AllAreas))
            {
                continue;
            }
            stops.Add(hit.position);
            forPiles.Add(pile);
        }
        return stops.Count > 0;
    }

    const float TidyBonusSeconds = 3f;
    const float TidyWaitMax = 15f;

    // Each pile pays into the tip jar (and the score) by how much of it is still standing
    IEnumerator TidyBonusRoutine()
    {
        if ((tidyBonusCoins <= 0 && tidyBonusScore <= 0) || propPiles.Count == 0)
        {
            yield break;
        }
        // Everything the ninja last sent flying comes down first (caught and carried home, or landed), so a pile that
        // was just blown up isn't counted while its plates are still in the air. The clock waits too (not forever).
        float waited = 0;
        wrapUpPaused = true;
        while (playing && FlyingProp.airborne.Count > 0 && waited < TidyWaitMax)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        wrapUpPaused = false;
        if (!playing)
        {
            yield break;
        }
        SayOnce(tidyLine);
        if (!string.IsNullOrEmpty(tidyBonusBanner))
        {
            GameManager.instance.ShowBanner(tidyBonusBanner, bannerSeconds);
        }
        yield return new WaitForSeconds(0.6f);
        foreach (PropPile pile in propPiles)
        {
            if (pile == null)
            {
                continue;
            }
            float whole = pile.standing;
            Vector3 at = pile.transform.position + Vector3.up * 0.4f;
            int score = Mathf.RoundToInt(whole * tidyBonusScore);
            int coins = Mathf.RoundToInt(whole * tidyBonusCoins);
            if (score > 0)
            {
                GameManager.instance.AddBonus(score, at);
            }
            if (coins > 0 && tipJar != null)
            {
                tipJar.AddCoins(at, coins);
            }
            FloatingWord.Show($"{Mathf.RoundToInt(whole * 100)}%", at + Vector3.up * 0.3f,
                              whole > 0.66f ? new Color(0.4f, 1f, 0.5f) : whole > 0.33f ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.4f, 0.3f), 4f, 1.6f);
            yield return new WaitForSeconds(0.5f);
        }
    }
}
