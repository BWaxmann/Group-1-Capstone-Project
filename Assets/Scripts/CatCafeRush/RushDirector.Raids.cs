using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Rush Director, raids: a hungry cat makes a run at the tip jar or a pile
public partial class RushDirector : MonoBehaviour
{
    float nextRaid;
    bool raidJarNext = true;

    // Every so often (one at a time) a hungry cat the player can see makes a run at the tip jar or a pile
    void UpdateRaids()
    {
        if (current.raidEvery <= 0 || CatRaid.active.Count > 0 || clock < nextRaid)
        {
            return;
        }
        nextRaid = clock + 1f;   // nobody could go: try again in a moment

        // With a tip jar, raids are all about the jar (the piles are the ninja's); otherwise they take turns
        bool jarFirst = tipJar != null || raidJarNext;
        for (int attempt = 0; attempt < (tipJar != null ? 1 : 2); attempt++)
        {
            bool useJar;
            if (attempt == 0)
            {
                useJar = jarFirst;
            }
            else
            {
                useJar = !jarFirst;
            }
            CatRaid.Target target;
            if (useJar)
            {
                target = JarTarget();
            }
            else
            {
                target = PileTarget();
            }
            if (target == null)
            {
                continue;
            }
            foreach (CatOrder raider in Raiders(target.position))
            {
                if (CatRaid.Begin(raider, target, raid, RaidOver) != null)
                {
                    raidJarNext = !useJar;
                    nextRaid = float.MaxValue;   // set again when this raid is over
                    return;
                }
            }
        }
    }

    void RaidOver(CatRaid finished, CatRaid.Result result)
    {
        if (current != null)
        {
            nextRaid = clock + current.raidEvery * Random.Range(0.8f, 1.2f);
        }
    }

    CatRaid.Target JarTarget()
    {
        if (tipJar == null)
        {
            return null;
        }
        TipJar jar = tipJar;
        return new CatRaid.Target { position = jar.transform.position, swat = from => jar.Spill(from, raidSpillCoins) };
    }

    CatRaid.Target PileTarget()
    {
        List<DefendPoint> standing = piles.FindAll(p => p != null && p.isActiveAndEnabled);
        if (standing.Count == 0)
        {
            return null;
        }
        DefendPoint pile = standing[Random.Range(0, standing.Count)];
        return new CatRaid.Target { position = pile.position, swat = from => pile.GetHit() };
    }

    // Hungry cats far enough from the target for the run to be seen, the ones in the player's view first
    List<CatOrder> Raiders(Vector3 target)
    {
        Transform eye;
        if (player != null)
        {
            eye = player.eyePoint;
        }
        else
        {
            eye = null;
        }
        List<CatOrder> inView = new List<CatOrder>();
        List<CatOrder> others = new List<CatOrder>();
        foreach (CatOrder order in orders)
        {
            if (order == null || !order.isHungry || order.busy || !order.hasOrdered)
            {
                continue;
            }
            Vector3 gap = target - order.transform.position;
            gap.y = 0;
            if (gap.magnitude < 2.5f)
            {
                continue;
            }
            bool seen = eye == null || Vector3.Angle(Flat(eye.forward), Flat(order.transform.position - eye.position)) < 60f;
            (seen ? inView : others).Add(order);
        }
        Shuffle(inView);
        Shuffle(others);
        inView.AddRange(others);
        return inView;
    }
}
