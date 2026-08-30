using System.Collections;
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
    SoundData launcherFireSound = new SoundData(1f, 1f);

    [SerializeField]
    [Tooltip("Sound to play when attempting to fire a treat with no ammo.")]
    SoundData launcherEmptySound = new SoundData(1f, 1f);

    [SerializeField]
    [Tooltip("Sound to play when reloading the treat launcher.")]
    SoundData launcherReloadSound = new SoundData(1f, 1f);

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
    }

    void Reload()
    {
        if (currentAmmo <= 0)
        {
            treatLaunchPreview.SetActive(true);
        }
        currentAmmo = maximumAmmo;
        PlaySound(launcherReloadSound);
        UpdateAmmoDisplay();
    }

    void LaunchTreat()
    {
        if (currentAmmo > 0)
        {
            Treat treat = treatsSpawner.GetRandomObjectFromPool<Treat>();
            if (treat == null)
            {
                Debug.LogError("Failed to spawn treat to launch.", this);
                return;
            }
            treat.gameObject.SetActive(true);
            treat.LaunchTreat(treatLaunchPosition, launchForce);
            PlaySound(launcherFireSound);
            currentAmmo--;
            UpdateAmmoDisplay();
            if (currentAmmo <= 0)
            {
                treatLaunchPreview.SetActive(false);
            }
        }
        else
        {
            PlaySound(launcherEmptySound);
        }
    }

    void UpdateAmmoDisplay()
    {
        if (ammoText != null) ammoText.text = currentAmmo.ToString();
    }

    void PlaySound(SoundData sound)
    {
        if (launcherAudioSource != null && sound.soundClip != null)
        {
            sound.PlayOneShot(launcherAudioSource);
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
