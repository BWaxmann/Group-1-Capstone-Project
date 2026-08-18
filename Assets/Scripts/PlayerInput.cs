using Liminal.SDK.VR;
using Liminal.SDK.VR.Input;
using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] 
    ObjectSpawner treatsSpawner;

    [SerializeField] 
    Transform treatLaunchPosition;

    [SerializeField]
    GameObject treatLaunchPreview;

    [SerializeField] 
    [Min(0)] 
    float launchForce = 10f;

    [SerializeField]
    [Min(0)]
    int maximumAmmo = 20;

    bool handInBox;
    int currentAmmo;

    void Start()
    {
        currentAmmo = maximumAmmo;
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
                if (handInBox)
                {
                    Reload();
                }
                else
                {
                    LaunchTreat();
                }   
            }
        }
    }

    public void HandInBox(bool inBox)
    {
        handInBox = inBox;
        if (currentAmmo > 0)
        {
            treatLaunchPreview.SetActive(!inBox);
        }
        Debug.Log("Hand in box: " + inBox);
    }

    void Reload()
    {
        currentAmmo = maximumAmmo;
    }

    void LaunchTreat()
    {
        if (currentAmmo > 0)
        {
            Treat treat = treatsSpawner.GetObjectFromPool<Treat>();
            treat.gameObject.SetActive(true);
            treat.LaunchTreat(treatLaunchPosition, launchForce);
            currentAmmo--;
            if (currentAmmo <= 0)
            {
                treatLaunchPreview.SetActive(false);
            }
        }
    }
}
