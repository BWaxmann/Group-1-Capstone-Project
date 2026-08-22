using System.Collections;
using Liminal.SDK.VR;
using Liminal.SDK.VR.Input;
using TMPro;
using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    [SerializeField]
    float billboardUpdateFrequency = 0.2f;

    [Header("Treat Launcher")]

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

    [SerializeField]
    TMP_Text ammoText;

    [SerializeField]
    Transform eye;

    [Header("Sound Effects")]

    [SerializeField]
    AudioSource launcherAudioSource;

    [SerializeField]
    [Tooltip("Sound to play when firing a treat.")]
    AudioClip launcherFireSound;

    [SerializeField]
    [Range(0f, 1f)]
    float launcherFireVolume = 1f;

    [SerializeField]
    [Tooltip("Sound to play when attempting to fire a treat with no ammo.")]
    AudioClip launcherEmptySound;

    [SerializeField]
    [Range(0f, 1f)]
    float launcherEmptyVolume = 0.5f;

    [SerializeField]
    [Tooltip("Sound to play when reloading the treat launcher.")]
    AudioClip launcherReloadSound;

    [SerializeField]
    [Range(0f, 1f)]
    float launcherReloadVolume = 0.8f;

    public delegate void FaceCamera(Quaternion rotation);
    public static event FaceCamera TurnToFaceCamera;

    bool handInBox;
    int currentAmmo;

    void Start()
    {
        currentAmmo = maximumAmmo;

        if (launcherAudioSource == null)
        {
            launcherAudioSource = GetComponentInChildren<AudioSource>();
        }

        StartCoroutine(UpdateBillboardsRoutine());
    }

    void Update()
    {
        if (!GameManager.instance.enablePlayerInput) return;

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
        {
            LaunchTreat();
        }

        if (handInBox && Input.GetMouseButtonDown(1))
        {
            Reload();
        }
#else
        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            LaunchTreat();
        }

        if (handInBox && OVRInput.GetDown(OVRInput.Button.One))
        {
            Reload();
        }
#endif
    }

    public void HandInBox(bool inBox)
    {
        handInBox = inBox;
        if (currentAmmo > 0)
        {
            treatLaunchPreview.SetActive(!inBox);
        }
    }

    void Reload()
    {
        currentAmmo = maximumAmmo;
        PlaySound(launcherReloadSound, launcherReloadVolume);
        UpdateAmmoDisplay();
    }

    void LaunchTreat()
    {
        if (currentAmmo > 0)
        {
            Treat treat = treatsSpawner.GetObjectFromPool<Treat>();
            treat.gameObject.SetActive(true);
            treat.LaunchTreat(treatLaunchPosition, launchForce);
            PlaySound(launcherFireSound, launcherFireVolume);
            currentAmmo--;
            UpdateAmmoDisplay();
            if (currentAmmo <= 0)
            {
                treatLaunchPreview.SetActive(false);
            }
        }
        else
        {
            PlaySound(launcherEmptySound, launcherEmptyVolume);
        }
    }

    void UpdateAmmoDisplay()
    {
        if (ammoText != null) ammoText.text = currentAmmo.ToString();
    }

    void PlaySound(AudioClip sound, float volume)
    {
        if (launcherAudioSource != null && sound != null)
        {
            launcherAudioSource.PlayOneShot(sound, volume);
        }
    }

    IEnumerator UpdateBillboardsRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(billboardUpdateFrequency);
        while (true)
        {
            TurnToFaceCamera?.Invoke(eye.rotation);
            yield return billboardUpdateFrequency;
        }
    }
}
