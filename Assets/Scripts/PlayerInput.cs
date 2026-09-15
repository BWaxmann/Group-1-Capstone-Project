using System.Collections;
using TMPro;
using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    [SerializeField]
    float billboardUpdateFrequency = 0.2f;

    [SerializeField]
    Transform handRight;

    [SerializeField]
    Transform handLeft;

    [Header("Treat Launcher")]

    [SerializeField] 
    GameObject launcherRight;

    [SerializeField]
    GameObject launcherLeft;

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

    [SerializeField]
    [Tooltip("Press Q to activate.")]
    [Min(0)]
    float debugInfiniteAmmoDuration = 200f;

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

    bool launchersActive;
    GrabbableLaunchers lastPerceivedLaunchers;

    int _currentAmmo;
    bool hasSpawnedFreeAmmo;
    bool dontConsumeAmmo;
    float infiniteAmmoTimer;

    public int currentAmmo => _currentAmmo;
    public Vector3 pickupMagnet => _pickupMagnet.position;

    void Start()
    {
        handLeft.gameObject.SetActive(true);
        handRight.gameObject.SetActive(true);
        launcherLeft.gameObject.SetActive(false);
        launcherRight.gameObject.SetActive(false);

        _currentAmmo = initialAmmoCount;
        UpdateAmmoDisplay();

        StartCoroutine(UpdateBillboardsRoutine());
    }

    void Update()
    {
        if (dontConsumeAmmo)
        {
            infiniteAmmoTimer -= Time.deltaTime;
            if (infiniteAmmoTimer <= 0)
            {
                infiniteAmmoTimer = 0;
                dontConsumeAmmo = false;
                UpdateAmmoDisplay();
            }
        }

        if (!GameManager.instance.enablePlayerInput) return;

#if UNITY_EDITOR
        if (launchersActive)
        {
            if (Input.GetMouseButtonDown(0))
            {
                LaunchTreat(true);
            }
            if (Input.GetKeyDown(KeyCode.Q))
            {
                MakeAmmoInfinite(debugInfiniteAmmoDuration);
            }
        }
        else
        {
            if (Physics.Raycast(handRight.position, handRight.forward, out RaycastHit hit, 20))
            {
                if (TestForLaunchers(hit.transform))
                {
                    if (Input.GetMouseButton(0))
                    {
                        GetLaunchers();
                    }
                    else
                    {
                        lastPerceivedLaunchers.Highlight(true);
                    }
                    return;
                }
            }

            if (lastPerceivedLaunchers != null)
            {
                lastPerceivedLaunchers.Highlight(false);
                lastPerceivedLaunchers = null;
            }
        }
#else
        if (launchersActive)
        {
            if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
            {
                LaunchTreat(true);
            }
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger))
            {
                LaunchTreat(false);
            }
        }
        else
        {
            bool launchersFound = false;
            RaycastHit hit;
            if (Physics.Raycast(handLeft.position, handLeft.forward, out hit, 20))
            {
                if (TestForLaunchers(hit.transform))
                {
                    launchersFound = true;
                    if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger)
                        || OVRInput.GetDown(OVRInput.Button.Three))
                    {
                        GetLaunchers();
                        return;
                    }
                    else
                    {
                        lastPerceivedLaunchers.Highlight(true);
                    }
                }
            }
            if (Physics.Raycast(handRight.position, handRight.forward, out hit, 20))
            {
                if (TestForLaunchers(hit.transform))
                {
                    launchersFound = true;
                    if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger)
                        || OVRInput.GetDown(OVRInput.Button.One))
                    {
                        GetLaunchers();
                        return;
                    }
                    else
                    {
                        lastPerceivedLaunchers.Highlight(true);
                    }
                }
            }

            if (!launchersFound && lastPerceivedLaunchers != null)
            {
                lastPerceivedLaunchers.Highlight(false);
                lastPerceivedLaunchers = null;
            }
        }
#endif
    }

    bool TestForLaunchers(Transform other)
    {
        GrabbableLaunchers launchers = other.GetComponent<GrabbableLaunchers>();
        if (launchers != null)
        {
            lastPerceivedLaunchers = launchers;
            return true;
        }
        else
        {
            return false;
        }
    }

    void GetLaunchers()
    {
        launchersActive = true;

        lastPerceivedLaunchers.gameObject.SetActive(false);
        lastPerceivedLaunchers = null;
        handLeft.gameObject.SetActive(false);
        handRight.gameObject.SetActive(false);
        launcherLeft.gameObject.SetActive(true);
        launcherRight.gameObject.SetActive(true);

        GameManager.instance.GrabbedLaunchers();
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
        if (!dontConsumeAmmo)
        {
            UpdateAmmoDisplay();
        }

        if (_currentAmmo >= freeAmmoThreashold)
        {
            hasSpawnedFreeAmmo = false;
        }
    }

    void LaunchTreat(bool rightHand)
    {
        if (_currentAmmo > 0 || dontConsumeAmmo)
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
            if (billboardUpdateFrequency > Time.deltaTime)
            {
                yield return billboardUpdateFrequency;
            }
            else
            {
                yield return null;
            }
        }
    }

    public void MakeAmmoInfinite(float duration)
    {
        dontConsumeAmmo = true;
        if (ammoText != null) ammoText.text = infiniteAmmoText;
        infiniteAmmoTimer += duration;
    }
}
