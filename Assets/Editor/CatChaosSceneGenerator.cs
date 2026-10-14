using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds the Cat Chaos prototype: a copy of CC_LEVEL (Brian's roaming game) where the treat tins become piles of
// cafe props that cats knock flying, and the launchers' grips work a gravity beam that catches them.
// CC_LEVEL itself is never changed. Creating the scene again starts over from a fresh copy of CC_LEVEL.
public static class CatChaosSceneGenerator
{
    const string SourceScene = "Assets/Scenes/CC_LEVEL.unity";
    const string PrototypeScene = "Assets/Scenes/CatChaos_Prototype.unity";
    const string PrefabFolder = "Assets/Prefabs/CatChaos";
    const string MafiaVariantPath = PrefabFolder + "/Mafia Boss Cat (Cat Chaos).prefab";
    const string NinjaSource = "Assets/Prefabs/Cat/Ninja Boss Cat.prefab";
    const string NinjaRushPath = PrefabFolder + "/Ninja Rush Cat.prefab";
    const string SmallPlant = "Assets/Models/Environment/Plants/Models/monstera_plant_small_potted.fbx";
    static readonly string[] MonsteraLeaves =
    {
        "Assets/Models/Environment/Plants/Models/monstera_leaf_small_A.fbx",
        "Assets/Models/Environment/Plants/Models/monstera_leaf_small_B.fbx",
        "Assets/Models/Environment/Plants/Models/monstera_leaf_small_C.fbx",
        "Assets/Models/Environment/Plants/Models/monstera_leaf_medium_A.fbx",
    };

    const string PlatePrefab = "Assets/Models/Environment/CoffeeShopStarterPack/Prefabs/PW_plate01.prefab";
    const string CookiePrefab = "Assets/Models/Environment/Cozy Cafe/Prefabs/Cafe_Cookie_1.prefab";
    const string JarPrefab = "Assets/Models/Environment/Cozy Cafe/Prefabs/Cafe_Bottle_with_Coffee_3.prefab";
    static readonly string[] CupPrefabs =
    {
        "Assets/Models/Environment/Cozy Cafe/Prefabs/Cafe_Cup_1.prefab",
        "Assets/Models/Environment/Cozy Cafe/Prefabs/Cafe_Cup_2.prefab",
        "Assets/Models/Environment/Cozy Cafe/Prefabs/Cafe_Cup_3.prefab",
        "Assets/Models/Environment/Cozy Cafe/Prefabs/Cafe_Cup_4.prefab",
    };

    // Sizes in metres. A little bigger than life, because the piles are 3-6 m from the player.
    const float PlateWidth = 0.3f;
    const float CookieWidth = 0.17f;
    const float CupHeight = 0.14f;
    const float JarHeight = 0.26f;
    const float SmallPlantHeight = 0.3f;

    const float RoundLength = 180f;
    const int LoosePropCount = 10;

    // Bosses: the Mafia cat is parked for now; the ninja runs as a mid-round boss phase instead
    const bool IncludeMafia = false;
    const float MafiaArrives = 90f;
    const float NinjaArrives = 80f;
    const int NinjaTreats = 12;
    const float NinjaGivesUpAfter = 40f;

    [MenuItem("Catastrophe Cafe/Cat Chaos/Create Prototype Scene", false, 60)]
    static void CreateFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(PrototypeScene) &&
            !EditorUtility.DisplayDialog("Cat Chaos",
                "This makes a fresh copy of CC_LEVEL as CatChaos_Prototype, replacing the existing prototype scene " +
                "(and any changes made to it by hand). CC_LEVEL itself isn't changed.", "Make it", "Cancel"))
        {
            return;
        }
        Create();
    }

    // Keeps the open scene exactly as it's laid out (including anything moved or duplicated by hand). Brings the
    // Cat Chaos tuning numbers up to date with the scripts' defaults, and fits the cats to the layout: NavMesh,
    // jump links and waypoints for new tables, what's in front of the player, the UI and launchers by the player.
    [MenuItem("Catastrophe Cafe/Cat Chaos/Update Open Scene (Keeps Layout)", false, 61)]
    static void UpdateSettingsFromMenu()
    {
        string report = UpdateSettings();
        EditorUtility.DisplayDialog("Cat Chaos", report, "OK");
    }

    // Command line: -executeMethod CatChaosSceneGenerator.BatchUpdateSettings [-scene <path>] (opens the scene first)
    public static void BatchUpdateSettings()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int at = System.Array.IndexOf(args, "-scene");
        EditorSceneManager.OpenScene(at >= 0 && at + 1 < args.Length ? args[at + 1] : PrototypeScene, OpenSceneMode.Single);
        Debug.Log("[Cat Chaos] " + UpdateSettings());
    }

    static string UpdateSettings()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path == SourceScene) return "This is CC_LEVEL. Open CatChaos_Prototype first.";

        StringBuilder done = new StringBuilder();
        PlayerInput player = Object.FindObjectOfType<PlayerInput>();
        if (player != null) FitToLayout(player, done);
        CatChaosSettings settings = Object.FindObjectOfType<CatChaosSettings>();
        if (settings != null)
        {
            ResetToDefaults(settings, "floorHeight", "knockSound", "landedSound", "respawnSound", "homeSound", "catchSound");
            done.AppendLine("Flying props and cat knocks: updated.");
        }
        GravityBeam beam = Object.FindObjectOfType<GravityBeam>();
        if (beam != null)
        {
            ResetToDefaults(beam, "beamColour");
            done.AppendLine("Gravity beam: updated (grabs instantly).");
        }
        CatChaosDirector director = Object.FindObjectOfType<CatChaosDirector>();
        if (director != null)
        {
            ResetToDefaults(director, "catPool", "spawnPoints", "returnPoints", "roundLength", "ninjaPrefab", "ninjaStops",
                            "bossPhaseStart", "bossArrivesBanner", "bossFedBanner", "bossLeftBanner");
            done.AppendLine("Director (top-up cats): updated.");
        }
        if (done.Length == 0) return "No Cat Chaos objects found in the open scene.";

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return done + "Layout left as it was. Scene saved.";
    }

    // ---------- Fitting the cats to a hand-made layout ----------

    static void FitToLayout(PlayerInput player, StringBuilder done)
    {
        Vector3 eye = player.transform.position;
        Vector3 forward = player.transform.forward;
        forward.y = 0;
        forward.Normalize();
        float floorY = FindFloor(eye, player);

        // Props copied by hand that no pile knows about (e.g. a duplicated stack): each group becomes a pile of its own
        int adopted = AdoptStrayProps();

        // Piles whose props were moved by hand: put each pile's centre back under its props
        int recentred = 0;
        foreach (PropPile pile in Object.FindObjectsOfType<PropPile>())
        {
            if (RecentrePile(pile)) recentred++;
        }

        // Keep the action in front: drop roaming waypoints beside or behind the player, and their spawners
        int dropped = DropWaypointsBehind(eye, forward, out List<string> droppedNames);
        int spawnersOff = 0;
        foreach (SpawnManager spawnManager in Object.FindObjectsOfType<SpawnManager>())
        {
            Transform spawn = new SerializedObject(spawnManager).FindProperty("spawnPointCentre").objectReferenceValue as Transform;
            if (spawn != null && !InFront(eye, forward, spawn.position, 100f))
            {
                spawnManager.gameObject.SetActive(false);
                spawnersOff++;
            }
        }

        // Tables added by hand: let cats walk round them, jump up onto them, and visit them
        int tables = 0;
        foreach (GameObject table in FindTables())
        {
            if (FitTable(table, eye, floorY)) tables++;
        }

        // The NavMesh for this scene only (Brian's CC_LEVEL keeps its own)
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

        int moved = BringUIToPlayer(eye, forward, floorY);

        CatChaosDirector director = Object.FindObjectOfType<CatChaosDirector>();
        if (director != null)
        {
            if (director.ninjaPrefab == null)
            {
                GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(NinjaRushPath);
                director.ninjaPrefab = saved != null ? saved.GetComponent<NinjaRushCat>() : MakeNinjaRush();
            }
            FindTopUpSpawns(director, player);
            director.ninjaStops.Clear();
            foreach (PropPile pile in Object.FindObjectsOfType<PropPile>())
            {
                if (pile.pile != null) AddNinjaStop(director, pile.pile.position);
            }
            int loose = 0;
            foreach (FlyingProp prop in Object.FindObjectsOfType<FlyingProp>())
            {
                if (prop.isLoose && loose < 5 && InFront(eye, forward, prop.transform.position, 80f))
                {
                    AddNinjaStop(director, prop.transform.position);
                    loose++;
                }
            }
            EditorUtility.SetDirty(director);
        }

        done.AppendLine($"Layout: {adopted} new pile(s) made from copied props; {recentred} pile(s) re-centred on their props; " +
                        $"{dropped} waypoint(s) beside/behind the player dropped" +
                        (droppedNames.Count > 0 ? $" ({string.Join(", ", droppedNames)})" : "") +
                        (spawnersOff > 0 ? $", {spawnersOff} spawner(s) behind switched off" : "") + ".");
        done.AppendLine($"Tables: {tables} new table(s) given jump links and a waypoint on top. NavMesh rebaked for this scene.");
        done.AppendLine($"Launchers and start/results boards moved back in front of the player: {moved}.");
        if (director != null)
        {
            done.AppendLine($"Ninja: {(director.ninjaPrefab != null ? "linked" : "MISSING")}, {director.ninjaStops.Count} stops; top-up cats from {director.spawnPoints.Count} spawn points.");
        }
    }

    internal static int AdoptStrayProps()
    {
        HashSet<FlyingProp> known = new HashSet<FlyingProp>();
        foreach (PropPile pile in Object.FindObjectsOfType<PropPile>())
        {
            foreach (FlyingProp prop in pile.props)
            {
                if (prop != null) known.Add(prop);
            }
        }
        List<FlyingProp> strays = new List<FlyingProp>();
        foreach (FlyingProp prop in Object.FindObjectsOfType<FlyingProp>())
        {
            if (!prop.isLoose && !known.Contains(prop)) strays.Add(prop);
        }

        int made = 0;
        int existing = Object.FindObjectsOfType<PropPile>().Length;
        while (strays.Count > 0)
        {
            // Grow a group from one stray: everything within 0.5 m (sideways) of anything already in the group
            List<FlyingProp> group = new List<FlyingProp> { strays[0] };
            strays.RemoveAt(0);
            for (int i = 0; i < group.Count; i++)
            {
                for (int j = strays.Count - 1; j >= 0; j--)
                {
                    if (Flat(strays[j].transform.position - group[i].transform.position).magnitude < 0.5f)
                    {
                        group.Add(strays[j]);
                        strays.RemoveAt(j);
                    }
                }
            }

            Vector3 centre = Vector3.zero;
            float lowest = float.MaxValue;
            foreach (FlyingProp prop in group)
            {
                centre += prop.transform.position;
                lowest = Mathf.Min(lowest, prop.transform.position.y);
            }
            centre /= group.Count;
            centre.y = lowest;

            made++;
            GameObject holder = new GameObject($"Pile {existing + made}");
            holder.transform.position = centre;
            Transform pileTransform = new GameObject("Prop Pile").transform;
            pileTransform.SetParent(holder.transform, false);
            PropPile newPile = holder.AddComponent<PropPile>();
            newPile.pile = pileTransform;
            foreach (FlyingProp prop in group)
            {
                prop.transform.SetParent(pileTransform, true);
                PrefabUtility.RecordPrefabInstancePropertyModifications(prop.transform);
                newPile.props.Add(prop);
            }
        }
        return made;
    }

    internal static bool RecentrePile(PropPile pile)
    {
        if (pile.pile == null || pile.props.Count == 0) return false;
        Vector3 sum = Vector3.zero;
        float lowest = float.MaxValue;
        int count = 0;
        foreach (FlyingProp prop in pile.props)
        {
            if (prop == null) continue;
            sum += prop.transform.position;
            lowest = Mathf.Min(lowest, prop.transform.position.y);
            count++;
        }
        if (count == 0) return false;
        Vector3 centre = sum / count;
        centre.y = lowest;
        if (Vector3.Distance(centre, pile.pile.position) < 0.2f) return false;

        // Move the pile without moving its props
        List<Transform> children = new List<Transform>();
        foreach (Transform child in pile.pile) children.Add(child);
        foreach (Transform child in children) child.SetParent(null, true);
        pile.pile.position = centre;
        foreach (Transform child in children)
        {
            child.SetParent(pile.pile, true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(child);
        }
        return true;
    }

    internal static int DropWaypointsBehind(Vector3 eye, Vector3 forward, out List<string> names)
    {
        names = new List<string>();
        WaypointManager waypoints = Object.FindObjectOfType<WaypointManager>();
        if (waypoints == null) return 0;
        SerializedObject so = new SerializedObject(waypoints);
        SerializedProperty list = so.FindProperty("roamingWaypoints");
        int kept = 0;
        for (int i = 0; i < list.arraySize; i++)
        {
            Transform t = list.GetArrayElementAtIndex(i).FindPropertyRelative("_waypoint").objectReferenceValue as Transform;
            if (t != null && InFront(eye, forward, t.position, 80f)) kept++;
        }
        if (kept < 3) return 0;   // too few would leave the cats nowhere to go

        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            Transform t = list.GetArrayElementAtIndex(i).FindPropertyRelative("_waypoint").objectReferenceValue as Transform;
            if (t == null || InFront(eye, forward, t.position, 80f)) continue;
            names.Add(t.name);
            list.DeleteArrayElementAtIndex(i);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return names.Count;
    }

    internal static List<GameObject> FindTables()
    {
        List<GameObject> tables = new List<GameObject>();
        foreach (Transform t in Object.FindObjectsOfType<Transform>())
        {
            if (!t.name.StartsWith("Cafe_Table")) continue;
            if (t.parent != null && t.parent.name.StartsWith("Cafe_Table")) continue;   // a part of a table, not the table
            if (!TryGetBounds(t.gameObject, out Bounds b) || b.size.y < 0.4f || b.size.y > 1.3f) continue;
            tables.Add(t.gameObject);
        }
        return tables;
    }

    // Gives a table jump links up onto it (unless it already has some) and a waypoint on top. False if it had them.
    internal static bool FitTable(GameObject table, Vector3 eye, float floorY)
    {
        foreach (Transform part in table.GetComponentsInChildren<Transform>())
        {
            GameObjectUtility.SetStaticEditorFlags(part.gameObject, GameObjectUtility.GetStaticEditorFlags(part.gameObject) | StaticEditorFlags.NavigationStatic);
            PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
        }

        TryGetBounds(table, out Bounds b);
        Vector3 centre = b.center;
        float top = b.max.y;
        float radius = Mathf.Max(b.extents.x, b.extents.z);

        foreach (UnityEngine.AI.OffMeshLink existing in Object.FindObjectsOfType<UnityEngine.AI.OffMeshLink>())
        {
            foreach (Transform end in new[] { existing.startTransform, existing.endTransform })
            {
                if (end != null && Flat(end.position - centre).magnitude <= radius && Mathf.Abs(end.position.y - top) < 0.2f) return false;
            }
        }

        GameObject group = GameObject.Find("Cat Chaos Table Links") ?? new GameObject("Cat Chaos Table Links");
        Vector3 toPlayer = Flat(eye - centre).normalized;
        if (toPlayer.sqrMagnitude < 0.01f) toPlayer = Vector3.forward;
        // One link on the player's side and one on the far side, both two-way
        foreach (Vector3 side in new[] { toPlayer, -toPlayer, Vector3.Cross(Vector3.up, toPlayer) })
        {
            GameObject link = new GameObject($"Link to {table.name}");
            link.transform.SetParent(group.transform, false);
            Transform start = new GameObject("Start Point").transform;
            start.SetParent(link.transform, false);
            start.position = new Vector3(centre.x, floorY, centre.z) + side * (radius + 0.45f);
            Transform end = new GameObject("End Point").transform;
            end.SetParent(link.transform, false);
            end.position = new Vector3(centre.x, top, centre.z) + side * (radius * 0.45f);
            UnityEngine.AI.OffMeshLink offMeshLink = link.AddComponent<UnityEngine.AI.OffMeshLink>();
            offMeshLink.startTransform = start;
            offMeshLink.endTransform = end;
            offMeshLink.biDirectional = true;
            offMeshLink.costOverride = -1;
            offMeshLink.autoUpdatePositions = false;
        }

        // A waypoint on the table top, so roaming cats go up there
        WaypointManager waypoints = Object.FindObjectOfType<WaypointManager>();
        if (waypoints != null)
        {
            Transform point = new GameObject($"Table Top: {table.name}").transform;
            point.SetParent(waypoints.transform, false);
            point.position = new Vector3(centre.x, top, centre.z);
            SerializedObject so = new SerializedObject(waypoints);
            SerializedProperty list = so.FindProperty("roamingWaypoints");
            list.arraySize++;
            SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            entry.FindPropertyRelative("_waypoint").objectReferenceValue = point;
            entry.FindPropertyRelative("_inFrontOfPlayer").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return true;
    }

    // After the player has been moved: the launchers to pick up, and the start/banner/results boards, go back to the
    // same spots in front of the player (the chalkboard score and the cat clock stay where they are)
    static int BringUIToPlayer(Vector3 eye, Vector3 forward, float floorY)
    {
        int moved = 0;
        foreach (GrabbableLaunchers launchers in Object.FindObjectsOfType<GrabbableLaunchers>())
        {
            if (Flat(launchers.transform.position - eye).magnitude <= 3f) continue;
            launchers.transform.position = new Vector3(eye.x, floorY + 1.74f, eye.z) + forward * 1.52f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(launchers.transform);
            moved++;
        }
        foreach (string name in new[] { "Pregame Canvas", "Postgame Canvas", "Instructions Canvas" })
        {
            GameObject canvas = GameObject.Find(name);
            if (canvas == null || Flat(canvas.transform.position - eye).magnitude <= 5f) continue;
            float distance = name == "Instructions Canvas" ? 2.7f : 3.5f;
            Vector3 position = new Vector3(eye.x, canvas.transform.position.y, eye.z) + forward * distance;
            canvas.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
            // UI canvases load their position from the anchored position, so set that to match too
            RectTransform rect = canvas.transform as RectTransform;
            if (rect != null)
            {
                Vector3 local = rect.parent != null ? rect.parent.InverseTransformPoint(position) : position;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.anchoredPosition3D = local;
            }
            // These are prefab instances: a change made from script is only saved if it is recorded as an override
            PrefabUtility.RecordPrefabInstancePropertyModifications(canvas.transform);
            moved++;
        }
        return moved;
    }

    // Copies a fresh component's values (the script's defaults) onto an existing one, except the named fields
    static void ResetToDefaults(Component target, params string[] keep)
    {
        GameObject temporary = new GameObject("Cat Chaos Defaults") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            Component fresh = temporary.AddComponent(target.GetType());
            SerializedObject from = new SerializedObject(fresh);
            SerializedObject to = new SerializedObject(target);
            SerializedProperty property = from.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == "m_Script" || System.Array.IndexOf(keep, property.name) >= 0) continue;
                to.CopyFromSerializedProperty(property);
            }
            to.ApplyModifiedPropertiesWithoutUndo();
        }
        finally
        {
            Object.DestroyImmediate(temporary);
        }
    }

    // For running from the command line (Unity -batchmode -executeMethod CatChaosSceneGenerator.BatchCreate)
    public static void BatchCreate()
    {
        Create();
    }

    static void Create()
    {
        // Copy CC_LEVEL's contents over the prototype (keeping the prototype scene's own ID) or make it the first time
        if (File.Exists(PrototypeScene))
        {
            File.Copy(SourceScene, PrototypeScene, true);
            AssetDatabase.ImportAsset(PrototypeScene, ImportAssetOptions.ForceUpdate);
        }
        else if (!AssetDatabase.CopyAsset(SourceScene, PrototypeScene))
        {
            Debug.LogError($"[Cat Chaos] Couldn't copy {SourceScene}.");
            return;
        }
        AssetDatabase.Refresh();
        Scene scene = EditorSceneManager.OpenScene(PrototypeScene, OpenSceneMode.Single);
        Generate(scene);
    }

    static void Generate(Scene scene)
    {
        PlayerInput player = Object.FindObjectOfType<PlayerInput>();
        GameManager gameManager = Object.FindObjectOfType<GameManager>();
        if (player == null || gameManager == null)
        {
            Debug.LogError("[Cat Chaos] The scene needs the player rig (PlayerInput) and the GameManager.");
            return;
        }

        StringBuilder log = new StringBuilder("[Cat Chaos] Prototype scene made from CC_LEVEL.\n");
        float floorY = FindFloor(player.transform.position, player);
        log.AppendLine($"Floor at y = {floorY:F2}.");

        GameObject root = new GameObject("Cat Chaos");
        CatChaosSettings settings = root.AddComponent<CatChaosSettings>();
        settings.floorHeight = floorY;
        SetSounds(settings);

        // Same layout every time it's generated
        Random.InitState(20261014);
        List<Transform> piles = BuildPiles(player, log);
        List<Vector3> looseSpots = BuildLooseProps(player, piles, floorY, log);

        // The grips now work the gravity beam, so the yarnball moves to A/X
        player.gameObject.AddComponent<GravityBeam>();
        SerializedObject playerSettings = new SerializedObject(player);
        playerSettings.FindProperty("yarnballButton").enumValueIndex = (int)PlayerInput.YarnballButton.FaceButton;
        playerSettings.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("Gravity beam on both grips; yarnball moved to A/X.");

        SerializedObject gameSettings = new SerializedObject(gameManager);
        gameSettings.FindProperty("gameTime").floatValue = RoundLength;
        gameSettings.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine($"Round length {RoundLength} s.");

        if (IncludeMafia) SetUpMafia(log);
        else TurnOffBossSpawner(log);
        BuildDirector(root, player, piles, looseSpots, log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Debug.Log(log.ToString(), root);
    }

    // ---------- Piles ----------

    internal static List<Transform> BuildPiles(PlayerInput player, StringBuilder log)
    {
        List<Transform> piles = new List<Transform>();
        DefendPoint[] points = Object.FindObjectsOfType<DefendPoint>();
        // Order them right to left as seen from the player, so the right one gets the plates every time
        Vector3 eye = player.transform.position;
        Vector3 forward = player.transform.forward;
        System.Array.Sort(points, (a, b) => SignedAngle(forward, a.position - eye).CompareTo(SignedAngle(forward, b.position - eye)) * -1);

        for (int i = 0; i < points.Length; i++)
        {
            DefendPoint point = points[i];
            SerializedObject pointSettings = new SerializedObject(point);
            pointSettings.FindProperty("hitScorePenalty").intValue = 0;
            pointSettings.ApplyModifiedPropertiesWithoutUndo();

            // Hide the health bar
            Object bar = pointSettings.FindProperty("healthBar").objectReferenceValue;
            if (bar is Component barComponent)
            {
                Canvas canvas = barComponent.GetComponentInParent<Canvas>();
                (canvas != null ? canvas.gameObject : barComponent.gameObject).SetActive(false);
            }

            // Hide the treat tin (child 0, which cats turn to face), but keep it and its NavMesh obstacle
            Transform tin = point.transform.GetChild(0);
            foreach (Renderer rend in tin.GetComponentsInChildren<Renderer>(true)) rend.enabled = false;

            bool plates = i % 2 == 0;
            Transform pile = new GameObject(plates ? "Prop Pile (Plates)" : "Prop Pile (Cups)").transform;
            pile.SetParent(point.transform, false);
            // Sit on whatever the tin stood on. The defend point itself is placed on that surface, so it's the fallback.
            Vector3 spot = new Vector3(tin.position.x, point.position.y, tin.position.z);
            pile.position = new Vector3(spot.x, FindSurface(spot, point.transform), spot.z);
            Vector3 toPlayer = eye - pile.position;
            toPlayer.y = 0;
            pile.rotation = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;
            Vector3 parentScale = point.transform.lossyScale;
            pile.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);

            PropPile propPile = point.gameObject.AddComponent<PropPile>();
            propPile.pile = pile;
            piles.Add(pile);
            if (plates) BuildPlateStack(pile, propPile.props);
            else BuildCupsAndJars(pile, propPile.props);

            float angle = SignedAngle(forward, point.position - eye);
            log.AppendLine($"{(plates ? "Plates and cookies" : "Cups and jars")} ({propPile.props.Count} props) at {Mathf.Abs(angle):F0} deg {(angle >= 0 ? "right" : "left")}, " +
                           $"{Vector3.Distance(eye, point.position):F1} m away.");
        }
        return piles;
    }

    // ---------- Loose props ----------

    // Cups, jars and potted plants along the routes cats take in front of the player, so roaming cats knock them flying.
    // Spots come from Brian's roaming waypoints and the straight lines between them, snapped onto the NavMesh.
    internal static List<Vector3> BuildLooseProps(PlayerInput player, List<Transform> piles, float floorY, StringBuilder log)
    {
        List<Vector3> chosen = new List<Vector3>();
        WaypointManager waypoints = Object.FindObjectOfType<WaypointManager>();
        if (waypoints == null)
        {
            log.AppendLine("No WaypointManager found; no loose props.");
            return chosen;
        }

        Vector3 eye = player.transform.position;
        Vector3 forward = player.transform.forward;

        // Where cats go: Brian's roaming waypoints and the wave spawn points
        List<Vector3> ends = new List<Vector3>();
        List<Vector3> front = new List<Vector3>();
        SerializedProperty list = new SerializedObject(waypoints).FindProperty("roamingWaypoints");
        for (int i = 0; i < list.arraySize; i++)
        {
            Transform t = list.GetArrayElementAtIndex(i).FindPropertyRelative("_waypoint").objectReferenceValue as Transform;
            if (t == null) continue;
            ends.Add(t.position);
            if (Mathf.Abs(SignedAngle(forward, t.position - eye)) <= 80f && Flat(t.position - eye).magnitude <= 7f) front.Add(t.position);
        }
        foreach (SpawnManager spawnManager in Object.FindObjectsOfType<SpawnManager>())
        {
            Transform spawn = new SerializedObject(spawnManager).FindProperty("spawnPointCentre").objectReferenceValue as Transform;
            if (spawn != null) ends.Add(spawn.position);
        }

        // Candidates: the front waypoints, plus points every 0.7 m along the NavMesh routes between every pair of places
        List<Vector3> candidates = new List<Vector3>(front);
        UnityEngine.AI.NavMeshPath route = new UnityEngine.AI.NavMeshPath();
        for (int a = 0; a < ends.Count; a++)
        {
            for (int b = a + 1; b < ends.Count; b++)
            {
                if (!UnityEngine.AI.NavMesh.SamplePosition(ends[a], out UnityEngine.AI.NavMeshHit from, 1f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                if (!UnityEngine.AI.NavMesh.SamplePosition(ends[b], out UnityEngine.AI.NavMeshHit to, 1f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                if (!UnityEngine.AI.NavMesh.CalculatePath(from.position, to.position, UnityEngine.AI.NavMesh.AllAreas, route)) continue;
                Vector3[] corners = route.corners;
                for (int c = 0; c + 1 < corners.Length; c++)
                {
                    if (Mathf.Abs(corners[c].y - corners[c + 1].y) > 0.3f) continue;   // a jump, not a walk
                    int steps = Mathf.Max(1, Mathf.FloorToInt(Vector3.Distance(corners[c], corners[c + 1]) / 0.7f));
                    for (int s = 0; s < steps; s++) candidates.Add(Vector3.Lerp(corners[c], corners[c + 1], s / (float)steps));
                }
            }
        }
        Shuffle(candidates);

        Transform root = new GameObject("Loose Props").transform;
        List<FlyingProp> made = new List<FlyingProp>();
        int plants = 0, cups = 0, hidden = 0;
        Vector3 eyeHeight = new Vector3(eye.x, floorY + 1.6f, eye.z);

        // Lots of the furniture (the cat tunnel, for one) has no collider, so give every mesh a temporary one while
        // checking what the player can see, and take them all away again afterwards
        List<MeshCollider> temporary = AddTemporaryColliders(player);
        try
        {
            foreach (Vector3 candidate in candidates)
            {
                if (chosen.Count >= LoosePropCount) break;
                if (!UnityEngine.AI.NavMesh.SamplePosition(candidate, out UnityEngine.AI.NavMeshHit hit, 0.4f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                Vector3 spot = hit.position;

                float angle = SignedAngle(forward, spot - eye);
                float distance = Flat(spot - eye).magnitude;
                if (Mathf.Abs(angle) > 80f || distance < 1.8f || distance > 7f) continue;   // in front, but not in the player's face
                // Not under or inside something (the cats' route runs through the tunnel, where a prop would be hidden)
                if (Physics.Raycast(spot + Vector3.up * 0.05f, Vector3.up, 0.8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                bool tooClose = false;
                foreach (Transform pile in piles) tooClose |= Flat(pile.position - spot).magnitude < 1f;
                foreach (Vector3 other in chosen) tooClose |= Vector3.Distance(other, spot) < 0.9f;
                if (tooClose) continue;

                // Up on the furniture only (counter, couch, shelves): props dotted about the floor looked messy
                if (spot.y < floorY + 0.3f) continue;

                // Mostly cups, with a jar and a small potted plant in the mix
                int kind = (cups + plants) % 4;
                bool plant = kind == 3;
                string path = plant ? SmallPlant : kind == 2 ? JarPrefab : CupPrefabs[cups % CupPrefabs.Length];
                float height = plant ? SmallPlantHeight : kind == 2 ? JarHeight : CupHeight;
                GameObject prop = PlaceProp(path, root, spot, Random.Range(0f, 360f), height, 0, made);
                if (prop == null) continue;

                // Only keep it if the player can actually see it from where they stand
                TryGetBounds(prop, out Bounds shown);
                if (Physics.Linecast(eyeHeight, shown.center + Vector3.up * shown.extents.y * 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    made.Remove(prop.GetComponent<FlyingProp>());
                    Object.DestroyImmediate(prop);
                    hidden++;
                    continue;
                }

                if (plant)
                {
                    // Leaves fly off when it's knocked
                    FlyingProp flying = prop.GetComponent<FlyingProp>();
                    flying.burstPieces = LoadModels(MonsteraLeaves);
                    flying.burstCount = 5;
                    flying.burstPieceSize = 0.14f;
                    plants++;
                }
                else
                {
                    cups++;
                }
                chosen.Add(spot);
            }
        }
        finally
        {
            foreach (MeshCollider col in temporary)
            {
                if (col != null) Object.DestroyImmediate(col);
            }
        }

        foreach (FlyingProp prop in made) prop.isLoose = true;
        log.AppendLine($"Loose props: {made.Count} along the cats' routes in front ({cups} cups and jars, {plants} small plants, all up on furniture), " +
                       $"from {candidates.Count} points on the cats' routes; {hidden} spots skipped as hidden from the player.");
        return chosen;
    }

    static List<MeshCollider> AddTemporaryColliders(PlayerInput player)
    {
        List<MeshCollider> added = new List<MeshCollider>();
        foreach (MeshFilter filter in Object.FindObjectsOfType<MeshFilter>())
        {
            if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
            if (filter.transform.IsChildOf(player.transform.root)) continue;
            Renderer rend = filter.GetComponent<Renderer>();
            if (rend == null || !rend.enabled) continue;
            MeshCollider col = filter.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = filter.sharedMesh;
            added.Add(col);
        }
        Physics.SyncTransforms();
        return added;
    }

    internal static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

    static GameObject[] LoadModels(string[] paths)
    {
        List<GameObject> models = new List<GameObject>();
        foreach (string path in paths)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model != null) models.Add(model);
            else Debug.LogWarning($"[Cat Chaos] Missing model: {path}");
        }
        return models.ToArray();
    }

    static void Shuffle(List<Vector3> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector3 swap = items[i];
            items[i] = items[j];
            items[j] = swap;
        }
    }

    // ---------- Director and the ninja ----------

    static void BuildDirector(GameObject root, PlayerInput player, List<Transform> piles, List<Vector3> looseSpots, StringBuilder log)
    {
        CatChaosDirector director = root.AddComponent<CatChaosDirector>();
        director.roundLength = RoundLength;
        director.bossPhaseStart = NinjaArrives;

        foreach (ObjectSpawner spawner in Object.FindObjectsOfType<ObjectSpawner>())
        {
            if (spawner.gameObject.name == "Cat Pool") director.catPool = spawner;
        }

        FindTopUpSpawns(director, player);

        // Where the ninja can appear: on the NavMesh by each pile and some of the loose props
        foreach (Transform pile in piles) AddNinjaStop(director, pile.position);
        for (int i = 0; i < looseSpots.Count && i < 5; i++) AddNinjaStop(director, looseSpots[i]);

        director.ninjaPrefab = MakeNinjaRush();
        log.AppendLine($"Director: top-ups from {director.spawnPoints.Count} spawn points ({(director.catPool != null ? "Cat Pool found" : "NO Cat Pool found")}); " +
                       $"ninja phase at {NinjaArrives} s with {director.ninjaStops.Count} stops, {NinjaTreats} treats to fill, gone after {NinjaGivesUpAfter} s if not fed.");
    }

    // Top-up cats come in from Brian's spawn points that are in front of the player (and not right next to them)
    static void FindTopUpSpawns(CatChaosDirector director, PlayerInput player)
    {
        director.spawnPoints.Clear();
        director.returnPoints.Clear();
        Vector3 eye = player.transform.position;
        Vector3 forward = player.transform.forward;
        foreach (SpawnManager spawnManager in Object.FindObjectsOfType<SpawnManager>())
        {
            SerializedObject so = new SerializedObject(spawnManager);
            Transform spawn = so.FindProperty("spawnPointCentre").objectReferenceValue as Transform;
            Transform back = so.FindProperty("afterFeedingReturnPoint").objectReferenceValue as Transform;
            if (spawn == null || !InFront(eye, forward, spawn.position, 100f)) continue;
            director.spawnPoints.Add(spawn);
            director.returnPoints.Add(back != null ? back : spawn);
        }
    }

    // In front of the player (within maxAngle either side) and at least 2 m away
    internal static bool InFront(Vector3 eye, Vector3 forward, Vector3 point, float maxAngle)
    {
        return Mathf.Abs(SignedAngle(forward, point - eye)) <= maxAngle && Flat(point - eye).magnitude >= 2f;
    }

    internal static void AddNinjaStop(CatChaosDirector director, Vector3 near)
    {
        if (UnityEngine.AI.NavMesh.SamplePosition(near, out UnityEngine.AI.NavMeshHit hit, 0.8f, UnityEngine.AI.NavMesh.AllAreas))
        {
            director.ninjaStops.Add(hit.position);
        }
    }

    // A copy of Brian's ninja with its brain swapped for the Ninja Rush one (his prefab and script stay as they are)
    internal static NinjaRushCat MakeNinjaRush()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "CatChaos");
        // Brian's prefab is only loaded into memory here, never saved. Saving over our existing copy (rather than
        // deleting and remaking it) keeps the copy's ID, so the scene's link to it survives regenerating.
        GameObject contents = PrefabUtility.LoadPrefabContents(NinjaSource);
        contents.name = "Ninja Rush Cat";
        NinjaBossCat oldBrain = contents.GetComponent<NinjaBossCat>();
        SerializedObject brain = new SerializedObject(oldBrain);
        MonoScript rushScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/CatChaos/NinjaRushCat.cs");
        brain.FindProperty("m_Script").objectReferenceValue = rushScript;
        brain.ApplyModifiedPropertiesWithoutUndo();

        // Re-read it as the new script and set it up for a short boss phase
        SerializedObject rush = new SerializedObject(contents.GetComponent<NinjaRushCat>());
        rush.FindProperty("treatsUntilFull").intValue = NinjaTreats;
        rush.FindProperty("despawnAfter").floatValue = NinjaGivesUpAfter;
        // One thing up per stop (the drifting props give plenty to do), and a longer sit to feed it in
        rush.FindProperty("knocksPerStop").intValue = 1;
        rush.FindProperty("minimumSit").floatValue = 2f;
        rush.FindProperty("maximumSit").floatValue = 2.8f;
        rush.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(contents, NinjaRushPath);
        PrefabUtility.UnloadPrefabContents(contents);
        return AssetDatabase.LoadAssetAtPath<GameObject>(NinjaRushPath).GetComponent<NinjaRushCat>();
    }

    static void TurnOffBossSpawner(StringBuilder log)
    {
        BossCatSpawner spawner = Object.FindObjectOfType<BossCatSpawner>();
        if (spawner == null) return;
        // With no spawn times it switches itself off at the start
        SerializedObject so = new SerializedObject(spawner);
        so.FindProperty("bossSpawnTimes").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("Brian's boss spawner is off in this scene (Mafia parked); the director runs the ninja phase.");
    }

    static void BuildPlateStack(Transform pile, List<FlyingProp> props)
    {
        float y = 0;
        for (int i = 0; i < 10; i++)
        {
            GameObject plate = PlaceProp(PlatePrefab, pile, new Vector3(Random.Range(-0.008f, 0.008f), y, Random.Range(-0.008f, 0.008f)),
                                         Random.Range(0f, 360f), 0, PlateWidth, props);
            if (plate == null) return;
            TryGetBounds(plate, out Bounds b);
            y += b.size.y * 0.8f;
        }
        PlaceProp(CookiePrefab, pile, new Vector3(-0.06f, y, 0.03f), Random.Range(0f, 360f), 0, CookieWidth, props);
        PlaceProp(CookiePrefab, pile, new Vector3(0.06f, y, 0.03f), Random.Range(0f, 360f), 0, CookieWidth, props);
        PlaceProp(CookiePrefab, pile, new Vector3(0f, y, -0.06f), Random.Range(0f, 360f), 0, CookieWidth, props);
    }

    static void BuildCupsAndJars(Transform pile, List<FlyingProp> props)
    {
        PlaceProp(JarPrefab, pile, new Vector3(-0.07f, 0, 0.03f), Random.Range(0f, 360f), JarHeight, 0, props);
        PlaceProp(JarPrefab, pile, new Vector3(0.08f, 0, 0.05f), Random.Range(0f, 360f), JarHeight, 0, props);
        for (int i = 0; i < 4; i++)
        {
            float a = (i * 90f + 45f) * Mathf.Deg2Rad;
            Vector3 spot = new Vector3(Mathf.Sin(a) * 0.17f, 0, Mathf.Cos(a) * 0.17f - 0.04f);
            PlaceProp(CupPrefabs[i % CupPrefabs.Length], pile, spot, Random.Range(0f, 360f), CupHeight, 0, props);
        }
        // One cup balanced on top, to be the first to go
        PlaceProp(CupPrefabs[0], pile, new Vector3(0.0f, 0.0f, 0.0f), 0, CupHeight, 0, props, stackOnTop: true);
    }

    // Puts a prop on the pile at a size given by height (or width), sitting on y. Props are decoration only:
    // their colliders are switched off so treats and the aim reticle pass straight through.
    static GameObject PlaceProp(string path, Transform pile, Vector3 localPosition, float yaw, float height, float width,
                                List<FlyingProp> props, bool stackOnTop = false)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[Cat Chaos] Missing prefab: {path}");
            return null;
        }

        GameObject prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pile);
        prop.transform.localPosition = Vector3.zero;
        prop.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        prop.transform.localScale = Vector3.one;

        if (TryGetBounds(prop, out Bounds b))
        {
            float scale = height > 0 ? height / Mathf.Max(b.size.y, 0.0001f) : width / Mathf.Max(b.size.x, b.size.z, 0.0001f);
            prop.transform.localScale = Vector3.one * scale;
        }

        if (stackOnTop)
        {
            // Rest on the highest thing already on the pile
            float top = 0;
            foreach (FlyingProp other in props)
            {
                if (other != null && TryGetBounds(other.gameObject, out Bounds ob)) top = Mathf.Max(top, pile.InverseTransformPoint(ob.max).y);
            }
            localPosition.y = top;
        }

        // Sit its base exactly on localPosition
        prop.transform.localPosition = localPosition;
        if (TryGetBounds(prop, out Bounds placed))
        {
            prop.transform.position += Vector3.up * (pile.TransformPoint(localPosition).y - placed.min.y);
        }

        foreach (Collider col in prop.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (Rigidbody body in prop.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        props.Add(prop.AddComponent<FlyingProp>());
        return prop;
    }

    // ---------- Boss ----------

    // Only the Mafia boss, once, halfway through, with smaller gangs and a belly you can actually fill
    static void SetUpMafia(StringBuilder log)
    {
        BossCatSpawner spawner = Object.FindObjectOfType<BossCatSpawner>();
        if (spawner == null)
        {
            log.AppendLine("No Boss Cat Spawner found; no bosses.");
            return;
        }

        SerializedObject spawnerSettings = new SerializedObject(spawner);
        SerializedProperty bosses = spawnerSettings.FindProperty("bossPrefabs");
        MafiaBossCat mafia = null;
        Object mafiaSpawnPoint = null;
        for (int i = 0; i < bosses.arraySize; i++)
        {
            SerializedProperty entry = bosses.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("_bossPrefab").objectReferenceValue is MafiaBossCat found)
            {
                mafia = found;
                mafiaSpawnPoint = entry.FindPropertyRelative("_spawnPoint").objectReferenceValue;
                break;
            }
        }
        if (mafia == null)
        {
            log.AppendLine("The Boss Cat Spawner has no Mafia boss; bosses left as they were.");
            return;
        }

        MafiaBossCat variant = MakeMafiaVariant(mafia);

        bosses.arraySize = 1;
        SerializedProperty only = bosses.GetArrayElementAtIndex(0);
        only.FindPropertyRelative("_bossPrefab").objectReferenceValue = variant;
        only.FindPropertyRelative("_spawnPoint").objectReferenceValue = mafiaSpawnPoint;

        SerializedProperty times = spawnerSettings.FindProperty("bossSpawnTimes");
        times.arraySize = 1;
        times.GetArrayElementAtIndex(0).floatValue = MafiaArrives;
        spawnerSettings.ApplyModifiedPropertiesWithoutUndo();

        log.AppendLine($"Bosses: only the Mafia cat, at {MafiaArrives} s. It calls 2-3 cats every 8-12 s and is full after 25 treats.");
    }

    static MafiaBossCat MakeMafiaVariant(MafiaBossCat original)
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "CatChaos");

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(original.gameObject);
        SerializedObject boss = new SerializedObject(instance.GetComponent<MafiaBossCat>());
        boss.FindProperty("minimumTimeBetweenSpawns").floatValue = 8f;
        boss.FindProperty("maximumTimeBetweenSpawns").floatValue = 12f;
        boss.FindProperty("minimumToSpawnAtATime").intValue = 2;
        boss.FindProperty("maximumToSpawnAtATime").intValue = 3;
        boss.FindProperty("treatsUntilFull").intValue = 25;
        boss.ApplyModifiedPropertiesWithoutUndo();

        // Saving an instance of a prefab as a new asset makes a Prefab Variant: the original stays as it is
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, MafiaVariantPath);
        Object.DestroyImmediate(instance);
        return saved.GetComponent<MafiaBossCat>();
    }

    // ---------- Helpers ----------

    internal static void SetSounds(CatChaosSettings settings)
    {
        SerializedObject so = new SerializedObject(settings);
        SetSound(so, "knockSound", "Assets/Audio/floraphonic-metal-hit-49-193314.mp3", 0.45f);
        SetSound(so, "landedSound", "Assets/Audio/Treats/DullThunk.mp3", 0.8f);
        SetSound(so, "respawnSound", "Assets/Audio/UI/Pop02.mp3", 0.35f);
        SetSound(so, "homeSound", "Assets/Audio/UI/Pop01.mp3", 0.45f);
        SetSound(so, "catchSound", "Assets/Audio/UI/ChimeUp01.mp3", 0.6f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSound(SerializedObject so, string field, string clipPath, float volume)
    {
        SerializedProperty sound = so.FindProperty(field);
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        if (clip == null) Debug.LogWarning($"[Cat Chaos] Missing sound: {clipPath}");
        sound.FindPropertyRelative("_soundClip").objectReferenceValue = clip;
        sound.FindPropertyRelative("_volume").floatValue = volume;
        sound.FindPropertyRelative("_pitch").floatValue = 1f;
    }

    internal static float SignedAngle(Vector3 forward, Vector3 direction)
    {
        forward.y = 0;
        direction.y = 0;
        return Vector3.SignedAngle(forward, direction, Vector3.up);
    }

    // The top of the furniture at a spot (the highest collider just above or below it), or the spot's own height
    static float FindSurface(Vector3 spot, Transform ignore)
    {
        float best = float.MinValue;
        foreach (RaycastHit hit in Physics.RaycastAll(spot + Vector3.up * 0.3f, Vector3.down, 0.8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(ignore)) continue;
            if (hit.point.y > best) best = hit.point.y;
        }
        return best > float.MinValue ? best : spot.y;
    }

    // The highest surface under the player (ignoring the player rig itself)
    internal static float FindFloor(Vector3 centre, PlayerInput player)
    {
        float floor = float.MinValue;
        foreach (RaycastHit hit in Physics.RaycastAll(centre + Vector3.up * 2f, Vector3.down, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(player.transform.root)) continue;
            if (hit.point.y <= centre.y + 0.05f && hit.point.y > floor) floor = hit.point.y;
        }
        return floor > float.MinValue ? floor : centre.y;
    }

    internal static bool TryGetBounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;
        foreach (Renderer rend in go.GetComponentsInChildren<Renderer>(true))
        {
            if (rend is ParticleSystemRenderer || rend is LineRenderer) continue;
            if (!any) { bounds = rend.bounds; any = true; }
            else bounds.Encapsulate(rend.bounds);
        }
        return any;
    }
}
