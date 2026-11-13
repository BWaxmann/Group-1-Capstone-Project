using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Rush Director, feeding: customers come in by the holes and the vents, order where the player can see them, and are served, storm off or run out of patience
public partial class RushDirector : MonoBehaviour
{
    readonly List<CatOrder> orders = new List<CatOrder>();
    float lastArrival = -100f;
    readonly List<CatOrder.Hole> holes = new List<CatOrder.Hole>();
    readonly List<Transform> entryDoors = new List<Transform>();

    // Where a treat should curve to: a hungry cat wanting its colour, or the ninja boss while it can be fed
    Vector3? AssistTarget(Treat treat)
    {
        Vector3? target = CatOrder.AssistTarget(treat, aimAssistAngle);
        if (target == null && KittenRun.running.Count > 0)
        {
            // Any kitten will do (no colours in the stampede)
            float best = aimAssistAngle;
            foreach (KittenRun kitten in KittenRun.running)
            {
                if (!kitten.isFed && kitten.feedable)
                {
                    Consider(kitten.aimPoint);
                }
            }

            void Consider(Vector3 point)
            {
                float angle = Vector3.Angle(treat.velocity, point - treat.transform.position);
                if (angle <= best)
                {
                    best = angle;
                    target = point;
                }
            }
        }
        if (target == null && ninja != null && ninja.feedable && ninja.gameObject.activeInHierarchy && !ninja.isFullyFed)
        {
            Vector3 point;
            if (ninjaHitbox != null)
            {
                point = ninjaHitbox.transform.TransformPoint(ninjaHitbox.center);
            }
            else
            {
                point = ninja.transform.position + Vector3.up * 0.3f;
            }
            if (Vector3.Angle(treat.velocity, point - treat.transform.position) <= aimAssistAngle)
            {
                target = point;
            }
        }
        return target;
    }

    void SendInCat()
    {
        if (catPool == null)
        {
            return;
        }
        // Now and then through a vent
        CafeVent vent;
        if (Random.value < ventChance)
        {
            vent = FreeVent();
        }
        else
        {
            vent = null;
        }
        List<Transform> ways;
        if (entryDoors.Count > 0)
        {
            ways = entryDoors;
        }
        else
        {
            ways = doors;
        }
        if (vent == null && ways.Count == 0)
        {
            return;
        }
        CatMovement cat = catPool.GetRandomObjectFromPool<CatMovement>();
        if (cat == null)
        {
            return;
        }
        lastArrival = Time.time;

        if (vent != null)
        {
            droppingIn++;
            vent.StartCoroutine(vent.ReleaseRoutine(cat, 1f, OutOfVent));
            return;
        }
        Transform door = ways[Random.Range(0, ways.Count)];
        Vector3 position = door.position;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
        {
            position = hit.position;
        }
        Arrive(cat, door, position);
    }

    // A cat that came out of a vent has landed: in it comes (unless its chance has passed)
    void OutOfVent(CatMovement cat, CafeVent vent)
    {
        droppingIn = Mathf.Max(0, droppingIn - 1);
        if (!playing || current == null || !HasOrders(current.kind))
        {
            cat.DustPuff(1f);
            cat.gameObject.SetActive(false);
            return;
        }
        Arrive(cat, vent.exit, vent.floorSpot);
    }

    // A customer walks in at `position` (facing the way `door` does) and is given its order
    void Arrive(CatMovement cat, Transform door, Vector3 position)
    {
        cat.gameObject.SetActive(true);
        cat.Initialise(door, position);

        int length = Random.Range(current.shortestOrder, current.longestOrder + 1);
        List<CatOrder.Food> foods = new List<CatOrder.Food>();
        for (int i = 0; i < length; i++)
        {
            bool left = Random.value < 0.5f;
            foods.Add(new CatOrder.Food
            {
                variation = left ? leftFood : rightFood,
                icon = new OrderBubble.Item(left ? leftTreatPrefab : rightTreatPrefab, left ? leftColour : rightColour)
            });
        }
        CatOrder order = CatOrder.Give(cat, foods, bubbleScale, cleanShots ? hitboxGenerosity : 0f, catSpeedMultiplier);
        order.served = Served;
        order.holeFor = null;
        if (fedCatsUseHoles && holes.Count > 0)
        {
            order.holeFor = NearestHole;
        }
        if (orderMoments)
        {
            order.orderSpotFor = OrderSpot;
        }
        else
        {
            order.orderSpotFor = (Func<CatOrder, Vector3?>)null;
        }
        order.wrongTreat = WrongTreat;
        order.patienceOut = PatienceOut;
        if (orderMoments)
        {
            order.OrderLater(Random.Range(orderDelayMin, Mathf.Max(orderDelayMin, orderDelayMax)),
                             patience > 0 ? patience + patiencePerExtraTreat * (length - 1) : 0f);
        }
        orders.Add(order);
        lastArrival = Time.time;
    }

    // Spots in front of the player (on the floor, and up on the walkways when wanted) with a clear view of a cat
    // standing there (found once, when first needed)
    readonly List<Vector3> orderSpots = new List<Vector3>();
    readonly Dictionary<CatOrder, Vector3> spotTaken = new Dictionary<CatOrder, Vector3>();
    bool lookedForOrderSpots;

    // Where a cat about to order should go first: nowhere (null) if the player can already see it well; otherwise the
    // nearest free spot they can
    Vector3? OrderSpot(CatOrder order)
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
        if (eye == null)
        {
            return null;
        }
        // Staying put is fine only where a treat can reach it: in view, a treat-wide line to its body, nothing over it
        Vector3 here = order.transform.position;
        if (InGoodView(eye, here) && ClearShot(eye.position, here + 0.3f * Vector3.up) && !CoveredFromAbove(here, 1.5f))
        {
            return null;
        }
        if (!lookedForOrderSpots)
        {
            lookedForOrderSpots = true;
            FindOrderSpots(eye);
        }

        // Not one another cat is standing at or heading for
        List<CatOrder> gone = new List<CatOrder>();
        foreach (KeyValuePair<CatOrder, Vector3> taken in spotTaken)
        {
            if (taken.Key == null || !taken.Key.isHungry)
            {
                gone.Add(taken.Key);
            }
        }
        foreach (CatOrder done in gone)
        {
            spotTaken.Remove(done);
        }

        // The nearest free spot it can actually get to; with none free, the nearest spot it can get to (two cats
        // sharing beats one under a table)
        NavMeshPath path = new NavMeshPath();
        Vector3? best = null;
        Vector3? nearest = null;
        float bestDistance = float.MaxValue;
        float nearestDistance = float.MaxValue;
        foreach (Vector3 spot in orderSpots)
        {
            float distance = Vector3.Distance(spot, order.transform.position);
            if (distance >= bestDistance && distance >= nearestDistance)
            {
                continue;
            }
            if (!NavMesh.CalculatePath(order.transform.position, spot, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = spot;
            }
            bool free = true;
            foreach (KeyValuePair<CatOrder, Vector3> taken in spotTaken)
            {
                if (taken.Key != order && Vector3.Distance(taken.Value, spot) < 0.9f)
                {
                    free = false;
                }
            }
            foreach (CatOrder other in orders)
            {
                if (other != null && other != order && other.isHungry && Vector3.Distance(other.transform.position, spot) < 0.9f)
                {
                    free = false;
                }
            }
            if (free && distance < bestDistance)
            {
                bestDistance = distance;
                best = spot;
            }
        }
        if (best == null)
        {
            best = nearest;
        }
        if (best != null)
        {
            spotTaken[order] = best.Value;
        }
        return best;
    }

    // In front of the player, not too near or far, and nothing solid hiding it
    bool InGoodView(Transform eye, Vector3 at)
    {
        Vector3 to = Flat(at - eye.position);
        float distance = to.magnitude;
        if (distance < 1.8f || distance > 8f)
        {
            return false;
        }
        Vector3 forward = Flat(player.transform.forward);
        if (forward.sqrMagnitude > 0.001f && Vector3.Angle(forward, to) > 60f)
        {
            return false;
        }
        return CanSee(eye.position, at + Vector3.up * 0.25f);
    }

    // Spots fanned out in front of the player: straight ahead only at the nearest ring, then to both sides
    void FindOrderSpots(Transform eye)
    {
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
        for (float distance = 2.5f; distance <= 10.5f; distance += 0.8f)
        {
            float minAngle = 20f;
            if (distance < 2.6f)
            {
                minAngle = 0f;
            }
            for (float angle = minAngle; angle <= 60f; angle += 10f)
            {
                TryOrderSpots(eye, at + Quaternion.Euler(0, -angle, 0) * forward * distance, at.y);
                if (angle > 0f)
                {
                    TryOrderSpots(eye, at + Quaternion.Euler(0, angle, 0) * forward * distance, at.y);
                }
            }
        }
    }

    static readonly float[] WalkwayHeights = { 1.2f, 1.7f, 2.2f, 2.7f, 3.2f };

    // The floor at `point`, and any walkway above it when those are wanted
    void TryOrderSpots(Transform eye, Vector3 point, float floorY)
    {
        TryOrderSpot(eye, point, floorY, 0f);
        if (orderSpotsOnWalkways)
        {
            foreach (float height in WalkwayHeights)
            {
                TryOrderSpot(eye, point, floorY, height);
            }
        }
    }

    // A good spot is on the floor (or, `raised` metres up, on a walkway: not a table top), clear of the other spots, in
    // good view low and high with a treat-wide line to it (so no table top between the player and the cat), and with
    // nothing over it (so not under a table or a chair)
    void TryOrderSpot(Transform eye, Vector3 point, float floorY, float raised)
    {
        if (!NavMesh.SamplePosition(point + Vector3.up * raised, out NavMeshHit hit, 0.4f, NavMesh.AllAreas))
        {
            return;
        }
        float up = hit.position.y - floorY;
        if (raised <= 0f)
        {
            if (Mathf.Abs(up) > 0.3f)
            {
                return;
            }
        }
        else if (up < 0.8f || up > 3.5f || Mathf.Abs(up - raised) > 0.5f)
        {
            return;
        }
        if (orderSpots.Exists(spot => Vector3.Distance(spot, hit.position) < 0.7f))
        {
            return;
        }
        if (!InGoodView(eye, hit.position) || !InGoodView(eye, hit.position + 0.5f * Vector3.up))
        {
            return;
        }
        float headroom = 1.5f;
        if (raised > 0f)
        {
            headroom = 0.8f;   // the ceiling is close over a hanging walkway
        }
        if (!ClearShot(eye.position, hit.position + 0.3f * Vector3.up) || CoveredFromAbove(hit.position, headroom))
        {
            return;
        }
        orderSpots.Add(hit.position);
        Debug.DrawLine(hit.position, hit.position + Vector3.up, Color.blue, 20f);
    }

    // Like CanSee, but as wide as a treat: a table edge a thin ray squeaks past still blocks the shot
    static bool ClearShot(Vector3 from, Vector3 to)
    {
        Vector3 line = to - from;
        foreach (RaycastHit hit in Physics.SphereCastAll(from, 0.12f, line.normalized, line.magnitude - 0.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Collider other = hit.collider;
            if (other.GetComponentInParent<CatMovement>() != null || other.GetComponentInParent<FlyingProp>() != null ||
                other.GetComponentInParent<PlayerInput>() != null)
            {
                continue;
            }
            return false;
        }
        return true;
    }

    // Something solid within head height over the spot: a table top, a chair, a shelf
    static bool CoveredFromAbove(Vector3 floorPoint, float reach)
    {
        foreach (RaycastHit hit in Physics.RaycastAll(floorPoint + Vector3.up * 0.1f, Vector3.up, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Collider other = hit.collider;
            if (other.GetComponentInParent<CatMovement>() != null || other.GetComponentInParent<FlyingProp>() != null ||
                other.GetComponentInParent<PlayerInput>() != null)
            {
                continue;
            }
            return true;
        }
        return false;
    }

    void Served(CatOrder order)
    {
        if (tipJar == null || coinsPerTreat <= 0)
        {
            return;
        }
        int coins = coinsPerTreat * order.orderLength;
        // Quick service (in the first half of its patience) tips better
        if (orderMoments && quickServiceCoins > 0 && order.patienceFraction > 0.5f)
        {
            coins += quickServiceCoins * order.orderLength;
            FloatingWord.Show(quickServiceText, order.aboveBubble, new Color(1f, 0.85f, 0.2f));
        }
        tipJar.AddCoins(order.transform.position + Vector3.up * 0.3f, coins);
    }

    CatOrder.Hole? NearestHole(Vector3 from)
    {
        CatOrder.Hole? best = null;
        foreach (CatOrder.Hole hole in holes)
        {
            if (best == null || (hole.outside - from).sqrMagnitude < (best.Value.outside - from).sqrMagnitude)
            {
                best = hole;
            }
        }
        return best;
    }

    // The doors cats come in by: the ones in front of the player (so they arrive where the player is looking)
    void PickEntryDoors()
    {
        entryDoors.Clear();
        Vector3 facing;
        if (player != null)
        {
            facing = Flat(player.transform.forward);
        }
        else
        {
            facing = Vector3.zero;
        }
        foreach (Transform door in doors)
        {
            if (door == null)
            {
                continue;
            }
            Vector3 to;
            if (player != null)
            {
                to = Flat(door.position - player.transform.position);
            }
            else
            {
                to = Vector3.zero;
            }
            if (entryArc >= 180f || facing.sqrMagnitude < 0.001f || Vector3.Angle(facing, to) <= entryArc)
            {
                entryDoors.Add(door);
            }
        }
        if (entryDoors.Count == 0)
        {
            entryDoors.AddRange(doors.FindAll(door => door != null));
        }
    }

    // The holes at the doors cats come in by. They are modelled into the cafe (the couches), with a door marker in the
    // mouth of each, facing out into the room: so a hole is the floor just outside its marker and the way in behind it
    void MakeHoles()
    {
        holes.Clear();
        foreach (Transform door in entryDoors)
        {
            if (door == null)
            {
                continue;
            }
            CatOrder.Hole hole = new CatOrder.Hole();
            hole.outside = door.position;
            if (NavMesh.SamplePosition(door.position, out NavMeshHit floor, 1.5f, NavMesh.AllAreas))
            {
                hole.outside = floor.position;
            }
            hole.inward = -Flat(door.forward);
            if (hole.inward.sqrMagnitude > 0.001f)
            {
                hole.inward = hole.inward.normalized;
            }
            holes.Add(hole);
        }
    }

    // Every hungry cat heads for the nearest door and leaves
    void SendCatsAway()
    {
        foreach (CatOrder order in orders)
        {
            if (order == null || !order.isHungry)
            {
                continue;
            }
            Vector3? exit = NearestExit(order.transform.position);
            if (exit.HasValue)
            {
                order.RunOff(exit.Value);
            }
        }
        orders.Clear();
    }

    // The floor at the door nearest `from` (some door markers sit a little above it)
    Vector3? NearestExit(Vector3 from)
    {
        Transform nearest = null;
        foreach (Transform door in doors)
        {
            if (door == null)
            {
                continue;
            }
            if (nearest == null || (door.position - from).sqrMagnitude < (nearest.position - from).sqrMagnitude)
            {
                nearest = door;
            }
        }
        if (nearest == null)
        {
            return null;
        }
        Vector3 exit = nearest.position;
        if (NavMesh.SamplePosition(exit, out NavMeshHit floor, 1.5f, NavMesh.AllAreas))
        {
            exit = floor.position;
        }
        return exit;
    }

    // The wrong colour: an angry face (already shown), a moment to sulk, then it storms off without a tip
    void WrongTreat(CatOrder order)
    {
        if (wrongColourLeaves)
        {
            StartCoroutine(StormOffRoutine(order));
        }
    }

    IEnumerator StormOffRoutine(CatOrder order)
    {
        yield return new WaitForSeconds(0.6f);
        if (order == null || !order.isHungry || !playing)
        {
            yield break;
        }
        CatRaid raid = order.GetComponent<CatRaid>();
        if (raid != null && raid.isRunning)
        {
            raid.Cancel();
        }
        LeaveWithoutTip(order);
    }

    void LeaveWithoutTip(CatOrder order)
    {
        Vector3? exit = NearestExit(order.transform.position);
        if (exit.HasValue)
        {
            order.RunOff(exit.Value);
        }
        orders.Remove(order);
    }

    // Kept waiting too long: it goes for the tip jar (the right treat during its wind-up still calms it down), or,
    // with no jar (or two raids already on), storms off without a tip
    void PatienceOut(CatOrder order)
    {
        if (!playing || order == null || !order.isHungry)
        {
            return;
        }
        if (impatientCatsRaid && tipJar != null && CatRaid.active.Count < 2)
        {
            CatRaid.Target jar = JarTarget();
            if (jar != null && CatRaid.Begin(order, jar, raid, ImpatientRaidOver) != null)
            {
                return;
            }
        }
        LeaveWithoutTip(order);
    }

    void ImpatientRaidOver(CatRaid finished, CatRaid.Result result)
    {
        CatOrder order = finished.catOrder;
        if (order == null || !playing)
        {
            return;
        }
        // Calmed down with the right treat in time: it'll wait a while longer
        if (result == CatRaid.Result.Saved)
        {
            order.RefillPatience(0.6f);
        }
        // Got to the jar: it grabbed its tip and it's off
        else if (result == CatRaid.Result.Swatted && order.isHungry)
        {
            LeaveWithoutTip(order);
        }
    }

    // A cat-proof patch of floor where the player stands (cut out of the NavMesh while the round runs)
    void KeepCatsOutOfPlayerSpace()
    {
        if (playerSpace.x <= 0 || playerSpace.z <= 0 || player == null)
        {
            return;
        }
        GameObject space = new GameObject("Player Space (no cats)");
        Vector3 at = player.transform.position;
        if (NavMesh.SamplePosition(at, out NavMeshHit floor, 2f, NavMesh.AllAreas))
        {
            at.y = floor.position.y;
        }
        space.transform.SetPositionAndRotation(at, Quaternion.Euler(0, player.transform.eulerAngles.y, 0));
        NavMeshObstacle obstacle = space.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = playerSpace;
        obstacle.center = new Vector3(0, playerSpace.y * 0.5f, 0);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
    }

    // Ninja stops far enough from the player (so nothing springs up right beside them)
    List<Vector3> StopsAwayFromPlayer()
    {
        if (player == null || ninjaMinDistance <= 0)
        {
            return ninjaStops;
        }
        Vector3 at = player.transform.position;
        List<Vector3> away = ninjaStops.FindAll(stop => new Vector2(stop.x - at.x, stop.z - at.z).magnitude >= ninjaMinDistance);
        if (away.Count > 0)
        {
            return away;
        }
        return ninjaStops;
    }
}
