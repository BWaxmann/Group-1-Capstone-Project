using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

// Cat Cafe Waves: the SDK's Platform Viewer (the app that plays a .limapp the way Liminal's app does) needs a VR
// pointer to press its Play Limapp button, and with our setup it shows none, in the editor or on the Quest. In the
// editor the menu Catastrophe Cafe > Play The Limapp In The Viewer presses it; in a viewer APK this does, a few
// seconds after the viewer scene is up, and the button itself is switched off: a click on it as well as this press
// loads the limapp twice, and the loader breaks on the second (scene to unload is invalid, then a black screen).
// The viewer is found by its type name at runtime, so nothing here refers to the SDK's viewer assembly (which
// Liminal's own compile of our scripts may not have). In the game APK, and inside Liminal's app, there is no viewer,
// so nothing happens.
public class ViewerAutoPlay : MonoBehaviour
{
    const string ViewerTypeName = "Liminal.Platform.Experimental.App.PlatformAppViewer";
    const float Delay = 5f;

    UnityEngine.Object viewer;
    MethodInfo play;
    int buttonsOff;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void LookForViewer()
    {
        Type viewerType = null;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            viewerType = assembly.GetType(ViewerTypeName);
            if (viewerType != null)
            {
                break;
            }
        }
        if (viewerType == null)
        {
            return;
        }
        UnityEngine.Object found = FindObjectOfType(viewerType);
        MethodInfo playMethod = viewerType.GetMethod("Play", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
        if (found == null || playMethod == null)
        {
            return;
        }
        // The viewer's own Play Limapp button(s): off, so this is the only press
        int buttons = 0;
        foreach (UnityEngine.UI.Button button in FindObjectsOfType<UnityEngine.UI.Button>())
        {
            int calls = button.onClick.GetPersistentEventCount();
            for (int i = 0; i < calls; i++)
            {
                if (button.onClick.GetPersistentTarget(i) == found && button.onClick.GetPersistentMethodName(i) == "Play")
                {
                    button.interactable = false;
                    buttons++;
                }
            }
        }
        ViewerAutoPlay presser = new GameObject("Viewer Auto Play").AddComponent<ViewerAutoPlay>();
        presser.viewer = found;
        presser.play = playMethod;
        presser.buttonsOff = buttons;
    }

    IEnumerator Start()
    {
        Debug.Log($"[Cat Cafe Waves] Viewer Auto Play: pressing Play Limapp in {Delay} seconds ({buttonsOff} Play button(s) switched off, so the limapp loads once).");
        yield return new WaitForSeconds(Delay);
        if (viewer == null)
        {
            Debug.LogWarning("[Cat Cafe Waves] Viewer Auto Play: the viewer is gone.");
            yield break;
        }
        try
        {
            play.Invoke(viewer, null);
            Debug.Log("[Cat Cafe Waves] Viewer Auto Play: Play Limapp pressed.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Cat Cafe Waves] Viewer Auto Play: Play Limapp failed: {e.Message}");
        }
    }
}
