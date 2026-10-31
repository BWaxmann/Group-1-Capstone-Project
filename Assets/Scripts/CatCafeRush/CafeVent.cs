using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Cat Cafe Waves: a vent in the cafe that cats can come out of. Its lid swings open on its hinge with a clank, a cat
// pokes out and jumps down to the floor (from a high vent it's a long drop, landing in a puff of dust), and the lid
// swings shut again. The RushDirector finds them: any "Vent..." object with a "...Lid" inside it. The lid turns about its
// own pivot (or a parent named "...pivot"); which way it swings, and which side is the room, are worked out from its shape.
public class CafeVent : MonoBehaviour
{
    Transform hinge;
    Quaternion closedRotation;
    Quaternion openRotation;
    Vector3 inside;     // just behind the lid, where a cat starts
    Vector3 mouth;      // just outside the lid
    Vector3 outward;    // the way the vent faces, into the room
    Transform landing;  // on the floor below/in front, facing into the room (cats leave from here as if it were a door)

    public bool busy { get; private set; }
    public Transform exit
    {
        get
        {
            return landing;
        }
    }
    public Vector3 floorSpot
    {
        get
        {
            return landing.position;
        }
    }
    // Just outside the lid, where cats come out
    public Vector3 opening
    {
        get
        {
            return mouth;
        }
    }
    // How far a cat drops from the opening to where it lands
    public float dropHeight
    {
        get
        {
            return mouth.y - landing.position.y;
        }
    }
    // False for a vent whose cats land up on something (a platform, a shelf) and find their own way down
    public bool landsOnFloor { get; private set; }

    // Sets a vent up (once), or null if there's nowhere to land. `viewer` is where the player is (the vent opens towards
    // their side); `floorY` is the height of the floor; `room` is a spot on the floor in front of the player (a cat that
    // lands up on something has to be able to get from there to here)
    public static CafeVent Make(Transform vent, Transform lid, Vector3 viewer, float floorY, Vector3 room)
    {
        CafeVent made = vent.GetComponent<CafeVent>();
        if (made != null) return made.landing != null ? made : null;
        made = vent.gameObject.AddComponent<CafeVent>();
        if (!made.Measure(lid, viewer, floorY, room))
        {
            Destroy(made);
            return null;
        }
        return made;
    }

    bool Measure(Transform lid, Vector3 viewer, float floorY, Vector3 room)
    {
        Mesh lidMesh = LidMesh(lid);
        if (lidMesh == null) return false;
        hinge = lid.parent != null && lid.parent != transform && lid.parent.name.ToLowerInvariant().Contains("pivot") ? lid.parent : lid;

        // The lid's middle, and its three half-widths, in the world
        Bounds local = lidMesh.bounds;
        Vector3 centre = lid.TransformPoint(local.center);
        Vector3[] axes =
        {
            lid.TransformVector(new Vector3(local.extents.x, 0, 0)),
            lid.TransformVector(new Vector3(0, local.extents.y, 0)),
            lid.TransformVector(new Vector3(0, 0, local.extents.z)),
        };
        int thinnest = 0;
        for (int i = 1; i < 3; i++)
        {
            if (axes[i].magnitude < axes[thinnest].magnitude) thinnest = i;
        }
        // Its face points into the room (the player's side)
        Vector3 normal = axes[thinnest].normalized;
        if (Vector3.Dot(normal, viewer - centre) < 0) normal = -normal;

        // It swings about the edge its pivot is on, outwards
        Vector3 fromHinge = centre - hinge.position;
        fromHinge -= Vector3.Dot(fromHinge, normal) * normal;
        Vector3 axis;
        if (fromHinge.magnitude > 0.02f)
        {
            axis = Vector3.Cross(normal, fromHinge).normalized;
        }
        else
        {
            // Pivot in the middle: flips about its longer side
            int other = (thinnest + 1) % 3, third = (thinnest + 2) % 3;
            axis = (axes[other].magnitude > axes[third].magnitude ? axes[other] : axes[third]).normalized;
        }
        closedRotation = hinge.rotation;
        Quaternion swing = Quaternion.AngleAxis(90f, axis);
        Vector3 swungCentre = hinge.position + swing * (centre - hinge.position);
        if (Vector3.Dot(swungCentre - centre, normal) < 0) swing = Quaternion.AngleAxis(-90f, axis);
        openRotation = swing * closedRotation;

        outward = normal;
        inside = centre - normal * 0.25f;
        mouth = centre + normal * 0.3f;

        // Where a cat lands: on the floor a little out from the vent
        Vector3 flat = new Vector3(normal.x, 0, normal.z);
        flat = flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.zero;
        // (further out if something's in the way of the jump, e.g. a shelf under a high vent)
        NavMeshHit floor = new NavMeshHit();
        bool found = false;
        foreach (float distance in new[] { 0.8f, 1.4f, 2f })
        {
            Vector3 target = mouth + flat * distance;
            target.y = floorY;
            if (!NavMesh.SamplePosition(target, out floor, 1.5f, NavMesh.AllAreas)) continue;
            if (Mathf.Abs(floor.position.y - floorY) > 0.3f) continue;
            if (!ClearJump(mouth, floor.position + Vector3.up * 0.3f)) continue;
            found = true;
            break;
        }
        landsOnFloor = found;
        if (!found)
        {
            // No floor to drop to (e.g. a vent on a platform): it lands on whatever's just below the opening, as long as
            // there's a way from there down into the room (the cats' jump-ways)
            if (!NavMesh.SamplePosition(mouth + flat * 0.5f + Vector3.down * 0.4f, out floor, 1f, NavMesh.AllAreas)) return false;
            if (floor.position.y > mouth.y + 0.1f || floor.position.y < floorY + 0.5f) return false;
            if (!ClearJump(mouth, floor.position + Vector3.up * 0.3f)) return false;
            NavMeshPath way = new NavMeshPath();
            if (!NavMesh.CalculatePath(floor.position, room, NavMesh.AllAreas, way) || way.status != NavMeshPathStatus.PathComplete) return false;
        }

        landing = new GameObject("Vent Landing").transform;
        landing.SetParent(transform, true);
        Vector3 into = flat != Vector3.zero ? flat : new Vector3(viewer.x - floor.position.x, 0, viewer.z - floor.position.z);
        landing.SetPositionAndRotation(floor.position, Quaternion.LookRotation(into.sqrMagnitude > 0.01f ? into : Vector3.forward));
        return true;
    }

    // The lid's own mesh. In Play mode Unity merges static scenery into one big combined mesh (static batching), which
    // also can't move: then the lid's shape comes from its collider, and a moving copy of it takes over the drawing
    static Mesh LidMesh(Transform lid)
    {
        MeshFilter shape = lid != null ? lid.GetComponent<MeshFilter>() : null;
        if (shape == null || shape.sharedMesh == null) return null;
        if (!shape.sharedMesh.name.StartsWith("Combined Mesh")) return shape.sharedMesh;

        MeshCollider solid = lid.GetComponent<MeshCollider>();
        MeshRenderer drawn = lid.GetComponent<MeshRenderer>();
        if (solid == null || solid.sharedMesh == null || drawn == null) return null;
        GameObject copy = new GameObject("Lid (moves)");
        copy.transform.SetParent(lid, false);
        copy.AddComponent<MeshFilter>().sharedMesh = solid.sharedMesh;
        copy.AddComponent<MeshRenderer>().sharedMaterials = drawn.sharedMaterials;
        drawn.enabled = false;
        return solid.sharedMesh;
    }

    // Nothing solid between the vent and where it lands (the vent itself, cats and props don't count)
    bool ClearJump(Vector3 from, Vector3 to)
    {
        Vector3 between = to - from;
        foreach (RaycastHit hit in Physics.RaycastAll(from, between.normalized, between.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Transform other = hit.collider.transform;
            if (other.IsChildOf(transform) || other.GetComponentInParent<CatMovement>() != null || other.GetComponentInParent<FlyingProp>() != null) continue;
            return false;
        }
        return true;
    }

    // Brings a pooled cat (switched off) out of the vent and down to the floor, then hands it over (`scale` shrinks it on
    // the way, e.g. for a kitten; it's back to its own size when it's handed over)
    public IEnumerator Release(CatMovement cat, float scale, Action<CatMovement, CafeVent> landed)
    {
        busy = true;
        Hide(cat, scale);
        yield return Open();
        yield return Drop(cat, scale, landed);
        yield return new WaitForSeconds(0.3f);
        yield return Close();
    }

    // Clank! The lid swings open (and stays open until Close: cats can Drop out meanwhile)
    public IEnumerator Open()
    {
        busy = true;
        CatChaosSettings sounds = CatChaosSettings.instance;
        if (sounds != null) sounds.PlayKnock(mouth);
        yield return Swing(closedRotation, openRotation, 0.2f);
    }

    public IEnumerator Close()
    {
        yield return Swing(openRotation, closedRotation, 0.25f);
        CatChaosSettings sounds = CatChaosSettings.instance;
        if (sounds != null) sounds.PlayKnock(mouth);
        busy = false;
    }

    // Takes a pooled cat out of the pool straight away (waiting inside the vent, shrunk by `scale`), so nothing else
    // takes it. Drop does this itself if it hasn't been done
    public void Hide(CatMovement cat, float scale)
    {
        if (waiting.ContainsKey(cat)) return;
        NavMeshAgent agent = cat.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        waiting[cat] = cat.transform.localScale;
        Vector3 look = landing.position - mouth;
        look.y = 0;
        cat.transform.localScale = cat.transform.localScale * scale;
        cat.treatGate = NoTreats;   // not a customer yet
        cat.wantsTreats = () => false;   // treats fly straight past
        cat.transform.SetPositionAndRotation(inside, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : landing.forward));
        cat.gameObject.SetActive(true);
        cat.TakeOver();
    }

    // Each cat waiting in the vent, and its own size
    readonly System.Collections.Generic.Dictionary<CatMovement, Vector3> waiting = new System.Collections.Generic.Dictionary<CatMovement, Vector3>();

    // A cat out through the open vent: it pokes out and jumps down to the floor, then it's handed over. Kittens (a scale
    // below 1) are quicker about it and squeak
    public IEnumerator Drop(CatMovement cat, float scale, Action<CatMovement, CafeVent> landed)
    {
        Hide(cat, scale);
        Vector3 ownSize = waiting[cat];
        bool kitten = scale < 0.9f;

        // It pokes out, looking into the room
        cat.Meow(kitten ? Random.Range(1.5f, 1.9f) : Random.Range(0.95f, 1.1f));
        float emerge = kitten ? 0.15f : 0.35f;
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / emerge;
            cat.transform.position = Vector3.Lerp(inside, mouth, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }

        // ...and jumps down: a little hop up, then down it drops (longer from higher up)
        Vector3 from = mouth;
        Vector3 to = landing.position;
        float drop = Mathf.Max(0, from.y - to.y);
        float seconds = Mathf.Clamp(0.4f + drop * 0.13f, 0.45f, 1f);
        SetJumpingDown(cat, true, seconds);
        t = 0;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / seconds);
            Vector3 position = Vector3.Lerp(from, to, t);
            position.y = Mathf.Lerp(from.y, to.y, t * t) + 4f * t * (1f - t) * 0.25f;
            cat.transform.position = position;
            cat.transform.rotation = Quaternion.Slerp(cat.transform.rotation, landing.rotation, Time.deltaTime * 6f);
            yield return null;
        }
        SetJumpingDown(cat, false, seconds);

        // Lands in a puff of dust, and off it goes (a stream of kittens doesn't thud every time)
        float landing01 = Mathf.Clamp01(drop / 4f);
        cat.DustPuff(kitten ? 0.5f : Mathf.Lerp(0.7f, 1.1f, landing01));
        CatChaosSettings sounds = CatChaosSettings.instance;
        if (sounds != null && !kitten) sounds.PlayLanded(to);
        cat.treatGate = null;
        cat.wantsTreats = null;
        cat.transform.localScale = ownSize;
        waiting.Remove(cat);
        landed?.Invoke(cat, this);
        CatJuice juice = cat.GetComponent<CatJuice>();
        if (juice != null && !kitten) juice.Hit(Mathf.Lerp(0.5f, 1f, landing01));
    }

    static bool NoTreats(Treat treat)
    {
        return false;
    }

    IEnumerator Swing(Quaternion from, Quaternion to, float seconds)
    {
        float t = 0;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / seconds);
            // Overshoots a touch and settles, like a flap on a spring
            float eased = 1f + 2.2f * Mathf.Pow(t - 1f, 3) + 1.2f * Mathf.Pow(t - 1f, 2);
            hinge.rotation = Quaternion.SlerpUnclamped(from, to, eased);
            yield return null;
        }
        hinge.rotation = to;
    }

    static readonly int JumpingDown = Animator.StringToHash("JumpingDown");
    static readonly int JumpSpeed = Animator.StringToHash("JumpSpeed");

    // The cat's jump-down animation (cats that have one), played over the drop
    static void SetJumpingDown(CatMovement cat, bool on, float seconds)
    {
        foreach (Animator animator in cat.GetComponentsInChildren<Animator>())
        {
            if (animator.runtimeAnimatorController == null) continue;
            bool hasJump = false, hasSpeed = false;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == JumpingDown) hasJump = true;
                if (parameter.nameHash == JumpSpeed) hasSpeed = true;
            }
            if (!hasJump) continue;
            animator.SetBool(JumpingDown, on);
            if (on && hasSpeed) animator.SetFloat(JumpSpeed, 1.2f / seconds);
        }
    }

    // When the round ends mid-drop
    void OnDisable()
    {
        if (hinge != null) hinge.rotation = closedRotation;
        busy = false;
    }
}
