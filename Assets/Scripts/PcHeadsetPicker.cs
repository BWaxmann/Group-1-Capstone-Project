#if UNITY_STANDALONE && !UNITY_EDITOR
using Liminal.SDK.VR;
using UnityEngine;
using UnityEngine.XR;

// PC builds: one build for any PC headset. Unity starts SteamVR if it's there (Index, Vive, WMR, or a Rift/Quest
// through SteamVR), otherwise the Oculus runtime (a Rift S or Quest Link without SteamVR). Liminal's device is
// normally fixed at build time, so here it's set to match whichever runtime actually started, just before the
// experience creates it (Liminal reads VREmulator.BuildType in ExperienceApp.Start, after every Awake).
public static class PcHeadsetPicker
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Pick()
    {
        string runtime = XRSettings.loadedDeviceName;
        ESDKType type = runtime == "Oculus" ? ESDKType.OVR : ESDKType.OpenVR;
        foreach (VREmulator emulator in Object.FindObjectsOfType<VREmulator>()) emulator.BuildType = type;
        Debug.Log($"[PC headset] XR runtime \"{runtime}\" ({XRDevice.model}): using Liminal's {type} device.");
    }
}
#endif
