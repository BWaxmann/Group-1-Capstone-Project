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
        CatChaosSceneGenerator.SetSingleRound(gameSettings, RoundLength);
        gameSettings.FindProperty("finaleBannerText").stringValue = "";   // the director announces the boss instead
        gameSettings.ApplyModifiedPropertiesWithoutUndo();

        PutAllInfoOnChalkboard(root.transform, gameManager, log);
        AttachTimerToClock(log);

        // The launchers to pick up float just in front, a little below the chalkboard so they don't hide it.
        // (Done last: their invisible hit sphere would otherwise block the "can the player see it?" checks above.)
        foreach (GrabbableLaunchers launchers in Object.FindObjectsOfType<GrabbableLaunchers>())
        {
            launchers.transform.position = new Vector3(eye.x, floorY + 1.45f, eye.z) + forward * 1.4f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(launchers.transform);
        }

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

        // Hang the anchor and the score on the chalkboard itself, so moving the chalkboard takes all its writing with it
        GameObject chalkboard = GameObject.Find(ChalkboardName);
        if (chalkboard != null)
        {
            anchor.SetParent(chalkboard.transform, true);
            AttachKeepingPlace(board, chalkboard.transform);
            log.AppendLine("The chalkboard's writing is attached to the chalkboard (move the board and it all goes too).");
        }

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

    // The time stays on the cat clock's screen: its board is attached to the clock, so moving, turning or resizing
    // the clock takes the time with it (already attached = left alone)
    static void AttachTimerToClock(StringBuilder log)
    {
        GameObject timer = GameObject.Find("Timer Canvas");
        GameObject clock = GameObject.Find("Cat_clock");
        if (timer == null || clock == null)
        {
            log.AppendLine("Couldn't find the cat clock or its timer; the time stays where it is.");
            return;
        }
        if (timer.transform.IsChildOf(clock.transform)) return;
        AttachKeepingPlace(timer.transform, clock.transform);
        log.AppendLine("The timer is attached to the cat clock (move the clock and the time goes with it).");
    }

    // Re-parents a canvas without moving it on screen
    static void AttachKeepingPlace(Transform canvas, Transform newParent)
    {
        Vector3 position = canvas.position;
        Quaternion rotation = canvas.rotation;
        canvas.SetParent(newParent, true);
        canvas.SetPositionAndRotation(position, rotation);
        RectTransform rect = canvas as RectTransform;
        if (rect != null)
        {
            // UI canvases load their position from the anchored position, so set that to match too
            Vector3 local = rect.localPosition;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.anchoredPosition3D = local;
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
    }

    // ---------- Trial scene ----------

    const string TrialScene = "Assets/Scenes/CatCafeRushTrial.unity";

    // A copy of Cat Cafe Rush (layout and all) with Eleni's trial ideas switched on: in feeding phases hungry cats
    // attack the piles (each swipe tips a prop over, and the pile's health bar shows what's still standing), and in
    // the boss finale each hand can swap between Single, Scatter and Buckshot. Cat Cafe Rush itself is unchanged.
    [MenuItem("Catastrophe Cafe/Cat Cafe Rush/Create Trial Scene (Copy Of Rush)", false, 90)]
    static void CreateTrialFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(TrialScene) &&
            !EditorUtility.DisplayDialog("Cat Cafe Rush", "This replaces CatCafeRushTrial with a fresh copy of CatCafeRush_Prototype " +
                                         "(and any changes made to the trial by hand).", "Make it", "Cancel"))
        {
            return;
        }
        EditorUtility.DisplayDialog("Cat Cafe Rush", CreateTrial(), "OK");
    }

    // Command line: -executeMethod CatCafeRushGenerator.BatchCreateTrial
    public static void BatchCreateTrial()
    {
        Debug.Log("[Cat Cafe Rush] " + CreateTrial());
    }

    static string CreateTrial()
    {
        if (File.Exists(TrialScene))
        {
            File.Copy(RushScene, TrialScene, true);
            AssetDatabase.ImportAsset(TrialScene, ImportAssetOptions.ForceUpdate);
        }
        else if (!AssetDatabase.CopyAsset(RushScene, TrialScene))
        {
            return $"Couldn't copy {RushScene}.";
        }
        AssetDatabase.Refresh();
        Scene scene = EditorSceneManager.OpenScene(TrialScene, OpenSceneMode.Single);

        RushDirector director = Object.FindObjectOfType<RushDirector>();
        if (director == null) return "No RushDirector in the copy.";
        director.attacksTopple = true;
        director.finaleFireStyles = true;
        // Attacks get a little more likely as the round goes
        float[] attackChances = { 0.3f, 0.35f, 0.4f };
        int feed = 0;
        foreach (RushDirector.Phase phase in director.phases)
        {
            if (phase.kind == RushDirector.Kind.Feed) phase.attackChance = attackChances[Mathf.Min(feed++, attackChances.Length - 1)];
        }
        EditorUtility.SetDirty(director);

        // The piles' health bars come back (they show how much of each pile is still standing)
        int bars = 0;
        foreach (DefendPoint point in Object.FindObjectsOfType<DefendPoint>())
        {
            UnityEngine.UI.Slider slider = point.GetComponentInChildren<UnityEngine.UI.Slider>(true);
            if (slider == null) continue;
            Canvas[] canvases = slider.GetComponentsInParent<Canvas>(true);
            Canvas canvas = canvases.Length > 0 ? canvases[canvases.Length - 1] : null;
            (canvas != null ? canvas.gameObject : slider.gameObject).SetActive(true);
            bars++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"CatCafeRushTrial made from Cat Cafe Rush: cats attack piles in feeding phases (30/35/40%), swipes tip props over, " +
               $"{bars} pile health bar(s) shown, and fire styles in the finale (flick the thumbsticks).";
    }

    // ---------- Cat Cafe Waves ----------

    const string WavesScene = "Assets/Scenes/CatCafeWaves.unity";
    const string WavesFinaleBanner = "NINJA FINALE!\n<size=55%>Save the piles: catch everything!</size>";
    const string WavesFinaleLine = "catch_2";   // until there's a finale line of its own
    const string WavesStartupMessage = "Feed the cats!\n<size=60%>Left trigger = Blue\nRight trigger = Orange\nAlternative trigger = catch</size>\n" +
                                       "<size=45%>Point at the launchers and pull the trigger</size>";

    // A copy of the Trial (layout, Eleni's ideas and all) that plays a run of short, different waves.
    // Cat Cafe Rush and the Trial are unchanged.
    [MenuItem("Catastrophe Cafe/Cat Cafe Rush/Create Waves Scene (Copy Of Trial)", false, 91)]
    static void CreateWavesFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(WavesScene) &&
            !EditorUtility.DisplayDialog("Cat Cafe Waves", "This replaces CatCafeWaves with a fresh copy of CatCafeRushTrial " +
                                         "(and any changes made to it by hand).", "Make it", "Cancel"))
        {
            return;
        }
        EditorUtility.DisplayDialog("Cat Cafe Waves", CreateWaves(), "OK");
    }

    // Command line: -executeMethod CatCafeRushGenerator.BatchCreateWaves
    public static void BatchCreateWaves()
    {
        Debug.Log("[Cat Cafe Waves] " + CreateWaves());
    }

    static string CreateWaves()
    {
        if (File.Exists(WavesScene))
        {
            File.Copy(TrialScene, WavesScene, true);
            AssetDatabase.ImportAsset(WavesScene, ImportAssetOptions.ForceUpdate);
        }
        else if (!AssetDatabase.CopyAsset(TrialScene, WavesScene))
        {
            return $"Couldn't copy {TrialScene}.";
        }
        AssetDatabase.Refresh();
        Scene scene = EditorSceneManager.OpenScene(WavesScene, OpenSceneMode.Single);

        RushDirector director = Object.FindObjectOfType<RushDirector>();
        if (director == null) return "No RushDirector in the copy.";
        string ninjaBanner = WavesFinaleBanner;
        director.phases = new List<RushDirector.Phase>
        {
            new RushDirector.Phase { kind = RushDirector.Kind.Feed, seconds = 35, cats = 3, shortestOrder = 1, longestOrder = 1,
                                     raidEvery = 10, banner = "FEED THE CATS!", line = "feed_first" },
            new RushDirector.Phase { kind = RushDirector.Kind.Stampede, seconds = 20, banner = "KITTEN STAMPEDE!", line = "stampede" },
            new RushDirector.Phase { kind = RushDirector.Kind.Feed, seconds = 20, cats = 4, shortestOrder = 1, longestOrder = 2,
                                     raidEvery = 7, banner = "COMBO ORDERS!", line = "feed_combo" },
            new RushDirector.Phase { kind = RushDirector.Kind.Feed, seconds = 15, cats = 4, shortestOrder = 2, longestOrder = 3,
                                     raidEvery = 7, banner = "BIGGER APPETITES!", line = "feed_three" },
            new RushDirector.Phase { kind = RushDirector.Kind.Catch, seconds = 15, banner = "CATCH!\n<size=55%>Use the alternative trigger to catch what flies</size>",
                                     line = "catch" },
            new RushDirector.Phase { kind = RushDirector.Kind.PopUp, seconds = 30, banner = "POP-UP CATS!", line = "popup" },
            new RushDirector.Phase { kind = RushDirector.Kind.Boss, seconds = 45, cats = 1, shortestOrder = 1, longestOrder = 2, banner = ninjaBanner,
                                     line = WavesFinaleLine },
        };
        // The manager on the speaker, fed cats leaving through holes, tips, and raids (instead of random pile attacks)
        director.announcer = true;
        director.fedCatsUseHoles = true;
        director.entryArc = 70f;   // cats come in through the holes in front, not the doors off to the side
        director.coinsPerTreat = 1;
        director.raids = true;
        director.catSpeedMultiplier = 1.3f;
        // Catching is taught the first time (one glowing prop hangs in the air until it's caught) and props glow
        director.catchIntro = true;
        director.catchGlow = true;
        // The ninja and the piles are the catching stake: it blows whole piles up, misses stay broken, the finale is
        // all mess (no feeding it) and each pile pays a tidy bonus at closing. Catching is a little stricter.
        director.ninjaExplodesPiles = true;
        director.missedPropsStayDown = true;
        director.bossMakesMess = true;
        director.finaleFireStyles = false;
        director.tidyBonusCoins = 10;
        director.tidyBonusScore = 30;
        director.catchAimAngle = 14f;
        director.fallSpeed = 0.65f;
        // The tip jar takes the place of the coffee bottle on the counter, a bit bigger; raids go for it
        director.tipJarReplaces = "Cafe_Bottle_with_Coffee_3";
        director.tipJarScale = 1.4f;
        director.wrongColourLeaves = true;
        EditorUtility.SetDirty(director);

        // No yarnball in Waves for now (it has no voice line and doesn't mean much in this version)
        PlayerInput player = Object.FindObjectOfType<PlayerInput>();
        if (player != null)
        {
            SerializedObject playerSettings = new SerializedObject(player);
            playerSettings.FindProperty("yarnballButton").enumValueIndex = (int)PlayerInput.YarnballButton.Off;
            playerSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        // The round lasts exactly as long as the waves
        float total = 0;
        foreach (RushDirector.Phase phase in director.phases) total += phase.seconds;
        GameManager gameManager = Object.FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            SerializedObject gameSettings = new SerializedObject(gameManager);
            CatChaosSceneGenerator.SetSingleRound(gameSettings, total);
            gameSettings.FindProperty("startupMessage").stringValue = WavesStartupMessage;
            gameSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"CatCafeWaves made from the Trial: Feed > Stampede > Combo > Bigger appetites > Catch > Pop-up > Boss, {total:F0} s " +
               $"(round timer {(gameManager != null ? "set" : "NOT set, no GameManager found")}). " +
               "Announcer, cat holes and tip jar on. Stampede and Pop-up play as feeding until they're built.";
    }

    // ---------- Updating a hand-made layout ----------

    // Turns the player (wherever they stand) to face the chalkboard. Run Update Open Scene afterwards.
    [MenuItem("Catastrophe Cafe/Cat Cafe Rush/Face Player Toward Chalkboard", false, 82)]
    static void FacePlayerFromMenu()
    {
        PlayerInput player = Object.FindObjectOfType<PlayerInput>();
        if (player == null) return;
        StringBuilder log = new StringBuilder();
        FacePlayerToward(player, ChalkboardName, log);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Cat Cafe Rush", log.Length > 0 ? log + "Now run Update Open Scene (Keeps Layout)." : "Couldn't find the chalkboard.", "OK");
    }

    // Command line: turn the player toward the chalkboard, then update (-executeMethod CatCafeRushGenerator.BatchFaceAndUpdate)
    public static void BatchFaceAndUpdate()
    {
        EditorSceneManager.OpenScene(RushScene, OpenSceneMode.Single);
        PlayerInput player = Object.FindObjectOfType<PlayerInput>();
        StringBuilder log = new StringBuilder();
        if (player != null) FacePlayerToward(player, ChalkboardName, log);
        Debug.Log("[Cat Cafe Rush] " + log + UpdateLayout());
    }

    // Keeps the open Cat Cafe Rush scene exactly as laid out (anything moved, copied or added by hand) and fits the
    // cats to it: what's in front of the player, doors, the ninja's stops, piles, tables and the NavMesh.
    [MenuItem("Catastrophe Cafe/Cat Cafe Rush/Update Open Scene (Keeps Layout)", false, 81)]
    static void UpdateFromMenu()
    {
        EditorUtility.DisplayDialog("Cat Cafe Rush", UpdateLayout(), "OK");
    }

    // Command line: -executeMethod CatCafeRushGenerator.BatchUpdate [-scene <path>]
    public static void BatchUpdate()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int at = System.Array.IndexOf(args, "-scene");
        EditorSceneManager.OpenScene(at >= 0 && at + 1 < args.Length ? args[at + 1] : RushScene, OpenSceneMode.Single);
        Debug.Log("[Cat Cafe Rush] " + UpdateLayout());
    }

    static string UpdateLayout()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path == SourceScene) return "This is CC_LEVEL. Open CatCafeRush_Prototype first.";
        PlayerInput player = Object.FindObjectOfType<PlayerInput>();
        RushDirector director = Object.FindObjectOfType<RushDirector>();
        if (player == null || director == null) return "This doesn't look like a Cat Cafe Rush scene (no player or RushDirector).";

        StringBuilder done = new StringBuilder();
        Vector3 eye = player.transform.position;
        Vector3 forward = player.transform.forward;
        forward.y = 0;
        forward.Normalize();
        float floorY = CatChaosSceneGenerator.FindFloor(eye, player);

        GameObject chalkboard = GameObject.Find(ChalkboardName);
        if (chalkboard != null && Mathf.Abs(CatChaosSceneGenerator.SignedAngle(forward, chalkboard.transform.position - eye)) > 90f)
        {
            done.AppendLine("NOTE: the chalkboard is behind the player, so \"in front\" may be the wrong way. " +
                            "Use Face Player Toward Chalkboard (or turn [ExperienceApp]), then run this again.");
        }

        // The launchers to pick up float just in front of wherever the player now stands
        foreach (GrabbableLaunchers launchers in Object.FindObjectsOfType<GrabbableLaunchers>())
        {
            launchers.transform.position = new Vector3(eye.x, floorY + 1.45f, eye.z) + forward * 1.4f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(launchers.transform);
        }

        // Props: copied ones get a pile of their own, moved piles are re-centred
        int adopted = CatChaosSceneGenerator.AdoptStrayProps();
        int recentred = 0;
        foreach (PropPile pile in Object.FindObjectsOfType<PropPile>())
        {
            if (CatChaosSceneGenerator.RecentrePile(pile)) recentred++;
        }
        done.AppendLine($"Props: {adopted} new pile(s) from copied props, {recentred} pile(s) re-centred.");

        // Tables added by hand: cats can jump up and visit them
        int tables = 0;
        foreach (GameObject table in CatChaosSceneGenerator.FindTables())
        {
            if (CatChaosSceneGenerator.FitTable(table, eye, floorY)) tables++;
        }
        done.AppendLine($"Tables: {tables} new table(s) given jump links and a waypoint on top.");

        // Where cats wander and come in: only places in front of the player (picked afresh from all of them,
        // so turning the player round brings back ones dropped earlier)
        WaypointManager waypoints = Object.FindObjectOfType<WaypointManager>();
        List<Transform> roam = new List<Transform>();
        List<Transform> doors = new List<Transform>();
        if (waypoints != null)
        {
            foreach (Transform place in waypoints.transform)
            {
                if (place.childCount > 0 || place.GetComponent<NinjaWaypoints>() != null) continue;   // the ninja's own group
                if (CatChaosSceneGenerator.InFront(eye, forward, place.position, 80f)) roam.Add(place);
                if ((place.name.StartsWith("Spawn") || place.name == "Door") && CatChaosSceneGenerator.InFront(eye, forward, place.position, 100f)) doors.Add(place);
            }
            if (roam.Count >= 3)
            {
                SerializedObject so = new SerializedObject(waypoints);
                SerializedProperty list = so.FindProperty("roamingWaypoints");
                list.arraySize = roam.Count;
                for (int i = 0; i < roam.Count; i++)
                {
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("_waypoint").objectReferenceValue = roam[i];
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("_inFrontOfPlayer").boolValue = true;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        if (doors.Count > 0) director.doors = doors;
        done.AppendLine($"Cats wander between {roam.Count} places in front ({string.Join(", ", roam.ConvertAll(t => t.name))})" +
                        (roam.Count < 3 ? " - too few, so the old list was kept" : "") +
                        $" and come in through {director.doors.Count} door(s) ({string.Join(", ", director.doors.ConvertAll(t => t != null ? t.name : "?"))}).");

        // The NavMesh for this scene only (CC_LEVEL keeps its own)
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

        // The ninja appears by the piles and loose props in front
        director.ninjaStops.Clear();
        foreach (PropPile pile in Object.FindObjectsOfType<PropPile>())
        {
            if (pile.pile != null && CatChaosSceneGenerator.InFront(eye, forward, pile.pile.position, 90f)) AddStop(director.ninjaStops, pile.pile.position);
        }
        int loose = 0;
        foreach (FlyingProp prop in Object.FindObjectsOfType<FlyingProp>())
        {
            if (prop.isLoose && loose < 5 && CatChaosSceneGenerator.InFront(eye, forward, prop.transform.position, 80f))
            {
                AddStop(director.ninjaStops, prop.transform.position);
                loose++;
            }
        }
        if (director.ninjaPrefab == null) director.ninjaPrefab = CatChaosSceneGenerator.MakeNinjaRush();
        EditorUtility.SetDirty(director);
        done.AppendLine($"NavMesh rebaked for this scene. Ninja: {director.ninjaStops.Count} stops.");
        StringBuilder timerLog = new StringBuilder();
        AttachTimerToClock(timerLog);
        done.Append(timerLog);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return done + "Layout left as it was. Scene saved.";
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
