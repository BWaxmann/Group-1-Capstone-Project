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
    Transform treatLaunchPositionRight;

    [SerializeField]
    Transform treatLaunchPositionLeft;

    [SerializeField]
    GameObject treatLaunchPreviewRight;

    [SerializeField]
    GameObject treatLaunchPreviewLeft;

    [SerializeField] 
    [Min(0)] 
    float launchForce = 10f;

    [SerializeField]
    [Min(0)]
    int initialAmmoCount = 20;

    [SerializeField]
    TMP_Text ammoText;

    [SerializeField]
    Transform eye;

    [SerializeField]
    Transform _pickupMagnet;

    [SerializeField]
    bool spawnFreeAmmoWhenOut = true;

    [SerializeField]
    [Min(0)]
    int freeAmmoThreashold = 5;

    [SerializeField]
    int ammoPickupIndex = 0;

    [SerializeField]
    Vector3 freeAmmoSpawnOffset = new Vector3(0f, 1f, -8f);

    [SerializeField]
    string infiniteAmmoText = "<color=#FFD700>∞</color>";

    [Header("Sound Effects")]

    [SerializeField]
    AudioSource launcherAudioSourceRight;

    [SerializeField]
    AudioSource launcherAudioSourceLeft;

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

    int _currentAmmo;
    bool hasSpawnedFreeAmmo;
    bool dontConsumeAmmo;

    public int currentAmmo => _currentAmmo;
    public Vector3 pickupMagnet => _pickupMagnet.position;

    void Start()
    {
        _currentAmmo = initialAmmoCount;
        UpdateAmmoDisplay();

        StartCoroutine(UpdateBillboardsRoutine());
    }

    void Update()
    {
        if (!GameManager.instance.enablePlayerInput) return;

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
        {
            LaunchTreat(true);
        }
        if (Input.GetKeyDown(KeyCode.Q))
        {
            MakeAmmoInfinite(10f);
        }
#else
        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            LaunchTreat(true);
        }
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger))
        {
            LaunchTreat(false);
        }
#endif
    }

    public void GetAmmo(int amount)
    {
        if (_currentAmmo <= 0)
        {
            treatLaunchPreviewRight.SetActive(true);
            treatLaunchPreviewLeft.SetActive(true);
        }
        _currentAmmo += amount;
        PlaySound(launcherReloadSound, true);
        UpdateAmmoDisplay();

        if (_currentAmmo >= freeAmmoThreashold)
        {
            hasSpawnedFreeAmmo = false;
        }
    }

    void LaunchTreat(bool rightHand)
    {
        if (_currentAmmo > 0)
        {
            Treat treat = treatsSpawner.GetRandomObjectFromPool<Treat>();
            if (treat == null)
            {
                Debug.LogError("Failed to spawn treat to launch.", this);
                return;
            }
            treat.gameObject.SetActive(true);
            treat.LaunchTreat(rightHand ? treatLaunchPositionRight : treatLaunchPositionLeft, launchForce);
            PlaySound(launcherFireSound, rightHand);
            if (!dontConsumeAmmo)
            {
                _currentAmmo--;
                UpdateAmmoDisplay();
                if (spawnFreeAmmoWhenOut && _currentAmmo < freeAmmoThreashold && !hasSpawnedFreeAmmo)
                {
                    Debug.Log("Spawning free ammo.");
                    hasSpawnedFreeAmmo = true;
                    PickupManager.instance.SpawnPickup(ammoPickupIndex, transform.TransformVector(freeAmmoSpawnOffset));
                }
                if (_currentAmmo <= 0)
                {
                    treatLaunchPreviewRight.SetActive(false);
                    treatLaunchPreviewRight.SetActive(true);
                }
            }
        }
        else
        {
            PlaySound(launcherEmptySound, rightHand);
        }
    }

    void UpdateAmmoDisplay()
    {
        if (ammoText != null) ammoText.text = _currentAmmo.ToString();
    }

    void PlaySound(SoundData sound, bool rightHand)
    {
        if (rightHand)
        {
            if (launcherAudioSourceRight != null && launcherAudioSourceRight.gameObject.activeInHierarchy)
            {
                sound.PlayOneShot(launcherAudioSourceRight);
            }
        }
        else
        {
            if (launcherAudioSourceLeft != null && launcherAudioSourceLeft.gameObject.activeInHierarchy)
            {
                sound.PlayOneShot(launcherAudioSourceLeft);
            }
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

    public void MakeAmmoInfinite(float duration)
    {
        dontConsumeAmmo = true;
        if (ammoText != null) ammoText.text = infiniteAmmoText;
        Invoke(nameof(EndInfiniteAmmo), duration);
    }

    void EndInfiniteAmmo()
    {
        dontConsumeAmmo = false;
        UpdateAmmoDisplay();
    }
}
