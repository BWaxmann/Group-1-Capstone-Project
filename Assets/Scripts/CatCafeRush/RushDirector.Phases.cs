using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Rush Director, the shift: the phase playlist, the wrap-up at the end of a phase, what counts as still going, and the breathers between the remix finale's big events
public partial class RushDirector : MonoBehaviour
{
    float nextBigAt;     // the remix finale: when the next big thing may start (a breather after each)
    int lastBusy;
    int feedPhasesSeen;
    int ninjaPhasesSeen;

    IEnumerator RunPhasesRoutine()
    {
        for (int index = 0; index < phases.Count; index++)
        {
            Phase phase = phases[index];
            if (!playing)
            {
                yield break;
            }
            current = phase;
            nextRaid = clock + phase.raidEvery * 0.6f;
            nextLatch = clock + phase.latchEvery * 0.5f;
            if (!Feeding(phase.kind) || phase.latchEvery <= 0)
            {
                LauncherKitten.EndAll();
            }
            if (!Feeding(phase.kind) && MafiaHeist.current != null)
            {
                MafiaHeist.current.Cancel();
            }

            // Hungry cats only go for the piles in feeding phases (and only if this phase says so)
            if (phase.kind == Kind.Feed)
            {
                CatMovement.targetingChanceOverride = phase.attackChance;
            }
            else
            {
                CatMovement.targetingChanceOverride = 0f;
            }
            PropPile.toppleOnHit = attacksTopple;

            string banner = phase.banner;
            if (phase.kind == Kind.Boss && finaleFireStyles)
            {
                player.UnlockFireStyles(true);
                if (!string.IsNullOrEmpty(fireStylesBanner))
                {
                    banner = fireStylesBanner;
                }
            }
            if (!string.IsNullOrEmpty(banner))
            {
                GameManager.instance.ShowBanner(banner, bannerSeconds);
            }
            bool ninjaPhase = phase.kind == Kind.Catch || phase.kind == Kind.Boss || phase.kind == Kind.Remix;
            // The piles' bars are only up while the ninja's about (each pops up again when it blows that pile up)
            if (!ninjaPhase)
            {
                PropPile.HideBars();
            }
            FlyingProp.dragHome = dragPropsHome && ninjaPhase;
            if (phase.kind == Kind.Remix)
            {
                FlyingProp.dropMargin = remixPropDropMargin;
            }
            else
            {
                FlyingProp.dropMargin = propDropMargin;
            }
            if (ninjaPhase && ninjaPhasesSeen++ > 0 && restockPiles && FlyingProp.RestockAll() > 0)
            {
                FloatingWord.Show("Restocked!", player != null && player.eyePoint != null ? player.eyePoint.position + player.eyePoint.forward * 2f : transform.position, new Color(0.4f, 1f, 0.5f), 3f, 1.4f);
            }
            Say(phase.line, ninjaPhase ? ninjaSting : null, hold: true);

            switch (phase.kind)
            {
                case Kind.Feed:
                    // After the first feeding phase, the Mafia cat makes his move partway through
                    if (++feedPhasesSeen > 1)
                    {
                        StartCoroutine(HeistsRoutine(phase, heistsPerPhase, heistGang));
                    }
                    // Last orders: nobody new comes in for the last few seconds
                    yield return WaitClockRoutine(Mathf.Max(0, phase.seconds - lastCallSeconds));
                    windingDown = true;
                    yield return WaitClockRoutine(Mathf.Min(lastCallSeconds, phase.seconds));
                    break;

                case Kind.Catch:
                    Haptics.PulseBoth(Haptics.Strength.Strong);
                    CatRaid.CancelAll();
                    SendCatsAway();
                    if (catchIntro && !catchIntroDone)
                    {
                        yield return CatchIntroRoutine();
                    }
                    if (ninja != null && ninja.gameObject.activeInHierarchy && ninja.holdStill)
                    {
                        // The ninja that opened the catch intro gets going
                        ninja.holdStill = false;
                    }
                    else
                    {
                        yield return WaitClockRoutine(1f);
                        SummonNinja(false);
                    }
                    yield return WaitClockRoutine(Mathf.Max(0, phase.seconds - 1f));
                    if (ninja != null)
                    {
                        ninja.Leave();
                    }
                    break;

                case Kind.Boss when bossMakesMess:
                    // The finale is all about saving the piles: the hungry cats clear out and the ninja trashes the
                    // place until just before closing, then each pile pays out by how whole it is
                    Haptics.PulseBoth(Haptics.Strength.Huge);
                    CatRaid.CancelAll();
                    SendCatsAway();
                    yield return WaitClockRoutine(1f);
                    SummonNinja(true);
                    float messUntil = clock + Mathf.Max(0, phase.seconds - 1f - TidyBonusSeconds);
                    while (playing && clock < messUntil)
                    {
                        yield return null;
                    }
                    if (ninja != null)
                    {
                        ninja.Leave();
                    }
                    if (playing)
                    {
                        yield return TidyBonusRoutine();
                    }
                    while (playing)
                    {
                        yield return null;
                    }
                    break;

                case Kind.Remix:
                    // The finale: a bit of every wave at once, but one big thing at a time. Customers
                    // keep ordering (Update), launcher kittens come by (Latch Every), the ninja makes a slower mess,
                    // spies drop in now and then, the Mafia cat strolls in, and a kitten flood pours out once
                    Haptics.PulseBoth(Haptics.Strength.Huge);
                    yield return WaitClockRoutine(1f);
                    nextChanceFloodAt = 0f;
                    if (remixRotation)
                    {
                        StartCoroutine(RemixRotationRoutine(phase));
                    }
                    else
                    {
                        SummonRemixNinja();
                        StartCoroutine(HeistsRoutine(phase, remixHeists, remixHeistGang));
                        StartCoroutine(RemixSpiesRoutine(phase));
                    }
                    if (remixFloodAt > 0)
                    {
                        StartCoroutine(RemixFloodRoutine(phase));
                    }
                    float remixUntil = clock + Mathf.Max(0, phase.seconds - 1f - TidyBonusSeconds);
                    while (playing && clock < remixUntil)
                    {
                        yield return null;
                    }

                    // Closing up: everyone heads off, then the piles pay out
                    windingDown = true;
                    if (ninja != null)
                    {
                        ninja.Leave();
                    }
                    if (MafiaHeist.current != null)
                    {
                        MafiaHeist.current.Cancel();
                    }
                    SpyCat.EndAll();
                    if (playing)
                    {
                        yield return TidyBonusRoutine();
                    }
                    while (playing)
                    {
                        yield return null;
                    }
                    break;

                case Kind.Boss:
                    Haptics.PulseBoth(Haptics.Strength.Huge);
                    yield return WaitClockRoutine(1f);
                    SummonNinja(true);
                    float timer = 1f;
                    while (playing && timer < phase.seconds && ninja.gameObject.activeInHierarchy && !ninja.isFullyFed)
                    {
                        timer += Time.deltaTime;
                        yield return null;
                    }
                    if (playing)
                    {
                        GameManager.instance.ShowBanner(ninja.isFullyFed ? bossFedBanner : bossLeftBanner, bannerSeconds);
                        Say(ninja.isFullyFed ? bossFedLine : bossLeftLine);
                        if (ninja.isFullyFed && tipJar != null)
                        {
                            tipJar.AddCoins(ninja.transform.position + Vector3.up * 0.3f, bossCoins);
                        }
                    }
                    // If time is left after the boss, the round finishes with ordinary feeding
                    current = new Phase { kind = Kind.Feed, cats = phase.cats + 2, shortestOrder = phase.shortestOrder, longestOrder = phase.longestOrder };
                    while (playing)
                    {
                        yield return null;
                    }
                    break;

                case Kind.Stampede:
                    Haptics.PulseBoth(Haptics.Strength.Strong);
                    CatRaid.CancelAll();
                    SendCatsAway();
                    yield return StampedeRoutine(phase);
                    break;

                case Kind.Spy:
                    Haptics.PulseBoth(Haptics.Strength.Strong);
                    CatRaid.CancelAll();
                    spyCustomersIn = false;   // the cafe is cleared while the first spy is explained
                    SendCatsAway();
                    FindVents();   // from where the player stands now, not where they stood at the start
                    if (CeilingVents().Count > 0)
                    {
                        spyMusic = SoundBite.Play("Spy Music", CafeResources.Load<AudioClip>("CafeSounds/spy_music_loop"), spyMusicVolume,
                                                  musicUnderSpyMusic, CafeResources.Load<AudioClip>("CafeSounds/spy_music_intro"),
                                                  loop: true, fadeIn: 0.05f);
                        yield return SpyCatsRoutine(phase);
                    }
                    else
                    {
                        Debug.LogWarning("[Cat Cafe Waves] No ceiling vent in the cafe for the spy cats: ordinary feeding instead.");
                        current = new Phase { kind = Kind.Feed, seconds = phase.seconds, cats = phase.cats, shortestOrder = phase.shortestOrder, longestOrder = phase.longestOrder };
                        yield return WaitClockRoutine(Mathf.Max(0, phase.seconds - lastCallSeconds));
                        windingDown = true;
                        yield return WaitClockRoutine(Mathf.Min(lastCallSeconds, phase.seconds));
                    }
                    break;
            }

            // Time's up: what's still going on gets a moment to finish before the next phase starts
            if (index < phases.Count - 1)
            {
                yield return WrapUpRoutine(phase, phases[index + 1]);
            }
            windingDown = false;
            if (phase.kind == Kind.Stampede)
            {
                KittenRun.EndAll();
                if (player != null)
                {
                    player.ForceFireStyle(null);
                }
            }
            if (phase.kind == Kind.Spy)
            {
                SpyCat.EndAll();
                if (spyMusic != null)
                {
                    spyMusic.Stop(2f);
                }
                spyMusic = null;
            }
        }
    }

    // Time's up for `phase`: the round clock waits (a few seconds at most) while what's still going on finishes, so a
    // wave gets to end before the next one starts
    IEnumerator WrapUpRoutine(Phase phase, Phase next)
    {
        if (wrapUpMax <= 0)
        {
            yield break;
        }
        windingDown = true;
        bool paused = false;
        float waited = 0;
        while (playing && waited < wrapUpMax && StillGoing(phase, next))
        {
            if (!paused)
            {
                wrapUpPaused = true;
                paused = true;
            }
            waited += Time.deltaTime;
            yield return null;
        }
        if (paused)
        {
            wrapUpPaused = false;
        }
    }

    bool StillGoing(Phase phase, Phase next)
    {
        switch (phase.kind)
        {
            case Kind.Feed:
                // Into another feeding phase it all just carries on
                if (next.kind == Kind.Feed)
                {
                    return false;
                }
                if (MafiaHeist.current != null || LauncherKitten.active.Count > 0)
                {
                    return true;
                }
                return orders.Exists(o => o != null && o.isHungry && o.hasOrdered);
            case Kind.Catch:
            case Kind.Boss:
                foreach (FlyingProp prop in FlyingProp.airborne)
                {
                    if (prop != null && (prop.state == FlyingProp.State.Flying || prop.state == FlyingProp.State.Held))
                    {
                        return true;
                    }
                }
                return false;
            case Kind.Stampede:
                return KittenRun.running.Exists(kitten => kitten != null && kitten.feedable && !kitten.isFed);
            case Kind.Spy:
                return SpyCat.active.Count > 0;
        }
        return false;
    }

    // Phases where hungry cats with orders keep coming in
    bool HasOrders(Kind kind)
    {
        return kind == Kind.Feed || kind == Kind.Remix || (kind == Kind.Boss && !bossMakesMess) ||
               (kind == Kind.Spy && spyPhaseKeepsCustomers && spyCustomersIn);
    }

    // Feeding-like phases: launcher kittens and the Mafia cat carry on through them
    static bool Feeding(Kind kind)
    {
        return kind == Kind.Feed || kind == Kind.Remix;
    }

    // The remix finale's chaos budget: how many big things are going on right now
    int BusyEvents()
    {
        int busy = 0;
        if (MafiaHeist.current != null)
        {
            busy++;
        }
        if (LauncherKitten.active.Count > 0)
        {
            busy++;
        }
        if (flooding)
        {
            busy++;
        }
        if (ninjaVisiting)
        {
            busy++;
        }
        busy += SpyCat.active.Count;
        return busy;
    }

    // Whether something new may start (always, outside the remix finale)
    bool RoomForMore
    {
        get
        {
            return (current == null || current.kind != Kind.Remix || (BusyEvents() < remixMaxBusy && clock >= nextBigAt));
        }
    }

    // In the finale the breather runs from when the last big thing finishes (each one plays out, then a pause, then the next)
    void UpdateBreather()
    {
        if (current.kind != Kind.Remix)
        {
            return;
        }
        int busy = BusyEvents();
        if (busy == 0 && lastBusy > 0)
        {
            nextBigAt = Mathf.Max(nextBigAt, clock + remixBreather);
        }
        lastBusy = busy;
    }

    // ---------------- The finale as a rotation (Remix Rotation) ----------------

    bool ninjaVisiting;
    int lastRotation = -1;   // 0 ninja, 1 heist, 2 spy
    static readonly string[] RotationNames = { "ninja", "heist", "spy" };

    void SummonRemixNinja()
    {
        SummonNinja(true);
        if (ninja != null)
        {
            ninja.Configure(false, bossKnocksPerStop, remixNinjaSitMin, remixNinjaSitMax, ninjaExplodesPiles && ninja.piles.Count > 0);
        }
    }

    // Every few seconds one big thing: a ninja visit, a Mafia heist or a spy (not the one before, when another can go)
    IEnumerator RemixRotationRoutine(Phase phase)
    {
        lastRotation = -1;
        float end = clock + phase.seconds - TidyBonusSeconds;
        float nextAt = clock + 2f;
        while (playing && current == phase && !windingDown && clock < end - 6f)
        {
            if (clock < nextAt || BusyEvents() >= remixMaxBusy)
            {
                yield return null;
                continue;
            }
            List<int> ready = new List<int>();
            if (ninja != null && !ninja.gameObject.activeInHierarchy && !PropsUp())
            {
                ready.Add(0);
            }
            if (mafiaHeists && mafiaPrefab != null && tipJar != null && MafiaHeist.current == null && MayIntroduce(heistLine))
            {
                ready.Add(1);
            }
            if (CeilingVents().Exists(vent => !vent.busy) && MayIntroduce(SpyLine))
            {
                ready.Add(2);
            }
            if (ready.Count > 1)
            {
                ready.Remove(lastRotation);
            }
            if (ready.Count == 0)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }
            int pick = ready[Random.Range(0, ready.Count)];
            bool started = false;
            switch (pick)
            {
                case 0:
                    StartCoroutine(NinjaVisitRoutine(phase));
                    started = true;
                    break;
                case 1:
                    StartHeist(remixHeistGang);
                    started = MafiaHeist.current != null;
                    break;
                case 2:
                    started = SendSpy() != null;
                    break;
            }
            if (started)
            {
                Debug.Log($"[Cat Cafe Waves] Finale rotation: {RotationNames[pick]} at {clock:0}s.");
                lastRotation = pick;
                nextAt = clock + Random.Range(remixRotationGapMin, Mathf.Max(remixRotationGapMin, remixRotationGapMax));
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    // The ninja blinks in, blows up a pile, and leaves
    IEnumerator NinjaVisitRoutine(Phase phase)
    {
        ninjaVisiting = true;
        SummonRemixNinja();
        float until = clock + remixNinjaVisitSeconds;
        while (playing && current == phase && !windingDown && clock < until && ninja != null && ninja.gameObject.activeInHierarchy)
        {
            yield return null;
        }
        if (ninja != null)
        {
            ninja.Leave();
        }
        ninjaVisiting = false;
    }

    static bool PropsUp()
    {
        foreach (FlyingProp prop in FlyingProp.airborne)
        {
            if (prop != null && (prop.state == FlyingProp.State.Flying || prop.state == FlyingProp.State.Held))
            {
                return true;
            }
        }
        return false;
    }

    // A big thing just started: in the finale, the next waits a breather
    void BigStarted()
    {
        if (current != null && current.kind == Kind.Remix)
        {
            nextBigAt = clock + remixBreather;
        }
    }
}
