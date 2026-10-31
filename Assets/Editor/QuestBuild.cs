using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Liminal.SDK.VR;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

// Builds a prototype (the Space Cafe, or Cat Cafe Waves) as a normal Android APK that peers can sideload onto a standalone Quest.
// The Liminal SDK's own Build Window only makes .limapp packages for the Liminal platform app, which
// testers don't have; a plain APK works because [ExperienceApp] starts itself when there's no platform.
//
// Everything this changes for the build (scripting backend, 64-bit, graphics API, VR SDK list, signing,
// headset type) is put back afterwards, so the team's project settings and build list are untouched.
// Menu: Catastrophe Cafe > Build Space Cafe for Quest (APK), or Build Cat Cafe Waves for Quest (APK)
// Command line: Unity -batchmode -quit -buildTarget Android -executeMethod QuestBuild.BatchBuild (or BatchBuildWaves)
public static class QuestBuild
{
    const string SpaceCafeScene = "Assets/Scenes/SpaceCafe_Prototype.unity";
    const string WavesScene = "Assets/Scenes/CatCafeWaves.unity";
    const string OutputFolder = "Builds/Quest";
    const string KeystorePath = OutputFolder + "/catcafe-playtest.keystore";
    const string KeystorePasswordPath = OutputFolder + "/catcafe-playtest.keystore.password";
    const string KeyAlias = "catcafe";

    // Set while our build runs, so the manifest tweak below only touches our builds
    internal static bool building;

    [MenuItem("Catastrophe Cafe/Build Space Cafe for Quest (APK)", false, 21)]
    static void BuildFromMenu()
    {
        BuildFromMenu(SpaceCafeScene, "FeedingFrenzy");
    }

    [MenuItem("Catastrophe Cafe/Build Cat Cafe Waves for Quest (APK)", false, 22)]
    static void BuildWavesFromMenu()
    {
        BuildFromMenu(WavesScene, "Waves");
    }

    static void BuildFromMenu(string scene, string name)
    {
        if (!CheckAndroidTools(interactive: true))
        {
            return;
        }

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            bool switchNow = EditorUtility.DisplayDialog("Build for Quest",
                "The project needs to switch to Android first. Unity will re-import everything, which can take 5-15 minutes " +
                "(and the same again when you switch back to Windows for the Index).\n\nSwitch to Android now?",
                "Switch to Android", "Cancel");
            if (!switchNow)
            {
                return;
            }
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[Quest Build] Couldn't switch the project to Android.");
                return;
            }

            // The Oculus build steps only load once Unity has recompiled for Android, so don't build in this same click
            EditorUtility.DisplayDialog("Build for Quest",
                "Switched to Android. When Unity has finished compiling, choose the Quest build in the Catastrophe Cafe menu again to build.",
                "OK");
            return;
        }
        if (EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("Build for Quest", "Unity is still compiling scripts. Try again when it finishes.", "OK");
            return;
        }

        string apk = Build(scene, name);
        if (apk != null)
        {
            EditorUtility.RevealInFinder(apk);
        }
    }

    // For command-line builds (e.g. from a separate Quest-only copy of the project)
    public static void BatchBuild()
    {
        BatchBuild(SpaceCafeScene, "FeedingFrenzy");
    }

    public static void BatchBuildWaves()
    {
        BatchBuild(WavesScene, "Waves");
    }

    // A Liminal-like build: Mono, 32-bit, the way Liminal's app runs experiences (it loads them as managed code, so it
    // can't be IL2CPP). Slower than the usual IL2CPP build: this is the one to test frame rate with. (Liminal's quality
    // settings, e.g. 4x MSAA, aren't applied here; the real check is the .limapp inside their app.)
    static bool monoBuild;

    public static void BatchBuildWavesMono()
    {
        monoBuild = true;
        BatchBuild(WavesScene, "Waves-Mono");
    }

    static void BatchBuild(string scene, string name)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.LogError("[Quest Build] Run with -buildTarget Android so the editor scripts are compiled for Android.");
            EditorApplication.Exit(1);
            return;
        }
        bool ok = CheckAndroidTools(interactive: false) && Build(scene, name) != null;
        EditorApplication.Exit(ok ? 0 : 1);
    }

    // Returns the APK path, or null if the build failed
    static string Build(string scene, string name)
    {
        if (!File.Exists(scene))
        {
            Debug.LogError($"[Quest Build] {scene} is missing.");
            return null;
        }
        if (!EnsureKeystore(out string password))
        {
            return null;
        }

        // Remember everything we change, so it can all be put back
        ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android);
        AndroidArchitecture architectures = PlayerSettings.Android.targetArchitectures;
        bool defaultGraphics = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
        GraphicsDeviceType[] graphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        AndroidSdkVersions targetSdk = PlayerSettings.Android.targetSdkVersion;
        string[] vrSdks = PlayerSettings.GetVirtualRealitySDKs(BuildTargetGroup.Android);
        bool customKeystore = PlayerSettings.Android.useCustomKeystore;
        string keystoreName = PlayerSettings.Android.keystoreName;
        string keystorePass = PlayerSettings.Android.keystorePass;
        string aliasName = PlayerSettings.Android.keyaliasName;
        string aliasPass = PlayerSettings.Android.keyaliasPass;
        VREmulator emulator = GetEmulator();
        int buildType;
        if (emulator != null)
        {
            buildType = (int)emulator.BuildType;
        }
        else
        {
            buildType = -1;
        }
        bool exportProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
        bool appBundle = EditorUserBuildSettings.buildAppBundle;
        AndroidBuildSystem buildSystem = EditorUserBuildSettings.androidBuildSystem;

        // Android didn't have its own scripting backend entry before? Then remove the one setting it adds, afterwards
        SerializedObject projectSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        bool hadBackendEntry = FindAndroidBackendEntry(projectSettings) >= 0;

        try
        {
            // Always a plain APK, whatever per-user export options are ticked
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            // 64-bit (IL2CPP + ARM64) runs on every Quest; the project's default Mono build is 32-bit only
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, monoBuild ? ScriptingImplementation.Mono2x : ScriptingImplementation.IL2CPP);
            if (monoBuild)
            {
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;
            }
            else
            {
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            }
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel28;
            PlayerSettings.SetVirtualRealitySDKs(BuildTargetGroup.Android, new[] { "Oculus" });

            PlayerSettings.Android.useCustomKeystore = true;
            // Forward slashes matter: 2019.1 stores a backslashed path inside the project as
            // "{inproject}: C:\...", which the Android build then can't find
            PlayerSettings.Android.keystoreName = Path.GetFullPath(KeystorePath).Replace('\\', '/');
            PlayerSettings.Android.keystorePass = password;
            PlayerSettings.Android.keyaliasName = KeyAlias;
            PlayerSettings.Android.keyaliasPass = password;

            // Quest controls (Liminal's OVR device); it should already be set, but make sure
            if (emulator != null)
            {
                SetBuildType(emulator, (int)ESDKType.OVR);
            }

            Directory.CreateDirectory(OutputFolder);
            string apk = $"{OutputFolder}/CatCafe-{name}-{DateTime.Now:yyyyMMdd-HHmm}.apk";

            building = true;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = apk,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Quest Build] Failed: {report.summary.result} ({report.summary.totalErrors} errors). See the Console.");
                return null;
            }

            Debug.Log($"[Quest Build] Done: {Path.GetFullPath(apk)} ({report.summary.totalSize / (1024f * 1024f):F0} MB, {report.summary.totalTime:mm\\:ss}).");
            return apk;
        }
        finally
        {
            building = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = exportProject;
            EditorUserBuildSettings.buildAppBundle = appBundle;
            EditorUserBuildSettings.androidBuildSystem = buildSystem;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, backend);
            if (!hadBackendEntry)
            {
                projectSettings.Update();
                int entry = FindAndroidBackendEntry(projectSettings);
                if (entry >= 0)
                {
                    projectSettings.FindProperty("scriptingBackend").DeleteArrayElementAtIndex(entry);
                    projectSettings.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            PlayerSettings.Android.targetArchitectures = architectures;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, graphics);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, defaultGraphics);
            PlayerSettings.Android.targetSdkVersion = targetSdk;
            PlayerSettings.SetVirtualRealitySDKs(BuildTargetGroup.Android, vrSdks);
            PlayerSettings.Android.useCustomKeystore = customKeystore;
            PlayerSettings.Android.keystoreName = keystoreName;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = aliasName;
            PlayerSettings.Android.keyaliasPass = aliasPass;
            if (emulator != null && buildType >= 0)
            {
                SetBuildType(emulator, buildType);
            }
            AssetDatabase.SaveAssets();
        }
    }

    // Unity 2019.1 must use its own bundled JDK, Android SDK and NDK. On this machine the shared
    // preferences point at Unity 6's tools, which 2019.1 can't build with.
    static bool CheckAndroidTools(bool interactive)
    {
        // Unity 2019.1 treats a setting that was never saved as "use the bundled tools", so default to true here too
        bool embedded = EditorPrefs.GetBool("SdkUseEmbedded", true) && EditorPrefs.GetBool("NdkUseEmbedded", true) && EditorPrefs.GetBool("JdkUseEmbedded", true);
        if (embedded)
        {
            return true;
        }

        const string message = "Unity 2019.1 needs to use its own bundled Android tools (JDK, Android SDK and NDK). " +
                               "Turn on 'Installed with Unity' for all three in Edit > Preferences > External Tools.";
        if (interactive && EditorUtility.DisplayDialog("Build for Quest", message + "\n\nTurn them on now?", "Turn on", "Cancel"))
        {
            EditorPrefs.SetBool("SdkUseEmbedded", true);
            EditorPrefs.SetBool("NdkUseEmbedded", true);
            EditorPrefs.SetBool("JdkUseEmbedded", true);
            return true;
        }

        Debug.LogError("[Quest Build] " + message);
        return false;
    }

    // A dedicated signing key for playtest builds, kept outside git (Builds/ is ignored).
    // Made with Unity 2019.1's own keytool so its older JDK can read it.
    static bool EnsureKeystore(out string password)
    {
        password = null;
        Directory.CreateDirectory(OutputFolder);

        if (File.Exists(KeystorePath) && File.Exists(KeystorePasswordPath))
        {
            password = File.ReadAllText(KeystorePasswordPath).Trim();
            return true;
        }
        if (File.Exists(KeystorePath))
        {
            Debug.LogError($"[Quest Build] {Path.GetFullPath(KeystorePath)} exists but its password file {Path.GetFullPath(KeystorePasswordPath)} is missing. " +
                           "Restore the .password file, or delete the keystore so a new key is made (testers would then need to uninstall the old build first).");
            return false;
        }

        string keytool = Path.Combine(EditorApplication.applicationContentsPath,
            "PlaybackEngines/AndroidPlayer/Tools/OpenJDK/Windows/bin/keytool.exe");
        if (!File.Exists(keytool))
        {
            Debug.LogError($"[Quest Build] Couldn't find Unity's keytool at {keytool}. Is Android Build Support installed for 2019.1?");
            return false;
        }

        password = NewPassword();
        Process process = Process.Start(new ProcessStartInfo
        {
            FileName = keytool,
            Arguments = $"-genkeypair -storetype JKS -keystore \"{Path.GetFullPath(KeystorePath)}\" -alias {KeyAlias} " +
                        $"-keyalg RSA -keysize 2048 -validity 10000 -storepass {password} -keypass {password} " +
                        "-dname \"CN=Catastrophe Cafe Playtest, O=Group 1 Capstone\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        // keytool reports most problems on standard output
        string output = process.StandardOutput.ReadToEnd();
        string errors = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 || !File.Exists(KeystorePath))
        {
            Debug.LogError("[Quest Build] Couldn't create the playtest keystore: " + output + errors);
            return false;
        }

        File.WriteAllText(KeystorePasswordPath, password);
        Debug.Log($"[Quest Build] Created a playtest signing key at {Path.GetFullPath(KeystorePath)}. Keep it (and its password file) to update installs later.");
        return true;
    }

    static int FindAndroidBackendEntry(SerializedObject projectSettings)
    {
        SerializedProperty map = projectSettings.FindProperty("scriptingBackend");
        if (map == null)
        {
            return -1;
        }
        for (int i = 0; i < map.arraySize; i++)
        {
            SerializedProperty first = map.GetArrayElementAtIndex(i).FindPropertyRelative("first");
            if (first != null && first.stringValue == "Android")
            {
                return i;
            }
        }
        return -1;
    }

    static string NewPassword()
    {
        const string letters = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        byte[] bytes = new byte[24];
        using (RandomNumberGenerator random = RandomNumberGenerator.Create())
        {
            random.GetBytes(bytes);
        }
        char[] chars = new char[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
        {
            chars[i] = letters[bytes[i] % letters.Length];
        }
        return new string(chars);
    }

    static VREmulator GetEmulator()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/[ExperienceApp].prefab");
        if (prefab != null)
        {
            return prefab.GetComponentInChildren<VREmulator>(true);
        }
        return null;
    }

    static void SetBuildType(VREmulator emulator, int type)
    {
        SerializedObject serialized = new SerializedObject(emulator);
        serialized.FindProperty("BuildType").intValue = type;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(emulator);
    }
}

// Marks the app as a VR app in its Android manifest, so Quest launches it in the headset rather than
// as a flat window. Only applies to builds made by QuestBuild above.
class QuestManifestTweak : IPostGenerateGradleAndroidProject
{
    public int callbackOrder
    {
        get
        {
            return 10;
        }
    }   // after the Oculus integration's own manifest changes

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        if (!QuestBuild.building)
        {
            return;
        }

        string manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        string manifest = File.ReadAllText(manifestPath);
        const string vrCategory = "<category android:name=\"com.oculus.intent.category.VR\" />";
        if (manifest.Contains("com.oculus.intent.category.VR"))
        {
            return;
        }

        // Add it right after the main activity's <action ... MAIN ... /> tag (whatever its spacing)
        int main = manifest.IndexOf("android.intent.action.MAIN", StringComparison.Ordinal);
        int tagEnd;
        if (main >= 0)
        {
            tagEnd = manifest.IndexOf("/>", main, StringComparison.Ordinal);
        }
        else
        {
            tagEnd = -1;
        }
        if (tagEnd < 0)
        {
            Debug.LogWarning("[Quest Build] Couldn't find the main activity in the Android manifest; the VR category wasn't added.");
            return;
        }

        manifest = manifest.Insert(tagEnd + 2, "\n        " + vrCategory);
        File.WriteAllText(manifestPath, manifest);
        Debug.Log("[Quest Build] Added the Oculus VR category to the Android manifest.");
    }
}
