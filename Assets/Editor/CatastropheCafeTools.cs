using System.Collections.Generic;
using System.IO;
using Liminal.SDK.VR;
using TMPro;
using Liminal.Platform.Experimental.App;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu tools for testing on a Valve Index as well as the Quest.
// The Quest build settings are never changed permanently: the Index build swaps the
// headset type only while it builds, then puts it back.
public static class CatastropheCafeTools
{
    const string ExperienceAppPrefabPath = "Assets/Prefabs/[ExperienceApp].prefab";
    const string IndexBuildPath = "Builds/Index/CatastropheCafe.exe";

    const string EditorIndexMenu = "Catastrophe Cafe/Editor Headset/Valve Index (SteamVR)";
    const string EditorMouseMenu = "Catastrophe Cafe/Editor Headset/Mouse Only (Emulator)";

    [MenuItem(EditorIndexMenu, false, 0)]
    static void UseIndexInEditor()
    {
        SetEmulatorType("EditorType", ESDKType.OpenVR);
        Debug.Log("[Catastrophe Cafe] Play mode will now use the Valve Index through SteamVR. Start SteamVR before pressing Play.");
    }

    [MenuItem(EditorMouseMenu, false, 1)]
    static void UseMouseInEditor()
    {
        SetEmulatorType("EditorType", ESDKType.Emulator);
        Debug.Log("[Catastrophe Cafe] Play mode will now use the mouse emulator.");
    }

    [MenuItem(EditorIndexMenu, true)]
    static bool UseIndexInEditorValidate()
    {
        Menu.SetChecked(EditorIndexMenu, GetEmulatorType("EditorType") == ESDKType.OpenVR);
        return true;
    }

    [MenuItem(EditorMouseMenu, true)]
    static bool UseMouseInEditorValidate()
    {
        Menu.SetChecked(EditorMouseMenu, GetEmulatorType("EditorType") == ESDKType.Emulator);
        return true;
    }

    // The object with a slot for each thing the game's scripts spawn (see CafePrefabs_HowTo.md)
    [MenuItem("Catastrophe Cafe/Add Cafe Prefabs To Scene", false, 5)]
    static void AddCafePrefabs()
    {
        CafePrefabs existing = Object.FindObjectOfType<CafePrefabs>();
        if (existing != null)
        {
            Selection.activeObject = existing.gameObject;
            Debug.Log("[Catastrophe Cafe] The scene already has a Cafe Prefabs object: selected it.");
            return;
        }
        GameObject made = new GameObject("Cafe Prefabs");
        made.AddComponent<CafePrefabs>();
        Undo.RegisterCreatedObjectUndo(made, "Add Cafe Prefabs");
        Selection.activeObject = made;
        Debug.Log("[Catastrophe Cafe] Cafe Prefabs added. Fill its slots with your prefabs; empty slots keep the built-in look. Save the scene.");
    }

    // The lists on the scene's Cafe Resources object (made if there isn't one): everything in the Resources folders,
    // which is what the game loads by name, and so what Liminal's app, which takes only what the scene refers to,
    // carries with it. Run it again whenever a sound, a line or a prefab is added to a Resources folder; save the scene.
    [MenuItem("Catastrophe Cafe/Fill Cafe Resources From The Resources Folders", false, 4)]
    static void FillCafeResources()
    {
        CafeResources holder = Object.FindObjectOfType<CafeResources>();
        if (holder == null)
        {
            GameObject made = new GameObject("Cafe Resources");
            holder = made.AddComponent<CafeResources>();
            Undo.RegisterCreatedObjectUndo(made, "Add Cafe Resources");
        }
        Undo.RecordObject(holder, "Fill Cafe Resources");

        string[] folders = ResourcesFolders();
        List<string> paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("", folders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!AssetDatabase.IsValidFolder(path) && !paths.Contains(path))
            {
                paths.Add(path);
            }
        }
        paths.Sort(System.StringComparer.Ordinal);

        List<AudioClip> sounds = new List<AudioClip>();
        List<GameObject> prefabs = new List<GameObject>();
        List<TextAsset> texts = new List<TextAsset>();
        List<Material> materials = new List<Material>();
        List<string> leftOut = new List<string>();
        List<string> clashes = new List<string>();
        Dictionary<string, string> seen = new Dictionary<string, string>();
        foreach (string path in paths)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset is AudioClip)
            {
                sounds.Add((AudioClip)asset);
            }
            else if (asset is GameObject)
            {
                prefabs.Add((GameObject)asset);
            }
            else if (asset is TextAsset)
            {
                texts.Add((TextAsset)asset);
            }
            else if (asset is Material)
            {
                materials.Add((Material)asset);
            }
            else
            {
                leftOut.Add(path);
                continue;
            }
            // The game finds a file by its name and type, so two of the same name in different folders would clash
            string key = asset.GetType().Name + ":" + asset.name.ToLowerInvariant();
            if (seen.ContainsKey(key))
            {
                clashes.Add(path + " and " + seen[key]);
            }
            else
            {
                seen[key] = path;
            }
        }

        holder.sounds = sounds;
        holder.prefabs = prefabs;
        holder.texts = texts;
        holder.materials = materials;
        EditorUtility.SetDirty(holder);
        EditorSceneManager.MarkSceneDirty(holder.gameObject.scene);
        Selection.activeObject = holder.gameObject;

        string notes = "";
        if (clashes.Count > 0)
        {
            notes += " Same name twice (the game takes the first): " + string.Join("; ", clashes.ToArray()) + ".";
        }
        if (leftOut.Count > 0)
        {
            notes += " Left out (not a sound, prefab, text or material; a shader comes along with its material): " + string.Join(", ", leftOut.ToArray()) + ".";
        }
        Debug.Log($"[Catastrophe Cafe] Cafe Resources: {sounds.Count} sound(s), {prefabs.Count} prefab(s), {texts.Count} text(s), {materials.Count} material(s) " +
                  $"from {string.Join(", ", folders)}. Save the scene.{notes}");
    }

    // Every Resources folder of ours: not TextMesh Pro's or the SDK's, which Liminal's app has of its own
    static string[] ResourcesFolders()
    {
        List<string> folders = new List<string>();
        foreach (string folder in Directory.GetDirectories("Assets", "Resources", SearchOption.AllDirectories))
        {
            string path = folder.Replace('\\', '/');
            if (path.StartsWith("Assets/TextMesh Pro/") || path.StartsWith("Assets/Liminal/"))
            {
                continue;
            }
            folders.Add(path);
        }
        return folders.ToArray();
    }

    const string PopUpWordPrefabPath = "Assets/Prefabs/UI/Pop-Up Word.prefab";
    const string CafeFontPath = "Assets/Art/Fonts/Salted Caramel SDF.asset";

    // A ready-made pop-up word prefab, the built-in look in the cafe's font, put straight into the Cafe Prefabs slot:
    // restyle it from there. The game sets each word's text, colour and size; the prefab sets the font, style and alignment.
    [MenuItem("Catastrophe Cafe/Make Pop-Up Word Prefab", false, 6)]
    static void MakePopUpWordPrefab()
    {
        GameObject made = new GameObject("Pop-Up Word");
        TextMeshPro text = made.AddComponent<TextMeshPro>();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CafeFontPath);
        if (font != null)
        {
            text.font = font;
        }
        text.text = "Pop-Up!";
        text.fontSize = 3;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.rectTransform.sizeDelta = new Vector2(2f, 1f);
        made.AddComponent<FloatingWord>();

        Directory.CreateDirectory(Path.GetDirectoryName(PopUpWordPrefabPath));
        string path = AssetDatabase.GenerateUniqueAssetPath(PopUpWordPrefabPath);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(made, path);
        Object.DestroyImmediate(made);

        string where = "Drag it into the Floating Word slot of the scene's Cafe Prefabs object.";
        CafePrefabs holder = Object.FindObjectOfType<CafePrefabs>();
        if (holder != null)
        {
            SerializedObject serialized = new SerializedObject(holder);
            SerializedProperty slot = serialized.FindProperty("floatingWord");
            if (slot.objectReferenceValue == null)
            {
                slot.objectReferenceValue = prefab.GetComponent<FloatingWord>();
                serialized.ApplyModifiedProperties();
                where = "It's in the Floating Word slot of Cafe Prefabs: save the scene.";
            }
            else
            {
                where = "The Floating Word slot of Cafe Prefabs already has a prefab, so this one was left out of it.";
            }
        }

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[Catastrophe Cafe] Made {path}. {where} Its Font Size is only a preview: the game sets each word's size.");
    }

    // The SDK's Platform Viewer scene loads a .limapp the way Liminal's app does, but its Play Limapp button needs a VR
    // pointer to click. This presses it from the menu while the viewer scene is playing.
    [MenuItem("Catastrophe Cafe/Play The Limapp In The Viewer", false, 8)]
    static void PlayLimappInViewer()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[Catastrophe Cafe] Open Assets/Liminal/PlatformViewer/Scenes/PlatformAppViewer, press Play, then run this.");
            return;
        }
        PlatformAppViewer viewer = Object.FindObjectOfType<PlatformAppViewer>();
        if (viewer == null)
        {
            Debug.LogWarning("[Catastrophe Cafe] No Platform App Viewer in the scene that is playing. This is for the PlatformAppViewer scene.");
            return;
        }
        string limapp = "";
        if (viewer.PreviewConfig != null)
        {
            limapp = viewer.PreviewConfig.EmulatorPath;
        }
        if (!System.IO.File.Exists(limapp))
        {
            Debug.LogWarning($"[Catastrophe Cafe] The viewer's config points at {limapp}, which isn't there. Build the Windows .limapp first (QuestBuild.BatchBuildLimappWindows) and copy it to that path.");
            return;
        }
        Debug.Log($"[Catastrophe Cafe] Loading {limapp} in the viewer...");
        viewer.Play();
    }

    // The special cats are prefabs with other prefabs nested inside them (the cat model, its mood canvas, its shadow,
    // its armour). Liminal's loader rebuilds our components on a prefab by walking its children by index, and with
    // prefabs nested inside, the order it meets at load isn't the order it recorded at build ("Child not found at
    // index" in the viewer's log), so the ninja, the spies and the armoured boss came out broken. Unpacked completely,
    // a prefab is plain objects in one file, in one order. Run this once (it edits the prefab files), then commit them.
    static readonly string[] SpecialCatPrefabs =
    {
        "Assets/Prefabs/CatChaos/Ninja Rush Cat.prefab",
        "Assets/Resources/SpyCat/Spy Phat Cat.prefab",
        "Assets/Prefabs/Cat/Armoured Boss Cat.prefab",
    };

    [MenuItem("Catastrophe Cafe/Unpack The Special Cat Prefabs (For The Liminal Loader)", false, 9)]
    static void UnpackSpecialCats()
    {
        foreach (string path in SpecialCatPrefabs)
        {
            UnpackNestedPrefabs(path);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Catastrophe Cafe] Special cat prefabs unpacked. Commit the three prefab files.");
    }

    // The same from the command line: -executeMethod CatastropheCafeTools.BatchUnpackSpecialCats
    public static void BatchUnpackSpecialCats()
    {
        UnpackSpecialCats();
        EditorApplication.Exit(0);
    }

    static void UnpackNestedPrefabs(string path)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        if (contents == null)
        {
            Debug.LogError($"[Catastrophe Cafe] {path} couldn't be opened.");
            return;
        }
        int unpacked = 0;
        List<string> names = new List<string>();
        List<GameObject> failed = new List<GameObject>();
        bool again = true;
        while (again)
        {
            // Outermost first: unpacking one flattens everything nested inside it, so the search starts over
            again = false;
            foreach (Transform child in contents.GetComponentsInChildren<Transform>(true))
            {
                if (failed.Contains(child.gameObject) || !PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                {
                    continue;
                }
                try
                {
                    PrefabUtility.UnpackPrefabInstance(child.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    unpacked++;
                    names.Add(child.name);
                    again = true;
                    break;
                }
                catch (System.Exception e)
                {
                    failed.Add(child.gameObject);
                    Debug.LogWarning($"[Catastrophe Cafe] {path}: {child.name} couldn't be unpacked: {e.Message}");
                }
            }
        }
        PrefabUtility.SaveAsPrefabAsset(contents, path);
        PrefabUtility.UnloadPrefabContents(contents);
        Debug.Log($"[Catastrophe Cafe] {path}: {unpacked} nested prefab instance(s) unpacked ({string.Join(", ", names.ToArray())}).");
    }

    // The catch phases: a held prop has to be brought back over its table. The marker there is Eleni's rainbow area
    // effect (the Home Zone slot of Cafe Prefabs) with a big arrow hanging over it (the Home Arrow slot). The arrow is
    // Brian's bobbing pointer from the Dual Launchers prefab, copied out as a flat prefab of its own, bigger.
    const string DualLaunchersPrefabPath = "Assets/Prefabs/Dual Launchers.prefab";
    const string LauncherPointerName = "Pointer Canvas";
    const string HomeArrowPrefabPath = "Assets/Prefabs/UI/Home Arrow.prefab";
    const string HomeZonePrefabPath = "Assets/Effects/Hyper Casual FX/Prefabs/Area/MASHEDArea_magic_multicolor 1.prefab";
    const float HomeArrowScale = 2.5f;

    [MenuItem("Catastrophe Cafe/Make Home Arrow Prefab From The Launcher Pointer", false, 6)]
    static void MakeHomeArrowPrefab()
    {
        GameObject prefab = MakeHomeArrow();
        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
    }

    static GameObject MakeHomeArrow()
    {
        GameObject launchers = AssetDatabase.LoadAssetAtPath<GameObject>(DualLaunchersPrefabPath);
        if (launchers == null)
        {
            Debug.LogError($"[Catastrophe Cafe] {DualLaunchersPrefabPath} not found.");
            return null;
        }
        Transform pointer = null;
        foreach (Transform child in launchers.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == LauncherPointerName)
            {
                pointer = child;
                break;
            }
        }
        if (pointer == null)
        {
            Debug.LogError($"[Catastrophe Cafe] No {LauncherPointerName} inside {DualLaunchersPrefabPath}.");
            return null;
        }
        GameObject copy = Object.Instantiate(pointer.gameObject);
        copy.name = "Home Arrow";
        copy.SetActive(true);
        if (PrefabUtility.IsPartOfPrefabInstance(copy))
        {
            // Flat, on purpose: Liminal's loader breaks on prefabs nested inside prefabs
            GameObject outermost = PrefabUtility.GetOutermostPrefabInstanceRoot(copy);
            if (outermost != null)
            {
                PrefabUtility.UnpackPrefabInstance(outermost, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
        }
        RectTransform rect = copy.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition3D = Vector3.zero;   // the pivot at the arrow itself: the game hangs it where it wants
            rect.localRotation = Quaternion.identity;
            rect.localScale = rect.localScale * HomeArrowScale;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(HomeArrowPrefabPath));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(copy, HomeArrowPrefabPath);
        Object.DestroyImmediate(copy);
        if (prefab == null)
        {
            Debug.LogError($"[Catastrophe Cafe] {HomeArrowPrefabPath} couldn't be saved.");
            return null;
        }
        Debug.Log($"[Catastrophe Cafe] Made {HomeArrowPrefabPath} from the launchers' {LauncherPointerName}, {HomeArrowScale}x the size. If it points up, turn it round on the prefab (Rotation Z 180).");
        return prefab;
    }

    // Puts the rainbow area and the arrow into the Home Zone and Home Arrow slots of the scene's Cafe Prefabs object
    // (making the arrow prefab first if it isn't there yet). Then save the scene.
    [MenuItem("Catastrophe Cafe/Wire The Catch Phase Prefabs (Home Zone + Arrow)", false, 6)]
    static void WireCatchPrefabs()
    {
        CafePrefabs holder = Object.FindObjectOfType<CafePrefabs>();
        if (holder == null)
        {
            Debug.LogError("[Catastrophe Cafe] The open scene has no Cafe Prefabs object (Catastrophe Cafe > Add Cafe Prefabs To Scene first).");
            return;
        }
        GameObject zone = AssetDatabase.LoadAssetAtPath<GameObject>(HomeZonePrefabPath);
        GameObject arrow = AssetDatabase.LoadAssetAtPath<GameObject>(HomeArrowPrefabPath);
        if (arrow == null)
        {
            arrow = MakeHomeArrow();
        }
        SerializedObject serialized = new SerializedObject(holder);
        string report = FillSlot(serialized, "homeZone", zone, "Home Zone", HomeZonePrefabPath)
                      + FillSlot(serialized, "homeArrow", arrow, "Home Arrow", HomeArrowPrefabPath);
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(holder.gameObject.scene);
        Selection.activeObject = holder.gameObject;
        Debug.Log($"[Catastrophe Cafe] Catch phase prefabs: {report}Save the scene (Ctrl+S), then it gets committed.");
    }

    static string FillSlot(SerializedObject serialized, string field, GameObject prefab, string label, string path)
    {
        SerializedProperty slot = serialized.FindProperty(field);
        if (slot == null)
        {
            return $"{label}: Cafe Prefabs has no {field} field. ";
        }
        if (prefab == null)
        {
            return $"{label}: {path} is missing, so the slot was left as it was. ";
        }
        Object before = slot.objectReferenceValue;
        slot.objectReferenceValue = prefab;
        if (before == prefab)
        {
            return $"{label}: already {prefab.name}. ";
        }
        if (before != null)
        {
            return $"{label}: {prefab.name} replaces {before.name}. ";
        }
        return $"{label}: {prefab.name}. ";
    }

    const string ShaderVariantsPath = "Assets/Settings/Cat Cafe Shader Variants.shadervariants";

    // The shaders the last Play session drew with, saved as a collection for the Rush Director's Shader Variants slot:
    // the game compiles them in the lobby, so the first boss cat (or anything new) doesn't stutter on the Quest.
    // Clear the tracked list first (Edit > Project Settings > Graphics > Shader Loading), play a whole round, stop, run this.
    [MenuItem("Catastrophe Cafe/Save Shader Variants From The Last Play", false, 7)]
    static void SaveShaderVariants()
    {
        System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        System.Reflection.MethodInfo save = typeof(ShaderUtil).GetMethod("SaveCurrentShaderVariantCollection", any);
        if (save == null)
        {
            Debug.LogWarning("[Catastrophe Cafe] This Unity can't save the tracked shaders from a menu. Use Edit > Project Settings > Graphics > Shader Loading > Save to asset, then drag the file into Rush Director > Shader Variants.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ShaderVariantsPath));
        save.Invoke(null, new object[] { ShaderVariantsPath });
        AssetDatabase.ImportAsset(ShaderVariantsPath);
        ShaderVariantCollection collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(ShaderVariantsPath);
        if (collection == null)
        {
            Debug.LogWarning($"[Catastrophe Cafe] Nothing was saved to {ShaderVariantsPath}. Play a whole round first, then stop and run this again.");
            return;
        }
        if (collection.variantCount == 0)
        {
            Debug.LogWarning($"[Catastrophe Cafe] {ShaderVariantsPath} is empty: Unity hadn't tracked any shaders. Play a whole round first, then stop and run this again.");
        }

        string where = "Drag it into Rush Director > Shader Variants, on the Cat Cafe Rush object, and save the scene.";
        RushDirector director = Object.FindObjectOfType<RushDirector>();
        if (director != null)
        {
            SerializedObject serialized = new SerializedObject(director);
            SerializedProperty slot = serialized.FindProperty("shaderVariants");
            if (slot != null && slot.objectReferenceValue == collection)
            {
                where = "The Rush Director's Shader Variants slot already points at it.";
            }
            else if (slot != null && slot.objectReferenceValue == null)
            {
                slot.objectReferenceValue = collection;
                serialized.ApplyModifiedProperties();
                where = "It's in the Rush Director's Shader Variants slot: save the scene.";
            }
            else if (slot != null)
            {
                where = "The Rush Director's Shader Variants slot has another collection, so this one was left out of it.";
            }
        }

        Selection.activeObject = collection;
        EditorGUIUtility.PingObject(collection);
        Debug.Log($"[Catastrophe Cafe] Saved {collection.shaderCount} shader(s), {collection.variantCount} variant(s), to {ShaderVariantsPath}. {where}");
    }

    [MenuItem("Catastrophe Cafe/Build for Valve Index (Windows)", false, 20)]
    static void BuildForIndex()
    {
        ESDKType originalBuildType = GetEmulatorType("BuildType");
        SetEmulatorType("BuildType", ESDKType.OpenVR);

        try
        {
            string[] scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("Build for Valve Index", "No scenes are enabled in Build Settings.", "OK");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(IndexBuildPath));

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = IndexBuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Catastrophe Cafe] Index build finished: {Path.GetFullPath(IndexBuildPath)}");
                EditorUtility.RevealInFinder(IndexBuildPath);
            }
            else
            {
                Debug.LogError($"[Catastrophe Cafe] Index build failed: {report.summary.result}. Check the Console for errors.");
            }
        }
        finally
        {
            // Always restore the Quest setting, even if the build failed
            SetEmulatorType("BuildType", originalBuildType);
        }
    }

    static string[] GetEnabledScenes()
    {
        System.Collections.Generic.List<string> scenes = new System.Collections.Generic.List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                scenes.Add(scene.path);
            }
        }
        return scenes.ToArray();
    }

    static VREmulator GetEmulator()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExperienceAppPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[Catastrophe Cafe] Couldn't find {ExperienceAppPrefabPath}.");
            return null;
        }

        VREmulator emulator = prefab.GetComponentInChildren<VREmulator>(true);
        if (emulator == null)
        {
            Debug.LogError($"[Catastrophe Cafe] {ExperienceAppPrefabPath} has no VR Emulator Setup component.");
        }
        return emulator;
    }

    static ESDKType GetEmulatorType(string propertyName)
    {
        VREmulator emulator = GetEmulator();
        if (emulator == null)
        {
            return ESDKType.Emulator;
        }
        if (propertyName == "BuildType")
        {
            return emulator.BuildType;
        }
        return emulator.EditorType;
    }

    static void SetEmulatorType(string propertyName, ESDKType type)
    {
        VREmulator emulator = GetEmulator();
        if (emulator == null)
        {
            return;
        }

        SerializedObject serialized = new SerializedObject(emulator);
        serialized.FindProperty(propertyName).intValue = (int)type;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(emulator);
        AssetDatabase.SaveAssets();
    }
}
