using System.IO;
using Liminal.SDK.VR;
using TMPro;
using Liminal.Platform.Experimental.App;
using UnityEditor;
using UnityEditor.Build.Reporting;
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
