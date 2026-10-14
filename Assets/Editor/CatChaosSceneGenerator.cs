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
    const float CookieWidth = 0.1f;
    const float CupHeight = 0.14f;
    const float JarHeight = 0.26f;

    const float RoundLength = 180f;
    const float MafiaArrives = 90f;

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

    // For running from the command line (Unity -batchmode -executeMethod CatChaosSceneGenerator.BatchCreate)
    public static void BatchCreate()
    {
        Create();
    }

    static void Create()
    {
        if (File.Exists(PrototypeScene)) AssetDatabase.DeleteAsset(PrototypeScene);
        if (!AssetDatabase.CopyAsset(SourceScene, PrototypeScene))
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

        BuildPiles(player, log);

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

        SetUpMafia(log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Debug.Log(log.ToString(), root);
    }

    // ---------- Piles ----------

    static void BuildPiles(PlayerInput player, StringBuilder log)
    {
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
            if (plates) BuildPlateStack(pile, propPile.props);
            else BuildCupsAndJars(pile, propPile.props);

            float angle = SignedAngle(forward, point.position - eye);
            log.AppendLine($"{(plates ? "Plates and cookies" : "Cups and jars")} ({propPile.props.Count} props) at {Mathf.Abs(angle):F0} deg {(angle >= 0 ? "right" : "left")}, " +
                           $"{Vector3.Distance(eye, point.position):F1} m away.");
        }
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
        PlaceProp(CookiePrefab, pile, new Vector3(-0.05f, y, 0.01f), Random.Range(0f, 360f), 0, CookieWidth, props);
        PlaceProp(CookiePrefab, pile, new Vector3(0.05f, y, -0.02f), Random.Range(0f, 360f), 0, CookieWidth, props);
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
        if (File.Exists(MafiaVariantPath)) AssetDatabase.DeleteAsset(MafiaVariantPath);

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

    static void SetSounds(CatChaosSettings settings)
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

    static float SignedAngle(Vector3 forward, Vector3 direction)
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
    static float FindFloor(Vector3 centre, PlayerInput player)
    {
        float floor = float.MinValue;
        foreach (RaycastHit hit in Physics.RaycastAll(centre + Vector3.up * 2f, Vector3.down, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(player.transform.root)) continue;
            if (hit.point.y <= centre.y + 0.05f && hit.point.y > floor) floor = hit.point.y;
        }
        return floor > float.MinValue ? floor : centre.y;
    }

    static bool TryGetBounds(GameObject go, out Bounds bounds)
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
