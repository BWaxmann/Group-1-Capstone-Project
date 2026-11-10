using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Rush Director, the kitten stampede: herds galloping across the floor from the sides, and floods pouring out of the vents
public partial class RushDirector : MonoBehaviour
{
    bool flooding;

    // Herds of kittens gallop across the room in front of the player, from alternating sides, for the whole phase
    IEnumerator StampedeRoutine(Phase phase)
    {
        float end = clock + phase.seconds;
        if (stampedeScatter && player != null)
        {
            player.ForceFireStyle(PlayerInput.FireStyle.Scatter);
        }
        yield return WaitClockRoutine(1.5f);   // the callout lands first

        bool fromLeft = Random.value < 0.5f;
        bool first = true;
        while (playing && clock < end - 3f)
        {
            // Now and then (and first off) the herd floods in through a vent up high
            CafeVent vent;
            if ((first && firstHerdFloods) || Random.value < floodChance)
            {
                vent = FloodVent();
            }
            else
            {
                vent = null;
            }
            first = false;
            List<Vector3> path;
            if (vent != null)
            {
                path = FloodPath(vent.floorSpot);
            }
            else
            {
                path = null;
            }
            if (path != null)
            {
                StartCoroutine(FloodRoutine(vent, path));
            }
            else
            {
                path = RoamPath(fromLeft, Random.Range(herdRoamMin, Mathf.Max(herdRoamMin, herdRoamMax)));
                if (path != null)
                {
                    StartCoroutine(SendHerdRoutine(path));
                }
            }
            fromLeft = !fromLeft;
            yield return WaitClockRoutine(Random.Range(herdGapMin, Mathf.Max(herdGapMin, herdGapMax)));
        }
        while (playing && clock < end)
        {
            yield return null;
        }
        // (kittens still running get to finish: see WrapUp)
    }

    IEnumerator SendHerdRoutine(List<Vector3> path)
    {
        int count = Random.Range(kittensPerHerdMin, Mathf.Max(kittensPerHerdMin, kittensPerHerdMax) + 1);
        count = Mathf.Min(count, maxKittens - KittenRun.running.Count);
        float speed = Random.Range(kittenSpeedMin, Mathf.Max(kittenSpeedMin, kittenSpeedMax));   // a herd keeps together
        LogHerd("Herd", count, path);
        int sent = 0;
        for (int i = 0; i < count && playing; i++)
        {
            CatMovement cat;
            if (catPool != null)
            {
                cat = catPool.GetRandomObjectFromPool<CatMovement>();
            }
            else
            {
                cat = null;
            }
            if (cat != null)
            {
                KittenRun.Launch(cat, path, Random.Range(-0.6f, 0.6f), speed * Random.Range(0.92f, 1.08f),
                                 kittenScale, kittenHitRadius, KittenFed);
                sent++;
                if (i == 0)
                {
                    cat.DustPuff(KittenRun.DustSize);   // a puff of dust as the herd bursts in
                }
            }
            yield return new WaitForSeconds(Random.Range(0.12f, 0.3f));
        }
        if (sent < count)
        {
            Debug.LogWarning($"[Cat Cafe Waves] Herd: the cat pool gave {sent} of {count} kitten(s).");
        }
    }

    // A herd pouring out of a vent: the lid clanks open, the kittens drop out one after another and tear off along `path`
    IEnumerator FloodRoutine(CafeVent vent, List<Vector3> path)
    {
        int count = Random.Range(kittensPerHerdMin, Mathf.Max(kittensPerHerdMin, kittensPerHerdMax) + 1);
        count = Mathf.Min(count, maxKittens - KittenRun.running.Count);
        float speed = Random.Range(kittenSpeedMin, Mathf.Max(kittenSpeedMin, kittenSpeedMax));
        float spread = 0.6f;
        LogHerd("Flood", count, path);
        yield return vent.OpenRoutine();
        for (int i = 0; i < count && playing; i++)
        {
            CatMovement cat;
            if (catPool != null)
            {
                cat = catPool.GetRandomObjectFromPool<CatMovement>();
            }
            else
            {
                cat = null;
            }
            if (cat == null)
            {
                break;
            }
            float offset = Random.Range(-spread, spread);
            float kittenSpeed = speed * Random.Range(0.92f, 1.08f);
            vent.StartCoroutine(vent.DropRoutine(cat, kittenScale, (kitten, from) =>
            {
                if (playing && current != null && (current.kind == Kind.Stampede || current.kind == Kind.Remix))
                {
                    KittenRun.Launch(kitten, path, offset, kittenSpeed, kittenScale, kittenHitRadius, KittenFed);
                }
                else
                {
                    kitten.DustPuff(KittenRun.DustSize);
                    kitten.gameObject.SetActive(false);
                }
            }));
            yield return new WaitForSeconds(Random.Range(0.14f, 0.26f));
        }
        yield return new WaitForSeconds(1.2f);   // the last ones land
        yield return vent.CloseRoutine();
    }

    // The remix finale's kitten flood, partway through (when there's room for it)
    IEnumerator RemixFloodRoutine(Phase phase)
    {
        float at = clock + phase.seconds * remixFloodAt;
        while (playing && current == phase && (clock < at || !RoomForMore))
        {
            yield return null;
        }
        if (!playing || current != phase || windingDown)
        {
            yield break;
        }
        CafeVent vent = FloodVent();
        List<Vector3> path;
        if (vent != null)
        {
            path = FloodPath(vent.floorSpot);
        }
        else
        {
            path = null;
        }
        if (path == null)
        {
            yield break;
        }
        BigStarted();
        flooding = true;
        yield return FloodRoutine(vent, path);
        flooding = false;
    }

    // A vent for a flood: the highest free one (kittens raining down from the ceiling) that drops to the floor; null if
    // none is free
    CafeVent FloodVent()
    {
        CafeVent best = null;
        foreach (CafeVent vent in vents)
        {
            if (vent == null || vent.busy || !vent.landsOnFloor)
            {
                continue;
            }
            if (best == null || vent.dropHeight > best.dropHeight)
            {
                best = vent;
            }
        }
        return best;
    }

    // Where a flood goes from the spot under its vent: off across the floor in front of the player to one side
    // One Console line per herd: inside Liminal's loader the kittens never showed, and nothing said why
    void LogHerd(string kind, int count, List<Vector3> path)
    {
        float length = 0;
        for (int i = 1; i < path.Count; i++)
        {
            length += Vector3.Distance(path[i - 1], path[i]);
        }
        string from = "nowhere";
        if (path.Count > 0)
        {
            from = path[0].ToString();
        }
        bool poolReady = catPool != null && catPool.isReady;
        Debug.Log($"[Cat Cafe Waves] {kind}: {count} kitten(s) along {path.Count} point(s), {length:F1} m from {from}; pool ready {poolReady}, {KittenRun.running.Count} running.");
    }

    List<Vector3> FloodPath(Vector3 from)
    {
        if (player == null)
        {
            return null;
        }
        NavMeshPath navPath = new NavMeshPath();
        Vector3 at = player.transform.position;
        Vector3 forward = Flat(player.transform.forward);
        if (forward.sqrMagnitude > 0.001f)
        {
            forward = forward.normalized;
        }
        else
        {
            forward = Vector3.forward;
        }
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 away = at + forward * Random.Range(2.5f, 5f) + right * Random.Range(3.5f, 5.5f) * (Random.value < 0.5f ? 1f : -1f);
            if (!NavMesh.SamplePosition(away, out NavMeshHit finish, 2f, NavMesh.AllAreas))
            {
                continue;
            }
            if (Mathf.Abs(finish.position.y - from.y) > 0.3f)
            {
                continue;
            }
            if (!NavMesh.CalculatePath(from, finish.position, NavMesh.AllAreas, navPath) ||
                navPath.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }
            float length = 0;
            for (int i = 1; i < navPath.corners.Length; i++)
            {
                length += Vector3.Distance(navPath.corners[i - 1], navPath.corners[i]);
            }
            if (length > Vector3.Distance(from, finish.position) * 2f + 2f)
            {
                continue;   // no big detours round the room
            }
            return new List<Vector3>(navPath.corners);
        }
        return null;
    }

    void KittenFed(KittenRun kitten)
    {
        Vector3 at = kitten.aimPoint;
        GameManager.instance.FeedCat(kittenScore, at);
        if (tipJar != null && kittenCoins > 0)
        {
            tipJar.AddCoins(at, kittenCoins);
        }
    }

    // A walkable run across the floor in front of the player, from one side to the other (around furniture, never
    // through the player's space), or null if none could be found
    List<Vector3> HerdPath(bool fromLeft)
    {
        if (player == null)
        {
            return null;
        }
        Transform eye = player.eyePoint;
        Vector3 head;
        if (eye != null)
        {
            head = eye.position;
        }
        else
        {
            head = player.transform.position;
        }
        Vector3 forward = Flat(player.transform.forward);
        if (forward.sqrMagnitude > 0.001f)
        {
            forward = forward.normalized;
        }
        else
        {
            forward = Vector3.forward;
        }
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float floorY = player.transform.position.y;
        NavMeshPath navPath = new NavMeshPath();

        for (int attempt = 0; attempt < 8; attempt++)
        {
            float depth = Random.Range(2.5f, 6.5f);
            float width = Random.Range(3.5f, 5.5f);
            Vector3 centre = head + forward * depth;
            centre.y = floorY;
            Vector3 side = right * width * (fromLeft ? 1f : -1f);
            if (!NavMesh.SamplePosition(centre - side, out NavMeshHit start, 2f, NavMesh.AllAreas))
            {
                continue;
            }
            if (!NavMesh.SamplePosition(centre + side, out NavMeshHit finish, 2f, NavMesh.AllAreas))
            {
                continue;
            }
            // On the floor, not up on a table
            if (Mathf.Abs(start.position.y - floorY) > 0.3f || Mathf.Abs(finish.position.y - floorY) > 0.3f)
            {
                continue;
            }
            if (!NavMesh.CalculatePath(start.position, finish.position, NavMesh.AllAreas, navPath) ||
                navPath.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            float length = 0;
            for (int i = 1; i < navPath.corners.Length; i++)
            {
                length += Vector3.Distance(navPath.corners[i - 1], navPath.corners[i]);
            }
            float straight = Vector3.Distance(start.position, finish.position);
            // Long enough to see, and not a big detour round the room
            if (straight < 4f || length > straight * 1.8f)
            {
                continue;
            }
            return new List<Vector3>(navPath.corners);
        }
        return null;
    }

    // A herd's run that zig-zags about in front of the player for about `seconds` (the time to chase them down) before
    // heading off out the other side: in from one side, from spot to spot across the floor in front of them, then away.
    // Just across if no spots can be found.
    List<Vector3> RoamPath(bool fromLeft, float seconds)
    {
        List<Vector3> across = HerdPath(fromLeft);
        if (across == null || seconds <= 0 || player == null)
        {
            return across;
        }
        Transform eye = player.eyePoint;
        Vector3 head;
        if (eye != null)
        {
            head = eye.position;
        }
        else
        {
            head = player.transform.position;
        }
        Vector3 forward = Flat(player.transform.forward);
        if (forward.sqrMagnitude > 0.001f)
        {
            forward = forward.normalized;
        }
        else
        {
            forward = Vector3.forward;
        }
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float floorY = across[0].y;
        float wanted = seconds * (kittenSpeedMin + kittenSpeedMax) * 0.5f;

        List<Vector3> route = new List<Vector3> { across[0] };
        Vector3 at = across[0];
        float length = 0;
        NavMeshPath leg = new NavMeshPath();
        for (int tries = 0; tries < 40 && length < wanted; tries++)
        {
            Vector3 spot = head + forward * Random.Range(2.2f, 6f) + right * Random.Range(-3.5f, 3.5f);
            spot.y = floorY;
            if (!NavMesh.SamplePosition(spot, out NavMeshHit floor, 1f, NavMesh.AllAreas) || Mathf.Abs(floor.position.y - floorY) > 0.3f)
            {
                continue;
            }
            float straight = Flat(floor.position - at).magnitude;
            if (straight < 1.8f)
            {
                continue;   // a proper dash, not a shuffle
            }
            if (!NavMesh.CalculatePath(at, floor.position, NavMesh.AllAreas, leg) || leg.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }
            float legLength = 0;
            for (int i = 1; i < leg.corners.Length; i++)
            {
                legLength += Vector3.Distance(leg.corners[i - 1], leg.corners[i]);
            }
            if (legLength > straight * 1.6f + 0.5f)
            {
                continue;   // not the long way round the furniture
            }
            for (int i = 1; i < leg.corners.Length; i++)
            {
                route.Add(leg.corners[i]);
            }
            length += legLength;
            at = floor.position;
        }
        if (route.Count < 2)
        {
            return across;
        }
        // ...then away out the far side
        Vector3 exit = across[across.Count - 1];
        if (!NavMesh.CalculatePath(at, exit, NavMesh.AllAreas, leg) || leg.status != NavMeshPathStatus.PathComplete)
        {
            return across;
        }
        for (int i = 1; i < leg.corners.Length; i++)
        {
            route.Add(leg.corners[i]);
        }
        return route;
    }
}
