using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Rush Director, the vents: found when the round starts (only the ones the player can see), handed out to whoever needs one
public partial class RushDirector : MonoBehaviour
{
    // Vents high up (in the ceiling) that the player can see: where spies come down from
    List<CafeVent> CeilingVents()
    {
        return vents.FindAll(vent => vent != null && vent.dropHeight > 1.5f);
    }

    readonly List<CafeVent> vents = new List<CafeVent>();
    int droppingIn;   // cats on their way out of a vent

    // Every vent with a lid ("Vent..." objects with a "...Lid" inside) that has floor to land on
    void FindVents()
    {
        vents.Clear();
        if (player == null)
        {
            return;
        }
        Transform eye = player.eyePoint;
        Vector3 viewer;
        if (eye != null)
        {
            viewer = eye.position;
        }
        else
        {
            viewer = player.transform.position + Vector3.up * 1.5f;
        }
        float floorY = player.transform.position.y;
        if (NavMesh.SamplePosition(player.transform.position, out NavMeshHit floor, 2f, NavMesh.AllAreas))
        {
            floorY = floor.position.y;
        }
        // A spot on the floor in front of the player that cats coming out of a vent must be able to reach
        Vector3 forward = Flat(player.transform.forward);
        if (forward.sqrMagnitude > 0.001f)
        {
            forward = forward.normalized;
        }
        else
        {
            forward = Vector3.forward;
        }
        Vector3 room = player.transform.position + forward * 2.5f;
        if (NavMesh.SamplePosition(room, out NavMeshHit roomFloor, 2f, NavMesh.AllAreas))
        {
            room = roomFloor.position;
        }
        foreach (MeshFilter lid in FindObjectsOfType<MeshFilter>())
        {
            if (!lid.name.ToLowerInvariant().Contains("lid"))
            {
                continue;
            }
            // The vent it belongs to: the nearest thing above it called Vent...
            Transform vent = lid.transform.parent;
            while (vent != null && !(vent.name.ToLowerInvariant().StartsWith("vent") && !vent.name.ToLowerInvariant().Contains("lid")))
            {
                vent = vent.parent;
            }
            if (vent == null)
            {
                continue;
            }
            CafeVent made = CafeVent.Make(vent, lid.transform, viewer, floorY, room);
            if (made == null || vents.Contains(made))
            {
                continue;
            }
            // Only vents the player can see (a cat out of one hidden behind the bar would seem to come from nowhere). A vent
            // set into the ceiling counts if the space just below it can be seen (its opening is up in the ceiling itself)
            Vector3 below = Vector3.Lerp(made.opening, made.floorSpot + Vector3.up * 0.5f, 0.3f);
            if (CanSee(viewer, made.opening) || CanSee(viewer, below))
            {
                vents.Add(made);
            }
        }
        Debug.Log($"[Cat Cafe Waves] Cats can come out of {vents.Count} vent(s).");
    }

    // A vent nobody's coming out of, within reach of the player's view (null if none); `floorOnly` leaves out vents whose
    // cats land up on something
    CafeVent FreeVent(bool floorOnly = false)
    {
        if (!catsUseVents || vents.Count == 0 || player == null)
        {
            return null;
        }
        Vector3 at = player.transform.position;
        Vector3 forward = Flat(player.transform.forward);
        List<CafeVent> free = vents.FindAll(vent =>
            vent != null && !vent.busy && (!floorOnly || vent.landsOnFloor) &&
            (forward.sqrMagnitude < 0.001f || Vector3.Angle(forward, Flat(vent.floorSpot - at)) <= ventArc));
        if (free.Count > 0)
        {
            return free[Random.Range(0, free.Count)];
        }
        return null;
    }
}
