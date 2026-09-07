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

    int _currentAmmo;
    bool hasSpawnedFreeAmmo;
    bool dontConsumeAmmo;

    public int currentAmmo => _currentAmmo;
    public Vector3 pickupMagnet => _pickupMagnet.position;

    void Start()
    {
        _currentAmmo = initialAmmoCount;
        UpdateAmmoDisplay();

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
        if (Input.GetKeyDown(KeyCode.Q))
        {
            MakeAmmoInfinite(10f);
        }
#else
        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            LaunchTreat();
        }
#endif
    }

    public void GetAmmo(int amount)
    {
        if (_currentAmmo <= 0)
        {
            treatLaunchPreview.SetActive(true);
        }
        _currentAmmo += amount;
        PlaySound(launcherReloadSound);
        UpdateAmmoDisplay();

        if (_currentAmmo >= freeAmmoThreashold)
        {
            hasSpawnedFreeAmmo = false;
        }
    }

    void LaunchTreat()
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
            treat.LaunchTreat(treatLaunchPosition, launchForce);
            PlaySound(launcherFireSound);
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
                    treatLaunchPreview.SetActive(false);
                }
            }
        }
        else
        {
            PlaySound(launcherEmptySound);
        }
    }

    void UpdateAmmoDisplay()
    {
        if (ammoText != null) ammoText.text = _currentAmmo.ToString();
    }

    void PlaySound(SoundData sound)
    {
        if (launcherAudioSource != null && launcherAudioSource.gameObject.activeInHierarchy)
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
