using System.Collections;
using Liminal.SDK.VR;
using UnityEngine;
using Valve.VR;

// Controller vibration that works on both Quest (OVR) and Valve Index (OpenVR).
// Call it from anywhere, e.g. Haptics.Pulse(true, Haptics.Strength.Light).
// Does nothing in the editor's mouse emulator.
public class Haptics : MonoBehaviour
{
    public enum Strength
    {
        Light,
        Medium,
        Strong,
        Huge
    }

    static Haptics instance;

    SteamVR_Action_Vibration steamVibration;
    Coroutine stopRight;
    Coroutine stopLeft;

    public static void Pulse(bool rightHand, Strength strength)
    {
        GetSettings(strength, out float amplitude, out float duration);
        Pulse(rightHand, amplitude, duration);
    }

    public static void PulseBoth(Strength strength)
    {
        GetSettings(strength, out float amplitude, out float duration);
        Pulse(true, amplitude, duration);
        Pulse(false, amplitude, duration);
    }

    public static void Pulse(bool rightHand, float amplitude, float duration)
    {
        IVRDevice device = VRDevice.Device;
        if (device == null || amplitude <= 0 || duration <= 0)
        {
            return;
        }

        if (instance == null)
        {
            instance = new GameObject("Haptics").AddComponent<Haptics>();
        }

        if (device.Name == "OpenVR")
        {
            instance.PulseSteamVR(rightHand, amplitude, duration);
        }
        else if (device.Name == "GearVR") // Liminal's name for the Oculus/Quest device
        {
            instance.PulseOculus(rightHand, amplitude, duration);
        }
    }

    static void GetSettings(Strength strength, out float amplitude, out float duration)
    {
        switch (strength)
        {
            case Strength.Light:
                amplitude = 0.35f;
                duration = 0.05f;
                break;
            case Strength.Medium:
                amplitude = 0.7f;
                duration = 0.12f;
                break;
            case Strength.Strong:
                amplitude = 1f;
                duration = 0.25f;
                break;
            default:
                amplitude = 1f;
                duration = 0.6f;
                break;
        }
    }

    void PulseSteamVR(bool rightHand, float amplitude, float duration)
    {
        if (steamVibration == null)
        {
            steamVibration = SteamVR_Input.GetAction<SteamVR_Action_Vibration>("default", "Haptic");
            if (steamVibration == null)
            {
                return;
            }
        }

        SteamVR_Input_Sources source;
        if (rightHand)
        {
            source = SteamVR_Input_Sources.RightHand;
        }
        else
        {
            source = SteamVR_Input_Sources.LeftHand;
        }
        steamVibration.Execute(0, duration, 160f, amplitude, source);
    }

    void PulseOculus(bool rightHand, float amplitude, float duration)
    {
        OVRInput.Controller controller;
        if (rightHand)
        {
            controller = OVRInput.Controller.RTouch;
        }
        else
        {
            controller = OVRInput.Controller.LTouch;
        }
        OVRInput.SetControllerVibration(1f, amplitude, controller);

        // Quest keeps vibrating until told to stop, so stop it after the duration
        if (rightHand)
        {
            if (stopRight != null)
            {
                StopCoroutine(stopRight);
            }
            stopRight = StartCoroutine(StopOculusAfterRoutine(controller, duration));
        }
        else
        {
            if (stopLeft != null)
            {
                StopCoroutine(stopLeft);
            }
            stopLeft = StartCoroutine(StopOculusAfterRoutine(controller, duration));
        }
    }

    IEnumerator StopOculusAfterRoutine(OVRInput.Controller controller, float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        OVRInput.SetControllerVibration(0, 0, controller);
    }
}
