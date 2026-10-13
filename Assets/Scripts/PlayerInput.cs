using System.Collections;
using Liminal.SDK.VR;
using Liminal.SDK.VR.Input;
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
    [Min(0)]
    float launchForce = 6f;

    [SerializeField]
    [Tooltip("Treats fly in a straight line until they hit something. When off, they arc using Launch Force.")]
    bool straightShots = true;

    [SerializeField]
    [Min(0)]
    [Tooltip("How fast straight shots travel, in metres per second.")]
    float straightShotSpeed = 15f;

    [SerializeField]
    [Tooltip("Show a ring where each launcher's shot will land.")]
    bool showAimReticle = true;

    [SerializeField]
    bool enableAutofire = true;

    [SerializeField]
    [Tooltip("Rhythm mode: one treat per trigger pull, and shots on the beat score more. Overrides autofire.")]
    bool rhythmMode = true;

    [SerializeField]
    [Min(0)]
    float autofireRate = 6f;

    [SerializeField]
    TMP_Text ammoText;

    [SerializeField]
    Transform eye;

    [SerializeField]
    Transform _pickupMagnet;

    [SerializeField]
    string defaultDisplayText = "^ <rotate=\"-90\">3</rotate> ^";

    [SerializeField]
    string instaKillText = "<color=#FFD700><size=1.5em>∞</size></color>";

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

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("How far the grip must be squeezed to count as pressed on controllers with an analogue grip (e.g. Valve Index).")]
    float gripPressThreshold = 0.7f;

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

    bool yarnballReady;
    float yarnballRechargeTimer;

    bool gripHeldRight;
    bool gripHeldLeft;

    AimReticle reticleRight;
    AimReticle reticleLeft;

    float shotSpeed => straightShots ? straightShotSpeed : launchForce;

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
                ammoText.text = defaultDisplayText;
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

        // Grip state is tracked every frame so a press is only counted once
        bool gripDownRight = GripDown(true);
        bool gripDownLeft = GripDown(false);

        UpdateReticles();

        if (!GameManager.instance.enablePlayerInput) return;

        // Input goes through Liminal's VRDevice so the same code works on Quest (OVR) and Valve Index (OpenVR).
        // In the editor the mouse also works: left click = right trigger, right click = right grip.
        if (launchersActive)
        {
            if (gripDownRight && yarnballReady)
            {
                LaunchYarnball(true);
            }
            if (gripDownLeft && yarnballReady)
            {
                LaunchYarnball(false);
            }

            if (enableAutofire && !rhythmMode)
            {
                if (TriggerDown(true) && rightHandAutofire == null)
                {
                    rightHandAutofire = StartCoroutine(AutofireRoutine(true));
                }
                else if (TriggerUp(true) && rightHandAutofire != null)
                {
                    StopCoroutine(rightHandAutofire);
                    rightHandAutofire = null;
                }

                if (TriggerDown(false) && leftHandAutofire == null)
                {
                    leftHandAutofire = StartCoroutine(AutofireRoutine(false));
                }
                else if (TriggerUp(false) && leftHandAutofire != null)
                {
                    StopCoroutine(leftHandAutofire);
                    leftHandAutofire = null;
                }
            }
            else
            {
                if (TriggerDown(true))
                {
                    LaunchTreat(true);
                }
                if (TriggerDown(false))
                {
                    LaunchTreat(false);
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
                    if (TriggerDown(false) || FaceButtonDown(false))
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
                    if (TriggerDown(true) || FaceButtonDown(true))
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
    }

    void UpdateReticles()
    {
        bool show = showAimReticle && launchersActive && GameManager.instance.enablePlayerInput;

        if (show && reticleRight == null)
        {
            // Ignore the player's own hands and launchers when working out where shots land
            reticleRight = AimReticle.Create(treatLaunchPositionRight, transform, "Aim Reticle Right");
            reticleLeft = AimReticle.Create(treatLaunchPositionLeft, transform, "Aim Reticle Left");
        }

        if (reticleRight != null)
        {
            reticleRight.Configure(shotSpeed, !straightShots);
            reticleLeft.Configure(shotSpeed, !straightShots);
            reticleRight.SetVisible(show);
            reticleLeft.SetVisible(show);
        }
    }

    IVRInputDevice GetHandInput(bool rightHand)
    {
        IVRDevice device = VRDevice.Device;
        if (device == null) return null;
        return rightHand ? device.PrimaryInputDevice : device.SecondaryInputDevice;
    }

    bool TriggerDown(bool rightHand)
    {
        IVRInputDevice input = GetHandInput(rightHand);
        bool pressed = input != null && input.GetButtonDown(VRButton.Trigger);
#if UNITY_EDITOR
        if (rightHand) pressed |= Input.GetMouseButtonDown(0);
#endif
        return pressed;
    }

    bool TriggerUp(bool rightHand)
    {
        IVRInputDevice input = GetHandInput(rightHand);
        bool released = input != null && input.GetButtonUp(VRButton.Trigger);
#if UNITY_EDITOR
        if (rightHand) released |= Input.GetMouseButtonUp(0);
#endif
        return released;
    }

    // A / X on Quest controllers. Index controllers don't map this, so the trigger is used there instead.
    bool FaceButtonDown(bool rightHand)
    {
        IVRInputDevice input = GetHandInput(rightHand);
        return input != null && input.HasButton(VRButton.Four) && input.GetButtonDown(VRButton.Four);
    }

    // Quest reports the grip as a button; Index reports it as an analogue squeeze, so it's turned into a press here.
    bool GripDown(bool rightHand)
    {
        IVRInputDevice input = GetHandInput(rightHand);
        bool held = false;
        if (input != null)
        {
            held = input.HasButton(VRButton.Three)
                ? input.GetButton(VRButton.Three)
                : input.GetAxis1D(VRAxis.Three) >= gripPressThreshold;
        }

        bool wasHeld = rightHand ? gripHeldRight : gripHeldLeft;
        if (rightHand) gripHeldRight = held;
        else gripHeldLeft = held;

        bool pressed = held && !wasHeld;
#if UNITY_EDITOR
        if (rightHand) pressed |= Input.GetMouseButtonDown(1);
#endif
        return pressed;
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
        Haptics.PulseBoth(Haptics.Strength.Medium);

        if (rhythmMode)
        {
            BeatPulse.AddTo(launcherLeft, 0.12f);
            BeatPulse.AddTo(launcherRight, 0.12f);
        }

        GameManager.instance.GrabbedLaunchers();
    }

    IEnumerator AutofireRoutine(bool rightHand)
    {
        WaitForSeconds wait = new WaitForSeconds(1f / autofireRate);
        while (true)
        {
            LaunchTreat(rightHand);
            yield return wait;
        }
    }

    void LaunchTreat(bool rightHand)
    {
        Treat treat = treatsSpawner.GetRandomObjectFromPool<Treat>();
        if (treat == null) return;
        treat.gameObject.SetActive(true);
        treat.Launch(rightHand ? treatLaunchPositionRight : treatLaunchPositionLeft, shotSpeed, instaKillActive);
        if (straightShots) treat.MakeStraightShot();
        PlaySound(launcherFireSound, rightHand);

        if (rhythmMode && BeatClock.instance != null && BeatClock.instance.isRunning)
        {
            BeatClock.ShotQuality quality = BeatClock.instance.JudgeTiming();
            treat.shotQuality = quality;

            // The better the timing, the bigger the kick
            switch (quality)
            {
                case BeatClock.ShotQuality.Perfect:
                    Haptics.Pulse(rightHand, Haptics.Strength.Strong);
                    break;
                case BeatClock.ShotQuality.Great:
                    Haptics.Pulse(rightHand, Haptics.Strength.Medium);
                    break;
                default:
                    Haptics.Pulse(rightHand, Haptics.Strength.Light);
                    break;
            }
        }
        else
        {
            Haptics.Pulse(rightHand, Haptics.Strength.Light);
        }
    }

    void LaunchYarnball(bool rightHand)
    {
        Yarnball yarnball = treatsSpawner.GetObjectFromPool<Yarnball>(yarnballTreatSpawnerIndex);
        if (yarnball == null) return;
        yarnball.gameObject.SetActive(true);
        yarnball.Launch(rightHand ? treatLaunchPositionRight : treatLaunchPositionLeft, shotSpeed, false);
        if (straightShots) yarnball.MakeStraightShot();
        PlaySound(launcherYarnballSound, rightHand);
        Haptics.Pulse(rightHand, Haptics.Strength.Strong);

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
        ammoText.text = instaKillText;
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
