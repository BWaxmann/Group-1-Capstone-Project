using Liminal.SDK.VR;
using Liminal.SDK.VR.Input;
using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    bool primaryHandInBox;
    bool secondaryHandInBox;
    bool treatInPrimaryHand;
    bool treatInSecondaryHand;

    void Update()
    {
        var device = VRDevice.Device.PrimaryInputDevice;

        if (device == null)
        {
            Debug.LogError("Input device could not be found");
            return;
        }

        if (device.GetButtonDown(VRButton.One))
        {
            PickupTreat(true);
        }
        else if (device.GetButtonUp(VRButton.One))
        {
            ThrowTreat(true);
        }
    }

    public void HandInBox(bool inBox, bool primaryHand)
    {
        if (primaryHand)
        {
            primaryHandInBox = inBox;
            Debug.Log("Primary hand in box: " + inBox);
        }
        else
        {
            secondaryHandInBox = inBox;
            Debug.Log("Secondary hand in box: " + inBox);
        }
    }

    void PickupTreat(bool primaryHand)
    {
        if (primaryHand)
        {
            if (primaryHandInBox && !treatInPrimaryHand)
            {
                treatInPrimaryHand = true;
                Debug.Log("Picked up a treat in primary hand");
            }
        }
        else
        {
            if (secondaryHandInBox && !treatInSecondaryHand)
            {
                treatInSecondaryHand = true;
                Debug.Log("Picked up a treat in secondary hand");
            }
        }
    }

    void ThrowTreat(bool primaryHand)
    {
        if (primaryHand)
        {
            if (treatInPrimaryHand)
            {
                treatInPrimaryHand = false;
                Debug.Log("Treat in primary hand was thrown");
            }
        }
        else
        {
            if (treatInSecondaryHand)
            {
                treatInSecondaryHand = false;
                Debug.Log("Treat in secondary hand was thrown");
            }
        }
    }
}
