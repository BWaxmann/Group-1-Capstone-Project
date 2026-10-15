using System.Collections;
using Liminal.SDK.VR;
using Liminal.SDK.VR.Input;
using Valve.VR;
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
    [Tooltip("Made when the launchers are grabbed, unless the scene already has a Pickup Manager.")]
    PickupManager pickupManagerPrefab;

    [Header("Treat Launcher")]

    [SerializeField]
    GameObject launcherRight;

    [SerializeField]
    GameObject launcherLeft;

    [SerializeField]
    [Tooltip("Optional. A treats pool already in the scene; if empty, one is made from the prefab below.")]
    ObjectSpawner treatsSpawner;

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
    [Tooltip("Always allow holding the trigger to fire. When off, it's one treat per pull unless Rapid Fire is active.")]
    bool enableAutofire = false;

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
    string rapidFireText = "<color=#FF7310><size=1.2em>RAPID!</size></color>";

    [SerializeField]
    string spreadText = "<color=#3FD0FF><size=1.2em>SPREAD!</size></color>";

    [SerializeField]
    [Tooltip("Which launchers fire treats with their trigger.")]
    bool shootWithLeft = true;

    [SerializeField]
    bool shootWithRight = true;

    [Header("Barista Mode")]

    [SerializeField]
    [Tooltip("Space Cafe: each hand's launcher fires its own food, and flicking that hand's thumbstick left/right switches it (no yarnballs).")]
    bool baristaMode = false;

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
    [Tooltip("Grip as in the original game, the A/X button (frees the grip for the gravity beam), or off.")]
    YarnballButton yarnballButton = YarnballButton.Grip;

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

    public enum YarnballButton
    {
        Grip,
        FaceButton,
        Off
    }

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

    bool spreadActive;
    float spreadTimer;

    bool instaKillActive;
    float instaKillTimer;

    float rapidFireTimer;
    bool rapidFireActive => rapidFireTimer > 0;

    // Which treat (by position in the treat pool) each launcher fires. -1 = random, as before.
    int loadedTreatVariationLeft = -1;
    int loadedTreatVariationRight = -1;
    bool gripPressedLeft;
    bool gripPressedRight;
    int flickLeft;
    int flickRight;
    bool stickHeldLeft;
    bool stickHeldRight;

    public Vector3 pickupMagnet => _pickupMagnet.position;

    // Barista mode
    public bool isBaristaMode => baristaMode;
    public bool hasLaunchers => launchersActive;

    public int LoadedFood(bool rightHand) => rightHand ? loadedTreatVariationRight : loadedTreatVariationLeft;
    public Transform Launcher(bool rightHand) => rightHand ? launcherRight.transform : launcherLeft.transform;
    public bool GripPressed(bool rightHand) => rightHand ? gripPressedRight : gripPressedLeft;
    public Transform LaunchPoint(bool rightHand) => rightHand ? treatLaunchPositionRight : treatLaunchPositionLeft;

    // The player's head (what billboards turn to face)
    public Transform eyePoint => eye;

    // Fire styles (Cat Cafe Rush trial, finale only): each launcher can fire one treat, a wide scatter, or a tight
    // short-range buckshot. Flicking that hand's thumbstick left/right swaps its style while they're unlocked.
    public enum FireStyle
    {
        Single,
        Scatter,
        Buckshot
    }

    public bool fireStylesUnlocked { get; private set; }
    FireStyle styleLeft;
    FireStyle styleRight;
    static int lastShotId;

    public FireStyle Style(bool rightHand) => rightHand ? styleRight : styleLeft;

    public void UnlockFireStyles(bool unlocked)
    {
        fireStylesUnlocked = unlocked;
        if (!unlocked) styleLeft = styleRight = FireStyle.Single;
        ShowStyles();
    }

    void SwapStyle(bool rightHand, int direction)
    {
        int count = System.Enum.GetValues(typeof(FireStyle)).Length;
        FireStyle next = (FireStyle)(((int)Style(rightHand) + direction + count) % count);
        if (rightHand) styleRight = next;
        else styleLeft = next;
        Haptics.Pulse(rightHand, Haptics.Strength.Medium);
        ShowStyles();
    }

    // The launchers' little display shows each hand's style while they're unlocked
    void ShowStyles()
    {
        if (!fireStylesUnlocked)
        {
            RefreshAmmoText();
            return;
        }
        ammoText.text = $"<size=55%>{styleLeft.ToString().ToUpper()}  |  {styleRight.ToString().ToUpper()}</size>";
    }

    // Spread of each style (degrees either side) and pellets per shot
    static float StyleSpread(FireStyle style) => style == FireStyle.Scatter ? 14f : style == FireStyle.Buckshot ? 5f : 0f;
    static int StylePellets(FireStyle style) => style == FireStyle.Scatter ? 5 : style == FireStyle.Buckshot ? 7 : 1;

    // Colours each launcher's aim reticle (e.g. the colour that hand fires); null = the reticle's own colours
    public void SetReticleColours(Color left, Color right)
    {
        reticleColourLeft = left;
        reticleColourRight = right;
        ApplyReticleColours();
    }

    Color? reticleColourLeft;
    Color? reticleColourRight;

    void ApplyReticleColours()
    {
        if (reticleLeft != null && reticleColourLeft.HasValue) reticleLeft.SetColours(Faded(reticleColourLeft.Value), reticleColourLeft.Value);
        if (reticleRight != null && reticleColourRight.HasValue) reticleRight.SetColours(Faded(reticleColourRight.Value), reticleColourRight.Value);
    }

    static Color Faded(Color colour)
    {
        colour.a = 0.75f;
        return colour;
    }

    // How fast straight shots fly, in m/s (a scene can speed them up)
    public float treatSpeed
    {
        get => straightShotSpeed;
        set => straightShotSpeed = Mathf.Max(1f, value);
    }

    // True for as long as that hand's grip is squeezed (in the editor, the right mouse button is the right grip)
    public bool GripHeld(bool rightHand)
    {
        bool held = rightHand ? gripHeldRight : gripHeldLeft;
#if UNITY_EDITOR
        if (rightHand) held |= Input.GetMouseButton(1);
#endif
        return held;
    }

    // A firm left/right flick of that hand's thumbstick this frame: -1 left, +1 right, 0 none
    public int ThumbFlick(bool rightHand) => rightHand ? flickRight : flickLeft;

    public void LoadFood(bool rightHand, int treatPoolVariation)
    {
        if (rightHand) loadedTreatVariationRight = treatPoolVariation;
        else loadedTreatVariationLeft = treatPoolVariation;
    }

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
        // No yarnball in this scene: its recharge bar goes too
        if (yarnballButton == YarnballButton.Off && yarnballRechargeBar != null) yarnballRechargeBar.gameObject.SetActive(false);

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
                RefreshAmmoText();
            }
        }

        if (spreadActive)
        {
            spreadTimer -= Time.deltaTime;
            if (spreadTimer <= 0)
            {
                spreadTimer = 0;
                spreadActive = false;
                RefreshAmmoText();
            }
        }

        if (rapidFireActive)
        {
            rapidFireTimer -= Time.deltaTime;
            if (!rapidFireActive)
            {
                rapidFireTimer = 0;
                if (!enableAutofire)
                {
                    // Back to one shot per pull
                    StopLaunchers();
                }
                RefreshAmmoText();
            }
        }

        if (!yarnballReady && yarnballButton != YarnballButton.Off)
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
        gripPressedLeft = gripDownLeft;
        gripPressedRight = gripDownRight;
        flickLeft = ReadThumbFlick(false);
        flickRight = ReadThumbFlick(true);
        if (fireStylesUnlocked && launchersActive && !baristaMode)
        {
            if (flickLeft != 0) SwapStyle(false, flickLeft);
            if (flickRight != 0) SwapStyle(true, flickRight);
        }

        UpdateReticles();

        if (!GameManager.instance.enablePlayerInput) return;

        // Input goes through Liminal's VRDevice so the same code works on Quest (OVR) and Valve Index (OpenVR).
        // In the editor the mouse also works: left click = right trigger, right click = right grip.
        if (launchersActive)
        {
            // In barista mode the grips switch food instead (handled by the Space Cafe)
            if (yarnballReady && !baristaMode && yarnballButton != YarnballButton.Off)
            {
                bool useGrip = yarnballButton == YarnballButton.Grip;
                if (useGrip ? gripDownRight : FaceButtonDown(true))
                {
                    LaunchYarnball(true);
                }
                else if (useGrip ? gripDownLeft : FaceButtonDown(false))
                {
                    LaunchYarnball(false);
                }
            }

            if (enableAutofire || rapidFireActive)
            {
                if (shootWithRight && TriggerDown(true) && rightHandAutofire == null)
                {
                    rightHandAutofire = StartCoroutine(AutofireRoutine(true));
                }
                else if (TriggerUp(true) && rightHandAutofire != null)
                {
                    StopCoroutine(rightHandAutofire);
                    rightHandAutofire = null;
                }

                if (shootWithLeft && TriggerDown(false) && leftHandAutofire == null)
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
                if (shootWithRight && TriggerDown(true))
                {
                    LaunchTreat(true);
                }
                if (shootWithLeft && TriggerDown(false))
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
            ApplyReticleColours();
        }

        if (reticleRight != null)
        {
            reticleRight.Configure(shotSpeed, !straightShots);
            reticleLeft.Configure(shotSpeed, !straightShots);
            reticleRight.SetSpread(StyleSpread(styleRight));
            reticleLeft.SetSpread(StyleSpread(styleLeft));
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

#if UNITY_ANDROID && !UNITY_EDITOR
    // On the Quest, input is read straight from OVRInput (the same buttons Liminal maps), because
    // Liminal's Quest controller builds a new button dictionary on every query, which causes stutters.
    static OVRInput.Controller OvrHand(bool rightHand) => rightHand ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;
#endif

    bool TriggerDown(bool rightHand)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OvrHand(rightHand));
#else
        IVRInputDevice input = GetHandInput(rightHand);
        bool pressed = input != null && input.GetButtonDown(VRButton.Trigger);
#if UNITY_EDITOR
        if (rightHand) pressed |= Input.GetMouseButtonDown(0);
#endif
        return pressed;
#endif
    }

    bool TriggerUp(bool rightHand)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, OvrHand(rightHand));
#else
        IVRInputDevice input = GetHandInput(rightHand);
        bool released = input != null && input.GetButtonUp(VRButton.Trigger);
#if UNITY_EDITOR
        if (rightHand) released |= Input.GetMouseButtonUp(0);
#endif
        return released;
#endif
    }

    // A / X on Quest controllers. On the Index, A is bound to SteamVR's "Secondary" action (bindings_knuckles.json).
    bool FaceButtonDown(bool rightHand)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return OVRInput.GetDown(OVRInput.Button.One, OvrHand(rightHand));
#else
        IVRDevice device = VRDevice.Device;
        if (device != null && device.Name == "OpenVR")
        {
            SteamVR_Action_Boolean secondary = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("default", "Secondary");
            return secondary != null && secondary.GetStateDown(rightHand ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand);
        }
        IVRInputDevice input = GetHandInput(rightHand);
        return input != null && input.HasButton(VRButton.Four) && input.GetButtonDown(VRButton.Four);
#endif
    }

    // Index: uses SteamVR's thumbstick left/right flick (bound as snap-turn, which this game doesn't use).
    // Quest: reads the thumbstick directly. Editor: the mouse scroll wheel flicks the right hand.
    int ReadThumbFlick(bool rightHand)
    {
        int direction = 0;
        IVRDevice device = VRDevice.Device;
        if (device != null && device.Name == "OpenVR")
        {
            SteamVR_Input_Sources source = rightHand ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand;
            SteamVR_Action_Boolean flickRightAction = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("default", "SnapTurnRight");
            SteamVR_Action_Boolean flickLeftAction = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("default", "SnapTurnLeft");
            if (flickRightAction != null && flickRightAction.GetStateDown(source)) direction = 1;
            else if (flickLeftAction != null && flickLeftAction.GetStateDown(source)) direction = -1;
        }
        else
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            float x = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OvrHand(rightHand)).x;
#else
            IVRInputDevice input = GetHandInput(rightHand);
            float x = input != null ? input.GetAxis2D(VRAxis.One).x : 0f;
#endif
            bool held = Mathf.Abs(x) > 0.7f;
            bool wasHeld = rightHand ? stickHeldRight : stickHeldLeft;
            if (held && !wasHeld) direction = x > 0 ? 1 : -1;
            if (rightHand) stickHeldRight = held;
            else stickHeldLeft = held;
        }

#if UNITY_EDITOR
        if (rightHand && direction == 0)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (scroll > 0.1f) direction = 1;
            else if (scroll < -0.1f) direction = -1;
        }
#endif
        return direction;
    }

    // Quest reports the grip as a button; Index reports it as an analogue squeeze, so it's turned into a press here.
    bool GripDown(bool rightHand)
    {
        bool held = false;
#if UNITY_ANDROID && !UNITY_EDITOR
        held = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OvrHand(rightHand));
#else
        IVRInputDevice input = GetHandInput(rightHand);
        if (input != null)
        {
            held = input.HasButton(VRButton.Three)
                ? input.GetButton(VRButton.Three)
                : input.GetAxis1D(VRAxis.Three) >= gripPressThreshold;
        }
#endif

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

        GameManager.instance.GrabbedLaunchers();

        // Older scenes keep their treats pool and Pickup Manager in the scene; otherwise they're made now
        if (treatsSpawner == null && treatsSpawnerPrefab != null)
        {
            treatsSpawner = Instantiate(treatsSpawnerPrefab);
        }
        PickupManager pickupManager = PickupManager.instance;
        if (pickupManager == null && pickupManagerPrefab != null)
        {
            pickupManager = Instantiate(pickupManagerPrefab);
        }
        if (pickupManager != null)
        {
            pickupManager.SetPlayerVisionPoint(eye);
        }
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
        if (spreadActive)
        {
            LaunchSpread(rightHand);
            return;
        }

        FireStyle style = Style(rightHand);
        int pellets = StylePellets(style);
        float spread = StyleSpread(style);
        int shotId = ++lastShotId;
        Transform muzzle = rightHand ? treatLaunchPositionRight : treatLaunchPositionLeft;
        for (int i = 0; i < pellets; i++)
        {
            // Spread the volley around the aim, inside a cone (the first pellet flies true)
            Transform aim = muzzle;
            if (spread > 0 && i > 0)
            {
                aim = SpreadAim(rightHand, muzzle);
                Vector2 offset = Random.insideUnitCircle * spread;
                aim.localRotation = Quaternion.Euler(-offset.y, offset.x, 0);
            }
            Treat treat = LaunchOneTreat(rightHand, aim, i == 0);
            if (treat == null) return;
            treat.shotId = shotId;
            if (style == FireStyle.Buckshot) treat.ShortenLife(0.4f);
        }
    }

    // Spread Shot pickup: a fan of treats, evenly spaced from one side of Max Spread Angle to the other
    void LaunchSpread(bool rightHand)
    {
        int shotId = ++lastShotId;
        Transform muzzle = rightHand ? treatLaunchPositionRight : treatLaunchPositionLeft;
        for (int i = 0; i < numberOfShotsInSpread; i++)
        {
            float angle = (1f - 2f * i / (numberOfShotsInSpread - 1)) * maxSpreadAngle;
            Treat treat = LaunchOneTreat(rightHand, muzzle, i == 0, angle);
            if (treat == null) return;
            treat.shotId = shotId;
        }
    }

    Transform spreadAimLeft;
    Transform spreadAimRight;

    // A pointer at the muzzle that can be turned, to aim a pellet off-centre
    Transform SpreadAim(bool rightHand, Transform muzzle)
    {
        Transform aim = rightHand ? spreadAimRight : spreadAimLeft;
        if (aim == null)
        {
            aim = new GameObject("Spread Aim").transform;
            aim.SetParent(muzzle, false);
            if (rightHand) spreadAimRight = aim;
            else spreadAimLeft = aim;
        }
        return aim;
    }

    Treat LaunchOneTreat(bool rightHand)
    {
        return LaunchOneTreat(rightHand, rightHand ? treatLaunchPositionRight : treatLaunchPositionLeft, true);
    }

    Treat LaunchOneTreat(bool rightHand, Transform from, bool withFeedback, float angle = 0)
    {
        int variation = LoadedFood(rightHand);
        Treat treat = variation >= 0
            ? treatsSpawner.GetObjectFromPool<Treat>(variation)
            : treatsSpawner.GetRandomObjectFromPool<Treat>();
        if (treat == null)
        {
            Debug.LogWarning($"[Space Cafe] {Time.timeSinceLevelLoad:F1}s: no treat available to fire (food {variation}, {(rightHand ? "right" : "left")} hand)");
            return null;
        }
        treat.foodVariation = variation;
        treat.shotId = 0;
        treat.gameObject.SetActive(true);
        treat.Launch(from, shotSpeed, angle, instaKillActive);
        if (straightShots) treat.MakeStraightShot();
        // One sound and buzz per trigger pull, not per pellet
        if (withFeedback)
        {
            PlaySound(launcherFireSound, rightHand);
            Haptics.Pulse(rightHand, Haptics.Strength.Light);
        }
        return treat;
    }

    void LaunchYarnball(bool rightHand)
    {
        Yarnball yarnball = treatsSpawner.GetObjectFromPool<Yarnball>(yarnballTreatSpawnerIndex);
        if (yarnball == null) return;
        yarnball.gameObject.SetActive(true);
        yarnball.Launch(rightHand ? treatLaunchPositionRight : treatLaunchPositionLeft, shotSpeed, 0, false);
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
        RefreshAmmoText();
    }

    public void EnableRapidFire(float duration)
    {
        rapidFireTimer += duration;
        Haptics.PulseBoth(Haptics.Strength.Strong);
        RefreshAmmoText();
    }

    public void ActivateSpread(float duration)
    {
        spreadActive = true;
        spreadTimer += duration;
        Haptics.PulseBoth(Haptics.Strength.Strong);
        RefreshAmmoText();
    }

    void RefreshAmmoText()
    {
        if (instaKillActive) ammoText.text = instaKillText;
        else if (spreadActive) ammoText.text = spreadText;
        else if (rapidFireActive) ammoText.text = rapidFireText;
        else ammoText.text = defaultDisplayText;
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
