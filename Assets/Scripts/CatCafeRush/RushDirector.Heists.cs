using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

// Rush Director, the Mafia heist: the boss cat strolls in for the tip jar with his gang, in feeding phases after the first
public partial class RushDirector : MonoBehaviour
{
    BossCat mafiaPrefab;

    void HeistOver(MafiaHeist heist, MafiaHeist.Result result)
    {
        if (!playing)
        {
            return;
        }
        if (result == MafiaHeist.Result.Busted)
        {
            SayOnce(bustedLine);
        }
        else if (result == MafiaHeist.Result.Stole)
        {
            SayOnce(stoleLine);
        }
    }

    // The Mafia cat's heists in a feeding phase, spread through its middle stretch, one at a time
    IEnumerator HeistsRoutine(Phase phase, int count, int gang)
    {
        if (!mafiaHeists || count <= 0 || mafiaPrefab == null)
        {
            yield break;
        }
        float start = clock;
        for (int i = 0; i < count; i++)
        {
            float slot = phase.seconds / count;
            float at = start + slot * i + slot * Random.Range(0.2f, 0.45f);
            while (playing && current == phase && (clock < at || MafiaHeist.current != null || !RoomForMore || !MayIntroduce(heistLine)))
            {
                yield return null;
            }
            if (!playing || current != phase || tipJar == null || windingDown)
            {
                yield break;
            }
            StartHeist(gang);
        }
    }

    void StartHeist(int gang)
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
        if (ways.Count == 0)
        {
            return;
        }
        Transform door = ways[Random.Range(0, ways.Count)];
        Vector3 position = door.position;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
        {
            position = hit.position;
        }

        List<CatOrder.Food> foods = new List<CatOrder.Food>();
        for (int i = 0; i < heist.treats; i++)
        {
            bool left = Random.value < 0.5f;
            foods.Add(new CatOrder.Food
            {
                variation = left ? leftFood : rightFood,
                icon = new OrderBubble.Item(left ? leftTreatPrefab : rightTreatPrefab, left ? leftColour : rightColour)
            });
        }
        MafiaHeist started = MafiaHeist.Begin(mafiaPrefab, transform, door, position, tipJar, foods, bubbleScale,
                                              cleanShots ? hitboxGenerosity : 0f, heist, HeistOver);
        if (started != null && gang > 0)
        {
            StartCoroutine(MafiaGangRoutine(position, gang));
        }
        if (started != null)
        {
            SayOnce(heistLine);
            BigStarted();
        }
    }

    // The Mafia cat's gang: kittens that burst in with him and go for the player's launchers
    IEnumerator MafiaGangRoutine(Vector3 at, int gang)
    {
        yield return new WaitForSeconds(0.6f);
        for (int i = 0; i < gang && playing; i++)
        {
            List<Vector3> path = LatchPath(at);
            CatMovement cat;
            if (path != null && catPool != null)
            {
                cat = catPool.GetRandomObjectFromPool<CatMovement>();
            }
            else
            {
                cat = null;
            }
            if (cat != null)
            {
                LauncherKitten.Send(cat, path, player, launcherKitten, KittenLatched, (kitten, result) => { });
                cat.DustPuff(KittenRun.DustSize);
            }
            yield return new WaitForSeconds(0.35f);
        }
    }
}
