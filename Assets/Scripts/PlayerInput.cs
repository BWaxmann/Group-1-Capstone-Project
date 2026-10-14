using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInput : MonoBehaviour
{
    [SerializeField]
    float billboardUpdateFrequency = 0.2f;

    [SerializeField]
    Transform handRight;

    [SerializeField]
    Transform handLeft;

    [SerializeField]
    PickupManager pickupManagerPrefab;

    [Header("Treat Launcher")]

    [SerializeField] 
    GameObject launcherRight;

    [SerializeField]
    GameObject launcherLeft;

    [SerializeField] 
    ObjectSpawner treatsSpawnerPrefab;

    [SerializeField] 
    Transform treatLaunchPositionRight;

    [SerializeField]
    Transform treatLaunchPositionLeft;

    [SerializeField] 
    [Min(0)] 
    float launchForce = 6f;

    [SerializeField]
    bool enableAutofire = true;

    [SerializeField]
    [Min(0)]
    float autofireRate = 6f;

    [SerializeField]
    [Min(2)]
    int numberOfShotsInSpread = 3;

    [SerializeField]
    [Range(0f, 90f)]
    float maxSpreadAngle = 45f;

    [SerializeField]
    TMP_Text ammoText;

    [SerializeField]
    Transform eye;

    [SerializeField]
    Transform _pickupMagnet;

    [SerializeField]
    string defaultDisplayText = "^ <rotate=\"-90\">3</rotate> ^";

    [Header("Yarnball")]

    [SerializeField]
    [Min(0)]
    int yarnballTreatSpawnerIndex = 4;

    [SerializeField]
    [Min(0)]
    float yarnballRechargeTime = 15f;

    [SerializeField]
    Slider yarnballRechargeBar;

    [SerializeField]
    Image yarnballRechargeBarImage;

    [SerializeField]
    Color yarnballRechargingColour = new Color(0.5f, 0, 0);

    [SerializeField]
    Color yarnballReadyColour = Color.green;

    [SerializeField]
    bool yarnballReadyOnStart;

    [Header("Sound Effects")]

    [SerializeField]
    AudioSource launcherAudioSourceRight;

    [SerializeField]
    AudioSource launcherAudioSourceLeft;

    [SerializeField]
    [Tooltip("Sound to play when firing a treat.")]
    SoundData launcherFireSound = new SoundData(1f, 1f);

    [SerializeField]
    SoundData launcherYarnballSound = new SoundData(1f, 1f);

    [SerializeField]
    SoundData yarnballRechargedSound = new SoundData(1f, 1f);

    public delegate void FaceCamera(Vector3 position);
    public static event FaceCamera TurnToFaceCamera;

    bool launchersActive;
    GrabbableLaunchers lastPerceivedLaunchers;

    Coroutine rightHandAutofire;
    Coroutine leftHandAutofire;
    ObjectSpawner treatsSpawner;

    bool yarnballReady;
    float yarnballRechargeTimer;

    bool spreadActive;
    float spreadTimer;

    bool instaKillActive;
    float instaKillTimer;

    public Vector3 pickupMagnet => _pickupMagnet.position;

    void Start()
    {
        handLeft.gameObject.SetActive(true);
        handRight.gameObject.SetActive(true);
        launcherLeft.gameObject.SetActive(false);
        launcherRight.gameObject.SetActive(false);
        GameManager.instance.GameEnd += StopLaunchers;

        ammoText.text = defaultDisplayText;

        yarnballReady = yarnballReadyOnStart;
        yarnballRechargeTimer = yarnballReady ? yarnballRechargeTime : 0;
        SetYarnballBar();

        StartCoroutine(UpdateBillboardsRoutine());
    }

    void Update()
    {
        if (instaKillActive)
        {
            instaKillTimer -= Time.deltaTime;
            if (instaKillTimer <= 0)
            {
                instaKillTimer = 0;
                instaKillActive = false;
            }
        }

        if (spreadActive)
        {
            spreadTimer -= Time.deltaTime;
            if (spreadTimer <= 0)
            {
                spreadTimer = 0;
                spreadActive = false;
            }
        }

        if (!yarnballReady)
        {
            yarnballRechargeTimer += Time.deltaTime;
            if (yarnballRechargeTimer >= yarnballRechargeTime)
            {
                yarnballRechargeTimer = yarnballRechargeTime;
                yarnballReady = true;
                yarnballRechargedSound.PlayOneShot(launcherAudioSourceRight);
            }
            SetYarnballBar();
        }

        if (!GameManager.instance.enablePlayerInput) return;

#if UNITY_EDITOR
        if (launchersActive)
        {
            if (Input.GetMouseButtonDown(1) && yarnballReady)
            {
                LaunchYarnball(true);
            }

            if (enableAutofire)
            {
                if (Input.GetMouseButtonDown(0) && rightHandAutofire == null)
                {
                    rightHandAutofire = StartCoroutine(AutofireRoutine(true));
                }
                else if (Input.GetMouseButtonUp(0) && rightHandAutofire != null)
                {
                    StopCoroutine(rightHandAutofire);
                    rightHandAutofire = null;
                }
            }
            else
            {
                if (Input.GetMouseButtonDown(0))
                {
                    if (spreadActive)
                    {
                        for (int i = 0; i < numberOfShotsInSpread; i++)
                        {
                            LaunchTreat(true, (1 - 2 * i / (numberOfShotsInSpread - 1)) * maxSpreadAngle);
                        }
                    }
                    else
                    {
                        LaunchTreat(true);
                    }
                    PlaySound(launcherFireSound, true);
                }
            }
        }
        else
        {
            if (Physics.Raycast(handRight.position, handRight.forward, out RaycastHit hit, 20))
            {
                if (TestForLaunchers(hit.transform))
                {
                    if (Input.GetMouseButtonDown(0))
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
            if (OVRInput.GetDown(OVRInput.Button.SecondaryHandTrigger) && yarnballReady)
            {
                LaunchYarnball(true);
            }
            if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger) && yarnballReady)
            {
                LaunchYarnball(false);
            }

            if (enableAutofire)
            {
                if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger) && rightHandAutofire == null)
                {
                    rightHandAutofire = StartCoroutine(AutofireRoutine(true));
                }
                else if (OVRInput.GetUp(OVRInput.Button.SecondaryIndexTrigger) && rightHandAutofire != null)
                {
                    StopCoroutine(rightHandAutofire);
                    rightHandAutofire = null;
                }

                if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) && leftHandAutofire == null)
                {
                    leftHandAutofire = StartCoroutine(AutofireRoutine(false));
                }
                else if (OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger) && leftHandAutofire != null)
                {
                    StopCoroutine(leftHandAutofire);
                    leftHandAutofire = null;
                }
            }
            else
            {
                if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
                {
                    if (spreadActive)
                    {
                        for (int i = 0; i < numberOfShotsInSpread; i++)
                        {
                            LaunchTreat(true, (1 - 2 * i / (numberOfShotsInSpread - 1)) * maxSpreadAngle);
                        }
                    }
                    else
                    {
                        LaunchTreat(true);
                    }
                    PlaySound(launcherFireSound, true);
                }
                if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger))
                {
                    if (spreadActive)
                    {
                        for (int i = 0; i < numberOfShotsInSpread; i++)
                        {
                            LaunchTreat(false, (1 - 2 * i / (numberOfShotsInSpread - 1)) * maxSpreadAngle);
                        }
                    }
                    else
                    {
                        LaunchTreat(false);
                    }
                    PlaySound(launcherFireSound, false);
                }
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

        treatsSpawner = Instantiate(treatsSpawnerPrefab);
        PickupManager pm = Instantiate(pickupManagerPrefab);
        pm.SetPlayerVisionPoint(eye);
    }

    IEnumerator AutofireRoutine(bool rightHand)
    {
        WaitForSeconds wait = new WaitForSeconds(1f / autofireRate);
        while (true)
        {
            if (spreadActive)
            {
                for (int i = 0; i < numberOfShotsInSpread; i++)
                {
                    LaunchTreat(rightHand, (1 - 2 * i / (numberOfShotsInSpread - 1)) * maxSpreadAngle);
                }
            }
            else
            {
                LaunchTreat(rightHand);
            }
            PlaySound(launcherFireSound, rightHand);
            yield return wait;
        }
    }

    void LaunchTreat(bool rightHand, float angle = 0)
    {
        Treat treat = treatsSpawner.GetRandomObjectFromPool<Treat>();
        if (treat == null) return;
        treat.gameObject.SetActive(true);
        treat.Launch(rightHand ? treatLaunchPositionRight : treatLaunchPositionLeft, launchForce, angle, instaKillActive);
    }

    void LaunchYarnball(bool rightHand)
    {
        Yarnball yarnball = treatsSpawner.GetObjectFromPool<Yarnball>(yarnballTreatSpawnerIndex);
        if (yarnball == null) return;
        yarnball.gameObject.SetActive(true);
        yarnball.Launch(rightHand ? treatLaunchPositionRight : treatLaunchPositionLeft, launchForce, 0, false);
        PlaySound(launcherYarnballSound, rightHand);

        yarnballReady = false;
        yarnballRechargeTimer = 0;
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
            TurnToFaceCamera?.Invoke(eye.position);
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

    public void MakeTreatsInstaKill(float duration)
    {
        instaKillActive = true;
        instaKillTimer += duration;
    }

    public void ActivateSpread(float duration)
    {
        spreadActive = true;
        spreadTimer += duration;
    }

    public void StopLaunchers()
    {
        if (rightHandAutofire != null)
        {
            StopCoroutine(rightHandAutofire);
            rightHandAutofire = null;
        }

        if (leftHandAutofire != null)
        {
            StopCoroutine(leftHandAutofire);
            leftHandAutofire = null;
        }
    }

    void SetYarnballBar()
    {
        yarnballRechargeBar.value = Mathf.Clamp01(yarnballRechargeTimer / yarnballRechargeTime);
        yarnballRechargeBarImage.color = yarnballReady ? yarnballReadyColour : yarnballRechargingColour;
    }
}
