using Liminal.SDK.VR;
using Liminal.SDK.VR.Input;
using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] 
    ObjectSpawner treatsSpawner;

    [SerializeField] 
    Transform primaryHandTreatPosition;

    [SerializeField] 
    Transform secondaryHandTreatPosition;

    [SerializeField] 
    GameObject primaryHandThrowPreview;

    [SerializeField] 
    GameObject secondaryHandThrowPreview;

    [SerializeField] 
    [Min(0)] 
    float throwForce = 5f;

    bool primaryHandInBox;
    bool secondaryHandInBox;
    Treat currentPrimaryHandTreat;
    Treat currentSecondaryHandTreat;

    public bool treatInPrimaryHand => currentPrimaryHandTreat != null;
    public bool treatInSecondaryHand => currentSecondaryHandTreat != null;

    void Start()
    {
        primaryHandThrowPreview.SetActive(false);
        secondaryHandThrowPreview.SetActive(false);
    }

    void Update()
    {
        if (!GameManager.instance.enablePlayerInput) return;

        var device = VRDevice.Device.PrimaryInputDevice;

        if (device == null)
        {
            Debug.LogError("Primary input device could not be found");
        }
        else
        {
            if (device.GetButtonDown(VRButton.One))
            {
                PickupTreat(true);
            }
            else if (device.GetButtonUp(VRButton.One))
            {
                ThrowTreat(true);
            }
        }

        device = VRDevice.Device.SecondaryInputDevice;

        if (device == null)
        {
            Debug.LogError("Secondary input device could not be found");
        }
        else
        {
            if (device.GetButtonDown(VRButton.One))
            {
                PickupTreat(false);
            }
            else if (device.GetButtonUp(VRButton.One))
            {
                ThrowTreat(false);
            }
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
                currentPrimaryHandTreat = treatsSpawner.GetObjectFromPool<Treat>();
                currentPrimaryHandTreat.gameObject.SetActive(true);
                currentPrimaryHandTreat.AttachTreatToHand(primaryHandTreatPosition);
                primaryHandThrowPreview.SetActive(true);
            }
        }
        else
        {
            if (secondaryHandInBox && !treatInSecondaryHand)
            {
                currentSecondaryHandTreat = treatsSpawner.GetObjectFromPool<Treat>();
                currentSecondaryHandTreat.gameObject.SetActive(true);
                currentSecondaryHandTreat.AttachTreatToHand(secondaryHandTreatPosition);
                secondaryHandThrowPreview.SetActive(true);
            }
        }
    }

    void ThrowTreat(bool primaryHand)
    {
        if (primaryHand)
        {
            if (treatInPrimaryHand)
            {
                currentPrimaryHandTreat.LaunchTreatFromHand(primaryHandTreatPosition.forward, throwForce);
                currentPrimaryHandTreat = null;
                primaryHandThrowPreview.SetActive(false);
            }
        }
        else
        {
            if (treatInSecondaryHand)
            {
                currentSecondaryHandTreat.LaunchTreatFromHand(secondaryHandTreatPosition.forward, throwForce);
                currentSecondaryHandTreat = null;
                secondaryHandThrowPreview.SetActive(false);
            }
        }
    }
}
