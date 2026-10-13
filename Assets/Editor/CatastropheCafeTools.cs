using System.IO;
using Liminal.SDK.VR;
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

    // Adds a cat hole where the Scene view is looking. Move it to the bottom-middle of the
    // opening and point its blue arrow (Z) out into the room.
    [MenuItem("GameObject/Catastrophe Cafe/Cat Hole", false, 10)]
    static void CreateCatHole(MenuCommand command)
    {
        GameObject hole = new GameObject("Cat Hole");
        GameObjectUtility.SetParentAndAlign(hole, command.context as GameObject);
        if (command.context == null && SceneView.lastActiveSceneView != null)
        {
            hole.transform.position = SceneView.lastActiveSceneView.pivot;
        }
        hole.AddComponent<CatHole>();

        Undo.RegisterCreatedObjectUndo(hole, "Create Cat Hole");
        Selection.activeObject = hole;
    }

    static string[] GetEnabledScenes()
    {
        var scenes = new System.Collections.Generic.List<string>();
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
        if (emulator == null) return ESDKType.Emulator;
        return propertyName == "BuildType" ? emulator.BuildType : emulator.EditorType;
    }

    static void SetEmulatorType(string propertyName, ESDKType type)
    {
        VREmulator emulator = GetEmulator();
        if (emulator == null) return;

        SerializedObject serialized = new SerializedObject(emulator);
        serialized.FindProperty(propertyName).intValue = (int)type;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(emulator);
        AssetDatabase.SaveAssets();
    }
}
