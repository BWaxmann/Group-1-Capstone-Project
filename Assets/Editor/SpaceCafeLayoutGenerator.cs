using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds the Space Cafe prototype layout from the project's existing assets:
// a donut counter of cupboards around the player, a ring of tables with portal seats,
// food tins and a register on the counter, and the Space Cafe Service that runs it.
// Everything is placed by measurement, so it's playable but not art-directed: rearrange freely.
public static class SpaceCafeLayoutGenerator
{
    const string SourceScene = "Assets/Scenes/CC_LEVEL.unity";
    const string PrototypeScene = "Assets/Scenes/SpaceCafe_Prototype.unity";
    const string LayoutName = "Space Cafe Layout";

    const string CupboardPrefab = "Assets/Models/Environment/CoffeeShopStarterPack/Prefabs/PW_cupboard01.prefab";
    const string TablePrefab = "Assets/Models/Environment/Cozy Cafe/Prefabs/Cafe_Table_1.prefab";
    const string ChairPrefab = "Assets/Models/Environment/Cozy Cafe/Prefabs/Cafe_Chair_1.prefab";
    const string RegisterPrefab = "Assets/Models/Environment/Cozy Cafe/Prefabs/Cafe_Cafe_Cash_Register_1.prefab";
    const string TinPrefab = "Assets/Models/Environment/Pet Assets/Asset/Prefabs/Food2 V1.prefab";
    const string CustomerFolder = "Assets/Prefabs/SpaceCafe/Customers";

    // Measurements in metres
    const float CounterRadius = 0.85f;
    const float CounterHeight = 0.95f;
    const float CounterDepth = 0.45f;
    const float TableHeight = 0.75f;
    const float TableWidth = 0.6f;
    const float TableRingOffset = 22.5f;   // leaves a gap straight ahead for the boards
    const float RaiseBoards = 0.9f;        // lifts the instruction/score boards above the cats
    const float ChairHeight = 0.9f;
    const int TableCount = 8;
    const float PortalAboveChair = 0.85f;
    const float PortalBehindTable = 0.75f;
    const float MaxTableRadius = 2.6f;
    const float MinTableRadius = 1.8f;

    [MenuItem("Catastrophe Cafe/Space Cafe/Create Prototype Scene", false, 40)]
    static void CreatePrototypeScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (!File.Exists(PrototypeScene))
        {
            if (!AssetDatabase.CopyAsset(SourceScene, PrototypeScene))
            {
                EditorUtility.DisplayDialog("Space Cafe", $"Couldn't copy {SourceScene}.", "OK");
                return;
            }
            AssetDatabase.Refresh();
        }

        Scene scene = EditorSceneManager.OpenScene(PrototypeScene, OpenSceneMode.Single);
        Generate(scene);
    }

    // For running from the command line (Unity -batchmode -executeMethod SpaceCafeLayoutGenerator.BatchCreate)
    public static void BatchCreate()
    {
        if (!File.Exists(PrototypeScene))
        {
            AssetDatabase.CopyAsset(SourceScene, PrototypeScene);
            AssetDatabase.Refresh();
        }
        Generate(EditorSceneManager.OpenScene(PrototypeScene, OpenSceneMode.Single));
    }

    [MenuItem("Catastrophe Cafe/Space Cafe/Regenerate Layout In Open Scene", false, 41)]
    static void RegenerateInOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path == SourceScene)
        {
            EditorUtility.DisplayDialog("Space Cafe",
                "This would change CC_LEVEL. Use Create Prototype Scene instead, which works on a copy.", "OK");
            return;
        }
        Generate(scene);
    }

    static void Generate(Scene scene)
    {
        PlayerInput player = Object.FindObjectOfType<PlayerInput>();
        if (player == null)
        {
            EditorUtility.DisplayDialog("Space Cafe", "No PlayerInput (player rig) found in the open scene.", "OK");
            return;
        }

        StringBuilder log = new StringBuilder("[Space Cafe] Layout generated.\n");

        // Undo a previous generation first, so regenerating is always clean
        GameObject oldRoot = GameObject.Find(LayoutName);
        if (oldRoot != null)
        {
            SpaceCafeLayoutInfo oldInfo = oldRoot.GetComponent<SpaceCafeLayoutInfo>();
            if (oldInfo != null)
            {
                foreach (GameObject go in oldInfo.disabledObjects)
                {
                    if (go != null) go.SetActive(true);
                }
                for (int i = 0; i < oldInfo.movedObjects.Count; i++)
                {
                    if (oldInfo.movedObjects[i] != null) oldInfo.movedObjects[i].transform.position = oldInfo.originalPositions[i];
                }
                foreach (Collider col in oldInfo.disabledColliders)
                {
                    if (col != null) col.enabled = true;
                }
            }
            Object.DestroyImmediate(oldRoot);
        }

        Vector3 centre = player.transform.position;
        float floorY = centre.y;
        Vector3 forward = player.transform.forward;
        forward.y = 0;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        GameObject root = new GameObject(LayoutName);
        root.transform.SetPositionAndRotation(new Vector3(centre.x, floorY, centre.z), Quaternion.LookRotation(forward));
        SpaceCafeLayoutInfo info = root.AddComponent<SpaceCafeLayoutInfo>();

        float roomRadius = MeasureRoomRadius(centre);
        float tableRadius = Mathf.Clamp(roomRadius - 1.5f, MinTableRadius, MaxTableRadius);
        float portalRadius = tableRadius + PortalBehindTable;
        log.AppendLine($"Room radius about {roomRadius:F1} m. Counter {CounterRadius} m, tables {tableRadius:F1} m, portals {portalRadius:F1} m.");
        if (portalRadius + 0.4f > roomRadius)
        {
            log.AppendLine("Note: the portal ring is close to the walls. The room may need to be bigger.");
        }

        TurnOffOldGameplay(info, log);
        ClearSpace(root.transform, player, centre, floorY, portalRadius + 0.5f, info, log);

        float counterTop = BuildCounter(root.transform, floorY);
        Transform register = PlaceOnCounter(root.transform, RegisterPrefab, "Register", 45f, counterTop, 0.35f);
        MoveLaunchersToCounter(root.transform, counterTop, info, log);
        RaiseBoardsAboveCats(info, log);

        // Decoration only: treats and the aim reticle pass straight through these
        int ghosted = MakeDecorationOnly(register != null ? register.gameObject : null, info);
        ghosted += MakeDecorationOnly(GameObject.Find("UI"), info);
        log.AppendLine($"Switched off {ghosted} colliders on the register and UI boards so they don't block treats.");
        BuildTables(root.transform, floorY, tableRadius, portalRadius);
        BuildService(root.transform, register, log);

        SerializedObject playerSettings = new SerializedObject(player);
        playerSettings.FindProperty("baristaMode").boolValue = true;
        playerSettings.ApplyModifiedProperties();
        log.AppendLine("Player set to Barista Mode (each hand fires its own food; grip switches food).");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Debug.Log(log.ToString(), root);
    }

    // ---------- Measuring ----------

    static Vector3 AngleToLocal(float degrees, float radius)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(r) * radius, 0, Mathf.Cos(r) * radius);
    }

    static bool TryGetBounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;
        foreach (Renderer rend in go.GetComponentsInChildren<Renderer>())
        {
            if (rend is ParticleSystemRenderer) continue;
            if (!any) { bounds = rend.bounds; any = true; }
            else bounds.Encapsulate(rend.bounds);
        }
        return any;
    }

    static float MeasureRoomRadius(Vector3 centre)
    {
        GameObject room = GameObject.Find("Cafe2");
        if (room != null && TryGetBounds(room, out Bounds b))
        {
            float r = Mathf.Min(b.max.x - centre.x, centre.x - b.min.x, b.max.z - centre.z, centre.z - b.min.z);
            if (r > 1f) return r;
        }
        return 5f;
    }

    // Places a prefab standing on the floor, scaled to a target height (and optionally a target width)
    static GameObject PlaceStanding(string path, Transform parent, Vector3 localPosition, Quaternion localRotation, float targetHeight, float floorY, float targetWidth = 0f)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[Space Cafe] Missing prefab: {path}");
            return null;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = Vector3.one;

        if (TryGetBounds(go, out Bounds b) && b.size.y > 0.001f)
        {
            float heightScale = targetHeight / b.size.y;
            float widthScale = targetWidth > 0 ? targetWidth / Mathf.Max(b.size.x, b.size.z, 0.001f) : heightScale;
            go.transform.localScale = new Vector3(widthScale, heightScale, widthScale);
            TryGetBounds(go, out b);
            go.transform.position += Vector3.up * (floorY - b.min.y);
        }
        return go;
    }

    // ---------- Clearing the old game ----------

    static void Disable(GameObject go, SpaceCafeLayoutInfo info)
    {
        if (go != null && go.activeSelf)
        {
            go.SetActive(false);
            info.disabledObjects.Add(go);
        }
    }

    static void TurnOffOldGameplay(SpaceCafeLayoutInfo info, StringBuilder log)
    {
        int before = info.disabledObjects.Count;
        foreach (SpawnManager x in Object.FindObjectsOfType<SpawnManager>()) Disable(x.gameObject, info);
        foreach (BossCatSpawner x in Object.FindObjectsOfType<BossCatSpawner>()) Disable(x.gameObject, info);
        foreach (DefendPoint x in Object.FindObjectsOfType<DefendPoint>()) Disable(x.gameObject, info);
        foreach (CatHole x in Object.FindObjectsOfType<CatHole>()) Disable(x.gameObject, info);
        foreach (string name in new[] { "Spawners", "Defend Points", "Waypoints" })
        {
            Disable(GameObject.Find(name), info);
        }
        log.AppendLine($"Switched off {info.disabledObjects.Count - before} old gameplay objects (wave spawners, defend points, cat holes).");
    }

    static bool IsProtected(Transform t, PlayerInput player)
    {
        if (t.IsChildOf(player.transform.root)) return true;
        if (t.GetComponentInParent<Canvas>() != null) return true;
        if (t.GetComponentInParent<GameManager>() != null) return true;
        if (t.GetComponentInParent<ObjectSpawner>() != null) return true;
        if (t.GetComponentInParent<GrabbableLaunchers>() != null) return true;
        if (t.GetComponentInParent<Light>() != null) return true;
        return false;
    }

    // Switches off furniture and props where the counter, tables and portals go (never floors or walls)
    static void ClearSpace(Transform root, PlayerInput player, Vector3 centre, float floorY, float clearRadius, SpaceCafeLayoutInfo info, StringBuilder log)
    {
        List<string> cleared = new List<string>();
        foreach (Renderer rend in Object.FindObjectsOfType<Renderer>())
        {
            if (rend is ParticleSystemRenderer) continue;
            Transform t = rend.transform;
            if (t.IsChildOf(root) || IsProtected(t, player)) continue;

            Bounds b = rend.bounds;
            if (b.size.x > 3.5f || b.size.z > 3.5f) continue;      // floors, walls, ceilings
            if (b.min.y > floorY + 2.0f) continue;                 // things up high
            if (b.max.y < floorY + 0.03f) continue;                // flat floor decals

            float dx = Mathf.Max(b.min.x - centre.x, 0, centre.x - b.max.x);
            float dz = Mathf.Max(b.min.z - centre.z, 0, centre.z - b.max.z);
            if (Mathf.Sqrt(dx * dx + dz * dz) > clearRadius) continue;

            // Switch off the whole prop if it's a small prefab, otherwise just this part
            GameObject target = rend.gameObject;
            GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(rend.gameObject);
            if (instanceRoot != null && !IsProtected(instanceRoot.transform, player)
                && TryGetBounds(instanceRoot, out Bounds rootBounds) && rootBounds.size.x < 3.5f && rootBounds.size.z < 3.5f)
            {
                target = instanceRoot;
            }

            if (target.activeSelf)
            {
                Disable(target, info);
                cleared.Add(target.name);
            }
        }

        log.AppendLine(cleared.Count == 0
            ? "Nothing was in the way."
            : $"Switched off {cleared.Count} objects in the way (not deleted; listed on {LayoutName} > Disabled Objects): {string.Join(", ", cleared)}");
    }

    // ---------- Building ----------

    static float BuildCounter(Transform root, float floorY)
    {
        Transform counter = new GameObject("Donut Counter").transform;
        counter.SetParent(root, false);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CupboardPrefab);
        if (prefab == null)
        {
            Debug.LogWarning($"[Space Cafe] Missing prefab: {CupboardPrefab}");
            return floorY + CounterHeight;
        }

        // Measure one cupboard at its natural size
        GameObject sample = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        sample.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        TryGetBounds(sample, out Bounds natural);
        Object.DestroyImmediate(sample);

        float sy = CounterHeight / Mathf.Max(natural.size.y, 0.01f);
        float sz = CounterDepth / Mathf.Max(natural.size.z, 0.01f);
        float circumference = 2f * Mathf.PI * CounterRadius;
        int pieces = Mathf.Max(10, Mathf.RoundToInt(circumference / Mathf.Max(natural.size.x * sy, 0.05f)));
        float sx = circumference / pieces * 1.08f / Mathf.Max(natural.size.x, 0.01f);

        float top = floorY + CounterHeight;
        for (int i = 0; i < pieces; i++)
        {
            float angle = i * 360f / pieces;
            Vector3 dir = AngleToLocal(angle, 1f);
            GameObject piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab, counter);
            piece.name = $"Counter {i + 1}";
            piece.transform.localPosition = dir * CounterRadius;
            // Fronts face the player in the middle
            piece.transform.localRotation = Quaternion.LookRotation(-dir);
            piece.transform.localScale = new Vector3(sx, sy, sz);

            if (TryGetBounds(piece, out Bounds b))
            {
                piece.transform.position += Vector3.up * (floorY - b.min.y);
                top = floorY + b.size.y;
            }
        }
        return top;
    }


    static Transform PlaceOnCounter(Transform root, string path, string name, float angle, float counterTop, float largestSize)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return null;

        Vector3 dir = AngleToLocal(angle, 1f);
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
        go.name = name;
        go.transform.localPosition = dir * CounterRadius;
        go.transform.localRotation = Quaternion.LookRotation(-dir);
        go.transform.localScale = Vector3.one;
        if (TryGetBounds(go, out Bounds b))
        {
            float largest = Mathf.Max(b.size.x, b.size.y, b.size.z, 0.01f);
            go.transform.localScale = Vector3.one * (largestSize / largest);
            TryGetBounds(go, out b);
            go.transform.position += Vector3.up * (counterTop - b.min.y);
        }
        return go.transform;
    }

    static void MoveLaunchersToCounter(Transform root, float counterTop, SpaceCafeLayoutInfo info, StringBuilder log)
    {
        GrabbableLaunchers launchers = Object.FindObjectOfType<GrabbableLaunchers>();
        if (launchers == null) return;

        GameObject go = PrefabUtility.GetNearestPrefabInstanceRoot(launchers.gameObject) ?? launchers.gameObject;
        info.movedObjects.Add(go);
        info.originalPositions.Add(go.transform.position);

        Vector3 target = root.TransformPoint(AngleToLocal(0f, CounterRadius));
        go.transform.position = new Vector3(target.x, go.transform.position.y, target.z);
        if (TryGetBounds(go, out Bounds b))
        {
            go.transform.position += Vector3.up * (counterTop - b.min.y + 0.01f);
        }
        log.AppendLine("Moved the launchers onto the front of the counter.");
    }

    // Switches off solid colliders (not triggers) so the object can't block shots
    static int MakeDecorationOnly(GameObject target, SpaceCafeLayoutInfo info)
    {
        if (target == null) return 0;

        int count = 0;
        foreach (Collider col in target.GetComponentsInChildren<Collider>(true))
        {
            if (col.isTrigger || !col.enabled) continue;
            col.enabled = false;
            info.disabledColliders.Add(col);
            count++;
        }
        return count;
    }

    // The instruction and score boards sit in front of the player; lift them so cats aren't hidden
    static void RaiseBoardsAboveCats(SpaceCafeLayoutInfo info, StringBuilder log)
    {
        GameObject ui = GameObject.Find("UI");
        if (ui == null) return;

        info.movedObjects.Add(ui);
        info.originalPositions.Add(ui.transform.position);
        ui.transform.position += Vector3.up * RaiseBoards;
        log.AppendLine($"Raised the UI boards by {RaiseBoards} m so they don't hide the cats.");
    }

    static void BuildTables(Transform root, float floorY, float tableRadius, float portalRadius)
    {
        Transform tables = new GameObject("Tables And Portals").transform;
        tables.SetParent(root, false);

        for (int i = 0; i < TableCount; i++)
        {
            float angle = TableRingOffset + i * 360f / TableCount;
            float signedAngle = angle > 180f ? angle - 360f : angle;
            Vector3 dir = AngleToLocal(angle, 1f);
            Quaternion facePlayer = Quaternion.LookRotation(-dir);

            Transform spot = new GameObject($"Table {i + 1}").transform;
            spot.SetParent(tables, false);
            spot.localPosition = dir * tableRadius;
            spot.localRotation = facePlayer;

            GameObject table = PlaceStanding(TablePrefab, spot, Vector3.zero, Quaternion.identity, TableHeight, floorY, TableWidth);
            float tableTop = floorY + TableHeight;
            if (table != null && TryGetBounds(table, out Bounds tb)) tableTop = tb.max.y;

            // The cat stands on the table, slightly towards the player
            Transform stand = new GameObject("Cat Stand Point").transform;
            stand.SetParent(spot, false);
            stand.localPosition = new Vector3(0, tableTop - floorY, 0.1f);

            // Chair behind the table, with the portal floating above it
            Vector3 chairLocal = new Vector3(0, 0, -(portalRadius - tableRadius));
            PlaceStanding(ChairPrefab, spot, chairLocal, Quaternion.identity, ChairHeight, floorY);

            Transform portalTransform = new GameObject("Portal").transform;
            portalTransform.SetParent(spot, false);
            portalTransform.localPosition = chairLocal + Vector3.up * PortalAboveChair;
            PortalRing portal = portalTransform.gameObject.AddComponent<PortalRing>();

            CustomerTable customerTable = spot.gameObject.AddComponent<CustomerTable>();
            customerTable.Setup(stand, portal, signedAngle);
            EditorUtility.SetDirty(customerTable);
        }
    }

    static void BuildService(Transform root, Transform register, StringBuilder log)
    {
        GameObject serviceObject = new GameObject("Space Cafe Service");
        serviceObject.transform.SetParent(root, false);
        SpaceCafeService service = serviceObject.AddComponent<SpaceCafeService>();

        // Each hand's food, shown as a tin under its launcher
        HandFoods hands = serviceObject.AddComponent<HandFoods>();
        hands.Setup(AssetDatabase.LoadAssetAtPath<GameObject>(TinPrefab), Sound("Assets/Audio/Treats/Reload.wav", 0.8f));
        EditorUtility.SetDirty(hands);

        SpaceCafeService.Food[] foods =
        {
            MakeFood("Blue", 0, "Assets/Prefabs/Treats/Blue Treat.prefab", new Color(0.3f, 0.6f, 1f)),
            MakeFood("Orange", 1, "Assets/Prefabs/Treats/Orange Treat.prefab", new Color(1f, 0.6f, 0.15f)),
            MakeFood("Pink", 2, "Assets/Prefabs/Treats/Pink Treat.prefab", new Color(1f, 0.45f, 0.75f))
        };

        List<CustomerCat> customers = new List<CustomerCat>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { CustomerFolder }))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            CustomerCat cat = prefab != null ? prefab.GetComponent<CustomerCat>() : null;
            if (cat != null) customers.Add(cat);
        }
        log.AppendLine($"{customers.Count} customer cat types, {foods.Length} foods.");

        service.Setup(foods, customers.ToArray(), hands, register,
            Sound("Assets/Audio/UI/ChimeUp01.mp3", 0.5f),
            Sound("Assets/Audio/Treats/DullThunk.mp3", 0.8f),
            Sound("Assets/Audio/UI/Pop03.mp3", 0.5f));
        EditorUtility.SetDirty(service);
    }

    static SpaceCafeService.Food MakeFood(string name, int variation, string treatPath, Color colour)
    {
        return new SpaceCafeService.Food
        {
            name = name,
            treatPoolVariation = variation,
            treatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(treatPath),
            colour = colour
        };
    }

    static SoundData Sound(string path, float volume)
    {
        return new SoundData(AssetDatabase.LoadAssetAtPath<AudioClip>(path), volume, 1f);
    }
}
