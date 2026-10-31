using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Rush Director, the launcher kittens: now and then one bolts at the player and latches onto a launcher
public partial class RushDirector : MonoBehaviour
{
    float nextLatch;

    // Every so often (one at a time) a kitten bolts in from a door the player can see and leaps onto a launcher
    void UpdateLatches()
    {
        if (LauncherKitten.active.Count > 0 || clock < nextLatch || !RoomForMore || !MayIntroduce(latchLine))
        {
            return;
        }
        nextLatch = clock + 1f;   // couldn't send one: try again in a moment
        if (catPool == null)
        {
            return;
        }

        // Out of a vent sometimes: it drops down, then makes its run
        CafeVent vent;
        if (Random.value < kittenVentChance)
        {
            vent = FreeVent(true);
        }
        else
        {
            vent = null;
        }
        List<Vector3> path;
        if (vent != null)
        {
            path = LatchPath(vent.floorSpot);
        }
        else
        {
            path = null;
        }
        if (path != null)
        {
            CatMovement fromVent = catPool.GetRandomObjectFromPool<CatMovement>();
            if (fromVent == null)
            {
                return;
            }
            nextLatch = float.MaxValue;   // set again when it's over
            BigStarted();
            vent.StartCoroutine(vent.ReleaseRoutine(fromVent, launcherKitten.runScale, (cat, landedFrom) =>
            {
                if (playing && current != null && Feeding(current.kind) && current.latchEvery > 0)
                {
                    LauncherKitten.Send(cat, path, player, launcherKitten, KittenLatched, LatchOver);
                }
                else
                {
                    cat.DustPuff(KittenRun.DustSize);
                    cat.gameObject.SetActive(false);
                    LatchOver(null, LauncherKitten.Result.Cancelled);
                }
            }));
            return;
        }

        path = LatchPath(null);
        if (path == null)
        {
            return;
        }
        CatMovement kitten = catPool.GetRandomObjectFromPool<CatMovement>();
        if (kitten == null)
        {
            return;
        }
        LauncherKitten.Send(kitten, path, player, launcherKitten, KittenLatched, LatchOver);
        kitten.DustPuff(KittenRun.DustSize);
        nextLatch = float.MaxValue;   // set again when it's over
        BigStarted();
    }

    void KittenLatched(LauncherKitten kitten)
    {
        SayOnce(latchLine);
    }

    void LatchOver(LauncherKitten kitten, LauncherKitten.Result result)
    {
        if (current != null)
        {
            nextLatch = clock + current.latchEvery * Random.Range(0.8f, 1.2f);
        }
    }

    // From `from` (or a door in front of the player) to the floor just outside the player's space
    List<Vector3> LatchPath(Vector3? from)
    {
        List<Transform> ways;
        if (entryDoors.Count > 0)
        {
            ways = entryDoors;
        }
        else
        {
            ways = doors;
        }
        if (player == null || (from == null && ways.Count == 0))
        {
            return null;
        }
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
        float front = Mathf.Max(0.5f, playerSpace.z * 0.5f) + 0.4f;
        NavMeshPath navPath = new NavMeshPath();
        for (int attempt = 0; attempt < 6; attempt++)
        {
            Transform door;
            if (from == null)
            {
                door = ways[Random.Range(0, ways.Count)];
            }
            else
            {
                door = null;
            }
            if (from == null && door == null)
            {
                continue;
            }
            Vector3 spot = at + forward * front + right * Random.Range(-0.5f, 0.5f);
            if (!NavMesh.SamplePosition(spot, out NavMeshHit end, 1.2f, NavMesh.AllAreas))
            {
                continue;
            }
            if (Mathf.Abs(end.position.y - at.y) > 0.3f)
            {
                continue;   // on the floor, not up on something
            }
            if (!NavMesh.SamplePosition(from ?? door.position, out NavMeshHit start, 1.5f, NavMesh.AllAreas))
            {
                continue;
            }
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, navPath) ||
                navPath.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }
            return new List<Vector3>(navPath.corners);
        }
        return null;
    }
}
