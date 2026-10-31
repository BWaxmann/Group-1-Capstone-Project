using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Rush Director, the tip jar: placed on the counter beside the cat clock, in view and out of the way of shots
public partial class RushDirector : MonoBehaviour
{
    TipJar tipJar;

    // On the counter beside the cat clock (it stands on the counter), on whichever side is nearer the player's front
    // right, so it's in view but out of the way of shots at the room. A Tip Jar Spot, if set, wins.
    void PlaceTipJar()
    {
        Transform eye;
        if (player.eyePoint != null)
        {
            eye = player.eyePoint;
        }
        else if (Camera.main != null)
        {
            eye = Camera.main.transform;
        }
        else
        {
            eye = null;
        }
        Vector3 head;
        if (eye != null)
        {
            head = eye.position;
        }
        else
        {
            head = player.transform.position + Vector3.up * 1.6f;
        }
        if (tipJarSpot != null)
        {
            tipJar = TipJar.Create(tipJarSpot.position, head, tipJarScale);
            return;
        }

        // In place of something already standing there (e.g. a bottle on the counter), which is put away. Several
        // things can share a name: it's the one nearest the player.
        GameObject replaced = null;
        if (!string.IsNullOrEmpty(tipJarReplaces))
        {
            float nearest = float.MaxValue;
            foreach (Transform thing in FindObjectsOfType<Transform>())
            {
                if (thing.name != tipJarReplaces)
                {
                    continue;
                }
                float distance = Flat(thing.position - head).magnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    replaced = thing.gameObject;
                }
            }
        }
        if (replaced != null)
        {
            Bounds bounds = new Bounds(replaced.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer rend in replaced.GetComponentsInChildren<Renderer>())
            {
                if (!any)
                {
                    bounds = rend.bounds;
                }
                else
                {
                    bounds.Encapsulate(rend.bounds);
                }
                any = true;
            }
            replaced.SetActive(false);
            tipJar = TipJar.Create(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), head, tipJarScale);
            return;
        }
        if (!string.IsNullOrEmpty(tipJarReplaces))
        {
            Debug.LogWarning($"[Cat Cafe Waves] No \"{tipJarReplaces}\" in the scene for the tip jar to replace.");
        }

        Vector3 forward = player.transform.forward;
        forward.y = 0;
        if (forward.sqrMagnitude > 0.001f)
        {
            forward = forward.normalized;
        }
        else
        {
            forward = Vector3.forward;
        }
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 wanted = head + forward * 0.8f + right * 0.5f;

        Vector3? spot = BesideTheClock(wanted);
        if (spot == null)
        {
            Debug.LogWarning("[Cat Cafe Waves] No Cat_clock on a counter to put the tip jar beside; it's guessing the counter height.");
            spot = new Vector3(wanted.x, head.y - 0.65f, wanted.z);
        }
        tipJar = TipJar.Create(spot.Value, head, tipJarScale);
    }

    Vector3? BesideTheClock(Vector3 wanted)
    {
        GameObject catClock = GameObject.Find("Cat_clock");
        if (catClock == null)
        {
            return null;
        }

        // The counter top is where the clock's base sits
        float top = catClock.transform.position.y;
        bool any = false;
        foreach (Renderer rend in catClock.GetComponentsInChildren<Renderer>())
        {
            if (rend.GetComponentInParent<Canvas>() != null)
            {
                continue;
            }
            if (any)
            {
                top = Mathf.Min(top, rend.bounds.min.y);
            }
            else
            {
                top = rend.bounds.min.y;
            }
            any = true;
        }
        Vector3 centre = catClock.transform.position;
        centre.y = top;

        // Spots around the clock where the counter is clear (a ray down lands on a surface at the counter's height),
        // the one nearest where we'd like it
        Vector3? best = null;
        float bestDistance = float.MaxValue;
        for (float distance = 0.3f; distance <= 0.61f; distance += 0.15f)
        {
            for (int angle = 0; angle < 360; angle += 30)
            {
                Vector3 spot = centre + Quaternion.Euler(0, angle, 0) * Vector3.forward * distance;
                if (!ClearCounterAt(spot, top, catClock.transform))
                {
                    continue;
                }
                float off = new Vector2(spot.x - wanted.x, spot.z - wanted.z).magnitude;
                if (off < bestDistance)
                {
                    bestDistance = off;
                    best = spot;
                }
            }
        }

        // Nothing to test against (no colliders): just beside the clock, towards where we'd like it
        if (best == null)
        {
            Vector3 towards = wanted - centre;
            towards.y = 0;
            best = centre + (towards.sqrMagnitude > 0.001f ? towards.normalized : Vector3.right) * 0.3f;
        }
        return best;
    }

    static bool ClearCounterAt(Vector3 spot, float top, Transform clock)
    {
        RaycastHit[] hits = Physics.RaycastAll(spot + Vector3.up * 0.6f, Vector3.down, 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float highest = float.MinValue;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(clock) || hit.collider.GetComponentInParent<CatMovement>() != null)
            {
                continue;
            }
            highest = Mathf.Max(highest, hit.point.y);
        }
        // The highest thing there is the counter top itself (not the floor below it, and nothing standing on it)
        return Mathf.Abs(highest - top) < 0.04f;
    }
}
