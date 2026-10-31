#if UNITY_STANDALONE || UNITY_EDITOR
using Liminal.SDK.VR;
using UnityEngine;
using UnityEngine.XR;

// PC builds and the editor: any PC headset works. Unity starts SteamVR if it's there (Index, Vive, WMR, or a Rift/Quest
// through SteamVR), otherwise the Oculus runtime (a Rift S or Quest Link without SteamVR). Liminal's device is normally
// fixed in advance (VREmulator's Build Type, or its Editor Type in the editor), so here it's set to match whichever
// runtime actually started, just before the experience creates it (Liminal reads it in ExperienceApp.Start, after
// every Awake). In the editor, the mouse emulator is left alone (that's chosen on purpose, with no headset).
public static class PcHeadsetPicker
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Pick()
    {
        string runtime = XRSettings.loadedDeviceName;
#if UNITY_EDITOR
        if (runtime != "Oculus" && runtime != "OpenVR") return;   // no headset running: as set
#endif
        ESDKType type;
        if (runtime == "Oculus")
        {
            type = ESDKType.OVR;
        }
        else
        {
            type = ESDKType.OpenVR;
        }
        // SteamVR's input (its actions) only starts once something of SteamVR's appears; in a build Liminal sets up its
        // SteamVR controllers before that happens, finds no actions and stops (the headset just shows grey). So it's
        // started here, first.
        if (type == ESDKType.OpenVR && runtime == "OpenVR" && !Valve.VR.SteamVR_Input.initialized)
        {
            try { Valve.VR.SteamVR.Initialize(); }
            catch (System.Exception e) { Debug.LogException(e); }
            Debug.Log($"[PC headset] SteamVR input started: {Valve.VR.SteamVR_Input.initialized}");
        }
        foreach (VREmulator emulator in Object.FindObjectsOfType<VREmulator>())
        {
#if UNITY_EDITOR
            if (emulator.EditorType == ESDKType.Emulator) continue;   // the mouse, chosen on purpose (anything else: the headset that's running)
            emulator.EditorType = type;
#else
            emulator.BuildType = type;
#endif
        }
        Debug.Log($"[PC headset] XR runtime \"{runtime}\" ({XRDevice.model}): using Liminal's {type} device.");
    }
}
#endif
