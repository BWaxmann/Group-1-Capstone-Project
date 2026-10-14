using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds the Cat Cafe Rush prototype from a copy of CC_LEVEL (Brian's room): the player in the middle facing the
// chalkboard, cats only in the half of the room in front, Feeding Frenzy colour orders on roaming cats, catch phases
// with the ninja knocking props about, a ninja boss finale, and all the info on the chalkboard.
// CC_LEVEL itself is never changed. It reuses the Cat Chaos pieces (piles, flying props, gravity beam, ninja).
public static class CatCafeRushGenerator
{
    const string SourceScene = "Assets/Scenes/CC_LEVEL.unity";
    const string RushScene = "Assets/Scenes/CatCafeRush_Prototype.unity";
    const string ChalkboardName = "Cat_Box5";
    const float RoundLength = 180f;
    const float ResultsScale = 0.6f;
    const string StartupMessage = "Feed the cats!\n<size=60%>Left trigger = Blue\nRight trigger = Orange\nGrip = catch</size>\n<size=45%>Point at the launchers and pull the trigger</size>";
    const string TinPrefab = "Assets/Models/Environment/Pet Assets/Asset/Prefabs/Food2 V1.prefab";
    const string BlueTreat = "Assets/Prefabs/Treats/Blue Treat.prefab";
    const string OrangeTreat = "Assets/Prefabs/Treats/Orange Treat.prefab";

    [MenuItem("Catastrophe Cafe/Cat Cafe Rush/Create Prototype Scene", false, 80)]
    static void CreateFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(RushScene) &&
            !EditorUtility.DisplayDialog("Cat Cafe Rush",
                "This makes a fresh copy of CC_LEVEL as CatCafeRush_Prototype, replacing the existing one " +
                "(and any changes made to it by hand). CC_LEVEL itself isn't changed.", "Make it", "Cancel"))
        {
            return;
        }
        Create();
    }

    // Command line: Unity -batchmode -executeMethod CatCafeRushGenerator.BatchCreate
    public static void BatchCreate()
    {
        Create();
    }

    static void Create()
    {
        // Copy CC_LEVEL over the prototype (keeping the prototype's own ID) or make it the first time
        if (File.Exists(RushScene))
        {
            File.Copy(SourceScene, RushScene, true);
            AssetDatabase.ImportAsset(RushScene, ImportAssetOptions.ForceUpdate);
        }
        else if (!AssetDatabase.CopyAsset(SourceScene, RushScene))
        {
            Debug.LogError($"[Cat Cafe Rush] Couldn't copy {SourceScene}.");
            return;
        }
        AssetDatabase.Refresh();
        Generate(EditorSceneManager.OpenScene(RushScene, OpenSceneMode.Single));
    }

    static void Generate(Scene scene)
    {
        PlayerInput player = Object.FindObjectOfType<PlayerInput>();
        GameManager gameManager = Object.FindObjectOfType<GameManager>();
        if (player == null || gameManager == null)
        {
            Debug.LogError("[Cat Cafe Rush] The scene needs the player rig (PlayerInput) and the GameManager.");
            return;
        }
        StringBuilder log = new StringBuilder("[Cat Cafe Rush] Prototype scene made from CC_LEVEL.\n");
        Random.InitState(20261014);

        // The player stands where Brian put them (the middle of the room), turned to face the chalkboard
        FacePlayerToward(player, ChalkboardName, log);
        Vector3 eye = player.transform.position;
        Vector3 forward = player.transform.forward;
        forward.y = 0;
        forward.Normalize();
        float floorY = CatChaosSceneGenerator.FindFloor(eye, player);

        // The launchers to pick up float just in front, a little below the chalkboard so they don't hide it
        foreach (GrabbableLaunchers launchers in Object.FindObjectsOfType<GrabbableLaunchers>())
        {
            launchers.transform.position = new Vector3(eye.x, floorY + 1.45f, eye.z) + forward * 1.4f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(launchers.transform);
        }

        GameObject root = new GameObject("Cat Cafe Rush");

        // Flying props: big and trailed so they're easy to spot. Cats don't knock them; only the ninja does.
        CatChaosSettings settings = root.AddComponent<CatChaosSettings>();
        settings.floorHeight = floorY;
        settings.airborneScale = 1.8f;
        settings.airborneTrail = true;
        settings.maxAirborne = 6;
        settings.pileTargetingChance = 0f;
        settings.knockChance = 0f;
        CatChaosSceneGenerator.SetSounds(settings);

        List<Transform> piles = CatChaosSceneGenerator.BuildPiles(player, log);
        List<Vector3> looseSpots = CatChaosSceneGenerator.BuildLooseProps(player, piles, floorY, log);

        // Grips catch (with a streak bonus); the yarnball moves to A/X
        GravityBeam beam = player.gameObject.AddComponent<GravityBeam>();
        beam.maxStreakMultiplier = 5;
        SerializedObject playerSettings = new SerializedObject(player);
        playerSettings.FindProperty("yarnballButton").enumValueIndex = (int)PlayerInput.YarnballButton.FaceButton;
        playerSettings.ApplyModifiedPropertiesWithoutUndo();

        // Keep the action in front: roaming waypoints beside or behind the player are dropped
        int dropped = CatChaosSceneGenerator.DropWaypointsBehind(eye, forward, out List<string> droppedNames);
        log.AppendLine($"Dropped {dropped} roaming waypoint(s) beside or behind the player ({string.Join(", ", droppedNames)}).");

        // The director spawns the cats, so Brian's wave spawners and boss spawner are off in this scene
        List<Transform> doors = new List<Transform>();
        foreach (SpawnManager spawnManager in Object.FindObjectsOfType<SpawnManager>())
        {
            Transform spawn = new SerializedObject(spawnManager).FindProperty("spawnPointCentre").objectReferenceValue as Transform;
            if (spawn != null && CatChaosSceneGenerator.InFront(eye, forward, spawn.position, 100f)) doors.Add(spawn);
            spawnManager.gameObject.SetActive(false);
        }
        GameObject door = GameObject.Find("Waypoints/Door");
        if (door != null && CatChaosSceneGenerator.InFront(eye, forward, door.transform.position, 100f)) doors.Add(door.transform);
        BossCatSpawner bossSpawner = Object.FindObjectOfType<BossCatSpawner>();
        if (bossSpawner != null)
        {
            SerializedObject so = new SerializedObject(bossSpawner);
            so.FindProperty("bossSpawnTimes").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        RushDirector director = root.AddComponent<RushDirector>();
        foreach (ObjectSpawner spawner in Object.FindObjectsOfType<ObjectSpawner>())
        {
            if (spawner.gameObject.name == "Cat Pool") director.catPool = spawner;
        }
        director.doors = doors;
        director.leftTreatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlueTreat);
        director.rightTreatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrangeTreat);
        director.tinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TinPrefab);
        director.ninjaPrefab = CatChaosSceneGenerator.MakeNinjaRush();
        foreach (Transform pile in piles) AddStop(director.ninjaStops, pile.position);
        for (int i = 0; i < looseSpots.Count && i < 5; i++) AddStop(director.ninjaStops, looseSpots[i]);
        log.AppendLine($"Director: {doors.Count} door(s) for cats in front ({string.Join(", ", doors.ConvertAll(d => d.name))}); " +
                       $"{(director.catPool != null ? "Cat Pool found" : "NO Cat Pool found")}; ninja with {director.ninjaStops.Count} stops.");

        SerializedObject gameSettings = new SerializedObject(gameManager);
        gameSettings.FindProperty("gameTime").floatValue = RoundLength;
        gameSettings.FindProperty("finaleBannerText").stringValue = "";   // the director announces the boss instead
        gameSettings.ApplyModifiedPropertiesWithoutUndo();

        PutAllInfoOnChalkboard(root.transform, gameManager, log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Debug.Log(log.ToString(), root);
    }

    static void AddStop(List<Vector3> stops, Vector3 near)
    {
        if (UnityEngine.AI.NavMesh.SamplePosition(near, out UnityEngine.AI.NavMeshHit hit, 0.8f, UnityEngine.AI.NavMesh.AllAreas)) stops.Add(hit.position);
    }

    static void FacePlayerToward(PlayerInput player, string objectName, StringBuilder log)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null || !CatChaosSceneGenerator.TryGetBounds(target, out Bounds b)) return;
        Vector3 direction = b.center - player.transform.position;
        direction.y = 0;
        if (direction.sqrMagnitude < 0.01f) return;
        player.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
        log.AppendLine($"Player starts facing {objectName}.");
    }

    // The countdown, announcements and results show on the chalkboard (as in Feeding Frenzy); the timer stays on
    // the cat clock and the floating instructions board is switched off
    static void PutAllInfoOnChalkboard(Transform root, GameManager gameManager, StringBuilder log)
    {
        SerializedObject settings = new SerializedObject(gameManager);
        TMPro.TMP_Text scoreText = settings.FindProperty("scoreText").objectReferenceValue as TMPro.TMP_Text;
        if (scoreText == null || scoreText.canvas == null)
        {
            log.AppendLine("Couldn't find the score canvas, so the UI was left where it was.");
            return;
        }

        // An anchor exactly on the board; the canvases are attached to it
        Transform board = scoreText.canvas.rootCanvas.transform;
        Transform anchor = new GameObject("Chalkboard UI").transform;
        anchor.SetParent(root, false);
        anchor.SetPositionAndRotation(board.position, board.rotation);

        // A world-space canvas faces the player along its -Z, so "in front of the board" is a little towards -Z
        foreach (string canvasProperty in new[] { "pregameCanvas", "postgameCanvas" })
        {
            GameObject canvas = settings.FindProperty(canvasProperty).objectReferenceValue as GameObject;
            if (canvas != null) AttachToBoard(canvas.transform, anchor, Vector3.back * 0.03f);
        }

        // The results are taller than the board, so shrink them to fit, and drop the old "Penalty" line
        GameObject results = settings.FindProperty("postgameCanvas").objectReferenceValue as GameObject;
        if (results != null)
        {
            results.transform.localScale = results.transform.localScale * ResultsScale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(results.transform);
            foreach (Transform t in results.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Defend Point Penalty" || t.name == "Penalty Value") t.gameObject.SetActive(false);
            }
        }

        InstructionsText instructions = Object.FindObjectOfType<InstructionsText>();
        if (instructions != null)
        {
            Canvas instructionsCanvas = instructions.GetComponentInParent<Canvas>();
            (instructionsCanvas != null ? instructionsCanvas.rootCanvas.gameObject : instructions.gameObject).SetActive(false);
        }

        settings.FindProperty("startupMessage").stringValue = StartupMessage;
        settings.FindProperty("bannerReplacesGameUI").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();

        // The countdown text is sized for "3, 2, 1, GO!". Auto-size lets longer messages shrink to fit the board
        // while short ones stay big. It also shows the start message in the editor, as it will in the game.
        TMPro.TMP_Text countdown = settings.FindProperty("countdownText").objectReferenceValue as TMPro.TMP_Text;
        if (countdown != null)
        {
            SerializedObject text = new SerializedObject(countdown);
            text.FindProperty("m_enableAutoSizing").boolValue = true;
            text.FindProperty("m_fontSizeMin").floatValue = 20f;
            text.FindProperty("m_fontSizeMax").floatValue = 128f;
            text.FindProperty("m_text").stringValue = StartupMessage;
            text.ApplyModifiedPropertiesWithoutUndo();

            // Use more of the board, so messages come out bigger
            RectTransform area = countdown.rectTransform;
            area.sizeDelta = new Vector2(Mathf.Max(area.sizeDelta.x, 430f), Mathf.Max(area.sizeDelta.y, 250f));
            PrefabUtility.RecordPrefabInstancePropertyModifications(area);
        }
        log.AppendLine("Countdown, announcements and results show on the chalkboard; the instructions board is off.");
    }

    static void AttachToBoard(Transform canvas, Transform anchor, Vector3 localPosition)
    {
        canvas.SetParent(anchor, true);
        canvas.localPosition = localPosition;
        canvas.localRotation = Quaternion.identity;
        // UI canvases load their position from the anchored position, so set that to match too
        RectTransform rect = canvas as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.anchoredPosition3D = localPosition;
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
    }
}
