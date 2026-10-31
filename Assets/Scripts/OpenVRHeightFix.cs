using System.Collections;
using Liminal.SDK.VR;
using Liminal.SDK.VR.Avatars;
using UnityEngine;
using UnityEngine.XR;

// Fixes the player floating near the roof on SteamVR headsets (e.g. Valve Index).
// Liminal's OpenVR avatar subtracts the player's real head height on its first frame,
// but SteamVR hasn't reported a head position yet at that point, so nothing is subtracted.
// This waits for real tracking data, then applies the same correction. Quest is unaffected.
public class OpenVRHeightFix : MonoBehaviour
{
    const float MinimumValidEyeHeight = 0.3f;
    const float TrackingTimeout = 5f;

    Vector3 defaultHeadPosition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AttachToAvatarHead()
    {
        VRAvatarHead head = FindObjectOfType<VRAvatarHead>();
        if (head == null)
        {
            return;
        }

        // Runs before any Start, so this is still the head position set up in the prefab
        OpenVRHeightFix fix = head.gameObject.AddComponent<OpenVRHeightFix>();
        fix.defaultHeadPosition = head.transform.localPosition;
    }

    IEnumerator Start()
    {
        float timer = 0;
        while (VRDevice.Device == null && timer < TrackingTimeout)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (VRDevice.Device == null || VRDevice.Device.Name != "OpenVR")
        {
            Destroy(this);
            yield break;
        }

        // Let Liminal's own adjustment run first, then wait for a real head position
        yield return null;
        timer = 0;
        Vector3 eyePosition = InputTracking.GetLocalPosition(XRNode.CenterEye);
        while (eyePosition.y < MinimumValidEyeHeight && timer < TrackingTimeout)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
            eyePosition = InputTracking.GetLocalPosition(XRNode.CenterEye);
        }

        if (eyePosition.y < MinimumValidEyeHeight)
        {
            Debug.LogWarning("[OpenVRHeightFix] No head tracking from SteamVR, so the player height wasn't adjusted.");
        }
        else
        {
            transform.localPosition = defaultHeadPosition - eyePosition;
        }

        Destroy(this);
    }
}
