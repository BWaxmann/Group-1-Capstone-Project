using System;
using System.Collections;
using System.Collections.Generic;
using Liminal.Core.Fader;
using Liminal.SDK.Core; // This is needed for ExperienceApp.End()
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

// Wakes before everything else in the scene, so a new run starts with every static put back first (RunReset)
[DefaultExecutionOrder(-1000)]
public class GameManager : MonoBehaviour
{
    [SerializeField]
    [Min(0)]
    float pregameDelay = 0.5f;

    [SerializeField]
    [Min(0)]
    int pregameCountdownTime = 3;

    [SerializeField]
    float[] roundMaxTimes = new float[] { 90, 90, 90 };

    [SerializeField]
    float[] roundMinTimes = new float[] { 30, 30, 30 };

    [SerializeField]
    [Min(0)]
    int maxCatsToEndRoundEarly = 3;

    [SerializeField]
    [Min(0)]
    float pauseBetweenRounds = 1f;

    [SerializeField]
    [Min(0)]
    float fadeDuration = 1f;

    [SerializeField]
    ParticleManager particleManagerPrefab;

    [Header("UI")]

    [SerializeField]
    GameObject pregameCanvas;

    [SerializeField]
    [TextArea]
    [Tooltip("Optional. Shown on the countdown canvas before the round, until the countdown starts.")]
    string startupMessage = "";

    [SerializeField]
    [Tooltip("Optional. Boxes the start-up message is spread across instead (split at blank lines, one part per box). Empty = all in the countdown text.")]
    TMP_Text[] startupBoxes;

    [SerializeField]
    [Tooltip("When the countdown canvas shares a board with the score (e.g. the chalkboard), announcements hide the score while they show.")]
    bool bannerReplacesGameUI = false;

    [SerializeField]
    TMP_Text countdownText;

    [SerializeField]
    string countdownEndText = "GO!";

    [SerializeField]
    [Min(0)]
    float countdownDisappearDelay = 1f;

    [SerializeField]
    GameObject[] gameCanvases;

    [SerializeField]
    TMP_Text scoreText;

    [SerializeField]
    GameObject bonusDisplay;

    [SerializeField]
    TMP_Text bonusText;

    [SerializeField]
    TMP_Text roundText;

    [SerializeField]
    TMP_Text timerText;

    [SerializeField]
    GameObject postgameCanvas;

    [SerializeField]
    GameObject timeUpText;

    [SerializeField]
    GameObject finalScorePanel;

    [SerializeField]
    [Min(0)]
    float timeUpDisplayDuration = 2f;

    [SerializeField]
    float finalScoreDisplayDuration = 10f;

    [SerializeField]
    TMP_Text catsFedText;

    [SerializeField]
    TMP_Text penaltyText;

    [SerializeField]
    TMP_Text finalScoreText;

    [SerializeField]
    [Tooltip("Optional. A score particle pool already in the scene; if empty, one is made from the prefab below.")]
    ObjectSpawner scoreParticlePool;

    [SerializeField]
    ObjectSpawner scoreParticlePoolPrefab;

    [Header("Sounds")]

    [SerializeField]
    AudioSource soundEffectSource;

    [SerializeField]
    AudioSource musicSource;

    [SerializeField]
    SoundData countdownSound = new SoundData(1f, 1f);

    [SerializeField]
    [Tooltip("Mark as true if the full countdown is included in a single clip.")]
    bool countdownSoundIsCombined = false;

    [SerializeField]
    [Tooltip("Mark as false if the game start sound is included in the countdown sound clip.")]
    bool separateGameStartSound = true;

    [SerializeField]
    SoundData gameStartSound = new SoundData(1f, 1f);

    [SerializeField]
    SoundData roundStartSound = new SoundData(1f, 1f);

    [SerializeField]
    SoundData scoreUpSound = new SoundData(1f, 1f);

    [SerializeField]
    SoundData scoreDownSound = new SoundData(1f, 1f);

    [SerializeField]
    SoundData gameEndSound = new SoundData(1f, 1f);

    [SerializeField]
    SoundData gameMusicIntroAndOutro = new SoundData(1f, 1f);

    [SerializeField]
    SoundData gameMusicLoop = new SoundData(1f, 1f);

    [SerializeField]
    [Min(0)]
    float introLength = 10f;

    [SerializeField]
    [Min(0)]
    float outroLength = 1f;

    [SerializeField]
    SoundData postgameMusic = new SoundData(1f, 1f);

    [Header("Cat Feel")]

    [SerializeField]
    [Tooltip("A random one plays each time a treat hits a cat, a little higher for each treat.")]
    SoundData[] catHitSounds;

    [SerializeField]
    [Tooltip("Plays as a fed cat launches into the air.")]
    SoundData catLaunchSound = new SoundData(1f, 1f);

    [SerializeField]
    [Tooltip("Plays when a launched cat pops into confetti.")]
    SoundData catPopSound = new SoundData(1f, 1f);

    [Header("Combo")]

    [SerializeField]
    [Min(0.1f)]
    [Tooltip("Seconds the player has to feed another cat before the combo starts winding down.")]
    float comboWindow = 3f;

    [SerializeField]
    [Min(1)]
    [Tooltip("Cats fed in a row needed to go up one combo level.")]
    int catsPerComboLevel = 3;

    [SerializeField]
    [Min(1)]
    int maxComboLevel = 4;

    [SerializeField]
    [Min(0)]
    [Tooltip("How much higher the score sound gets per combo level.")]
    float comboPitchStep = 0.08f;

    [SerializeField]
    [Tooltip("Optional. If left empty, a copy of the score text is created just below it.")]
    TMP_Text comboText;

    [SerializeField]
    string comboTextFormat = "COMBO x{0}!";

    [SerializeField]
    Color comboTextColour = new Color(1f, 0.84f, 0f);

    [Header("Finale")]

    [SerializeField]
    [Min(0)]
    [Tooltip("The finale starts when this many seconds are left. Set to 0 to turn it off.")]
    float finaleDuration = 30f;

    [SerializeField]
    string finaleBannerText = "LAST ORDERS!";

    [SerializeField]
    [Min(0)]
    float finaleBannerDuration = 2.5f;

    [SerializeField]
    [Min(0.1f)]
    float finaleMusicPitch = 1.12f;

    [SerializeField]
    SoundData finaleSound = new SoundData(1f, 1f);

    [SerializeField]
    [Min(0)]
    [Tooltip("When time runs out, the remaining cats leave happy over this many seconds.")]
    float endCelebrationSpread = 1f;

    [Header("End Screen")]

    [SerializeField]
    [Tooltip("Optional. If left empty, a copy of the final score text is created below the final score panel.")]
    TMP_Text rankText;

    [SerializeField]
    [Tooltip("Optional. If left empty, a copy of the final score text is created below the rank text.")]
    TMP_Text bestScoreText;

    [SerializeField]
    [Tooltip("Ordered from lowest to highest score. Every score gets a rank, so nobody fails.")]
    Rank[] ranks = new Rank[]
    {
        new Rank(0, "Cat Sitter"),
        new Rank(800, "Good Kitty Keeper"),
        new Rank(2000, "Treat Master"),
        new Rank(3500, "Purr-fect Barista")
    };

    public static GameManager instance;

    public delegate void GameStateChange();
    public event GameStateChange StartCountdown;
    public event GameStateChange GameStart;
    public event GameStateChange FinaleStart;
    public event GameStateChange EndRound;
    public event GameStateChange GameEnd;

    public event Action<int> StartNewRound;

    bool playerInputsEnabled;
    int currentRound;
    int catsInScene;
    int catsFed;
    int penalties;
    int score;
    float scoreMultiplier;
    bool endRoundEarly;
    GameState state;

    List<DefendPoint> defendPoints = new List<DefendPoint>();

    int comboCount;
    int comboLevel = 1;
    float comboTimer;
    Coroutine comboPunchRoutine;
    bool finaleStarted;

    public bool enablePlayerInput => playerInputsEnabled;

    // While true the round clock stands still (e.g. while something new is being explained)
    public bool clockPaused { get; set; }

    // The scoreboard's texts, for modes that show their own numbers on it (Cat Cafe Waves shows tips)
    public TMP_Text scoreDisplay => scoreText;
    public TMP_Text bonusDisplayText => bonusText;
    public TMP_Text countdownDisplay => countdownText;
    public TMP_Text finalScoreDisplay => finalScoreText;
    public TMP_Text catsFedDisplay => catsFedText;
    public TMP_Text penaltyDisplay => penaltyText;
    public GameObject finalScorePanelObject => finalScorePanel;
    public GameObject timeUpObject => timeUpText;
    public TMP_Text rankDisplay => rankText;
    public TMP_Text bestScoreDisplay => bestScoreText;

    void Awake()
    {
        if (instance == null) RunReset.ForNewRun();   // a new run (Liminal can run it again without a restart)
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        pregameCanvas.SetActive(false);
        ShowGameCanvases(false);
        postgameCanvas.SetActive(false);

        if (!string.IsNullOrEmpty(startupMessage) && countdownText != null)
        {
            ShowStartupMessage();
            pregameCanvas.SetActive(true);
        }

        state = GameState.Initialising;
        playerInputsEnabled = true;

        if (comboText == null && scoreText != null)
        {
            comboText = CreateTextBelow(scoreText, scoreText.rectTransform, 0.6f, false);
        }
        if (comboText != null)
        {
            comboText.color = comboTextColour;
            comboText.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (state != GameState.DuringGame || comboCount == 0) return;

        comboTimer -= Time.deltaTime;
        if (comboTimer <= 0)
        {
            // Wind the combo down one level at a time instead of dropping it to nothing
            if (comboLevel > 1)
            {
                comboLevel--;
                comboCount = (comboLevel - 1) * catsPerComboLevel;
                comboTimer = comboWindow;
            }
            else
            {
                comboCount = 0;
            }
            ShowCombo(false);
        }
    }

    public void RegisterDefendPoint(DefendPoint defendPoint)
    {
        if (defendPoints == null)
        {
            defendPoints = new List<DefendPoint>();
        }

        if (defendPoint != null && !defendPoints.Contains(defendPoint))
        {
            defendPoints.Add(defendPoint);
        }
    }

    public void GrabbedLaunchers()
    {
        playerInputsEnabled = false;

        Invoke(nameof(InitiatePregame), pregameDelay);
        StartCountdown?.Invoke();

        if (particleManagerPrefab != null && ParticleManager.instance == null) Instantiate(particleManagerPrefab);
    }

    IEnumerator PreGameRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(1);

        for (int i = 0; i < pregameCountdownTime; i++)
        {
            countdownText.text = (pregameCountdownTime - i).ToString();
            if (!countdownSoundIsCombined) PlaySoundEffect(countdownSound);

            yield return wait;
        }

        countdownText.text = countdownEndText;
        InitiateGame();
    }

    IEnumerator GameRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(1);
        WaitForSeconds interRoundWait = new WaitForSeconds(pauseBetweenRounds);

        currentRound = 1;
        bool roundEndedEarly = false;
        for (int i = 0; i < roundMaxTimes.Length; i++)
        {
            if (i > 0)
            {
                yield return interRoundWait;

                currentRound++;
                roundStartSound.PlayOneShot(soundEffectSource);
            }
            if (roundText != null) roundText.text = currentRound.ToString();
            StartNewRound?.Invoke(currentRound);

            float roundTime = roundMaxTimes[i];
            if (Mathf.Floor(roundTime) < roundTime)
            {
                timerText.text = ClockText(roundTime, roundTime);
                yield return new WaitForSeconds(roundTime % 1);
            }

            int steps = Mathf.FloorToInt(roundTime);
            for (int j = 0; j < steps; j++)
            {
                while (clockPaused) yield return null;
                timerText.text = ClockText(steps - j, roundTime);

                // The finale is the end of the last round
                if (!finaleStarted && finaleDuration > 0 && i == roundMaxTimes.Length - 1 && steps - j <= finaleDuration)
                {
                    StartFinale();
                }

                yield return wait;

                if (endRoundEarly && j > roundMinTimes[i] && catsInScene <= maxCatsToEndRoundEarly)
                {
                    roundEndedEarly = true;
                    break;
                }
            }

            if (roundEndedEarly)
            {
                roundEndedEarly = false;
            }
            else
            {
                timerText.text = ClockText(0, roundTime);
            }
            endRoundEarly = false;

            EndRound?.Invoke();
        }

        yield return StartCoroutine(FadeOutMusic());

        EndGame();
    }

    IEnumerator PostGameRoutine()
    {
        yield return new WaitForSeconds(timeUpDisplayDuration);

        timeUpText.SetActive(false);
        finalScorePanel.SetActive(true);
        catsFedText.text = catsFed.ToString();
        penaltyText.text = penalties.ToString();
        finalScoreText.text = score.ToString();
        ShowRankAndBestScore();

        yield return new WaitForSeconds(finalScoreDisplayDuration);

        state = GameState.FadeOut;
        FadeOut();

        yield return new WaitForSeconds(fadeDuration);

        ExitGame();
    }

    // The start-up message across the boxes if there are any (one part per box, split at blank lines), else in the
    // countdown text
    void ShowStartupMessage()
    {
        if (startupBoxes == null || startupBoxes.Length == 0)
        {
            countdownText.text = startupMessage;
            return;
        }

        string[] parts = startupMessage.Replace("\r", "").Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < startupBoxes.Length; i++)
        {
            if (startupBoxes[i] == null) continue;
            startupBoxes[i].text = i < parts.Length ? parts[i].Trim() : "";
            startupBoxes[i].gameObject.SetActive(i < parts.Length);
        }
        countdownText.text = "";
    }

    void HideStartupBoxes()
    {
        if (startupBoxes == null) return;
        foreach (TMP_Text box in startupBoxes)
        {
            if (box != null) box.gameObject.SetActive(false);
        }
    }

    void InitiatePregame()
    {
        state = GameState.PreGame;
        pregameCanvas.SetActive(true);
        HideStartupBoxes();
        if (countdownSoundIsCombined) PlaySoundEffect(countdownSound);

        StartCoroutine(PreGameRoutine());

        if (scoreParticlePool == null && scoreParticlePoolPrefab != null) scoreParticlePool = Instantiate(scoreParticlePoolPrefab);
    }

    void InitiateGame()
    {
        if (separateGameStartSound) PlaySoundEffect(gameStartSound);
        PlayMusic(gameMusicIntroAndOutro);
        Invoke(nameof(BeginLoop), introLength);
        
        state = GameState.DuringGame;
        playerInputsEnabled = true;
        score = 0;
        scoreMultiplier = 1;
        comboCount = 0;
        comboLevel = 1;
        finaleStarted = false;
        bonusDisplay.SetActive(false);
        Invoke(nameof(HidePregameCanvas), countdownDisappearDelay);

        // On a shared board the score appears once "GO!" has cleared
        ShowGameCanvases(!bannerReplacesGameUI);
        scoreText.text = "0";

        StartCoroutine(GameRoutine());
        GameStart?.Invoke();
    }

    void HidePregameCanvas()
    {
        pregameCanvas.SetActive(false);
        if (bannerReplacesGameUI && state == GameState.DuringGame) ShowGameCanvases(true);
    }

    void ShowGameCanvases(bool show)
    {
        foreach (GameObject canvas in gameCanvases)
        {
            canvas.SetActive(show);
        }
    }

    void EndGame()
    {
        PlaySoundEffect(gameEndSound);
        PlayMusic(postgameMusic, loop: false);
        state = GameState.PostGame;
        playerInputsEnabled = false;
        ShowGameCanvases(false);
        postgameCanvas.SetActive(true);
        timeUpText.SetActive(true);
        finalScorePanel.SetActive(false);
        if (comboText != null) comboText.gameObject.SetActive(false);

        Haptics.PulseBoth(Haptics.Strength.Huge);
        StartCoroutine(PostGameRoutine());
        StartCoroutine(CelebrateRemainingCats());
        GameEnd?.Invoke();
    }

    // Big announcement text in front of the player (uses the countdown canvas)
    public void ShowBanner(string text, float duration)
    {
        if (countdownText != null && pregameCanvas != null)
        {
            CancelInvoke(nameof(HidePregameCanvas));
            countdownText.text = text;
            pregameCanvas.SetActive(true);
            if (bannerReplacesGameUI && state == GameState.DuringGame) ShowGameCanvases(false);
            Invoke(nameof(HidePregameCanvas), duration);
        }
    }

    void StartFinale()
    {
        finaleStarted = true;
        // An empty banner text means another script announces the finale itself
        if (!string.IsNullOrEmpty(finaleBannerText)) ShowBanner(finaleBannerText, finaleBannerDuration);

        PlaySoundEffect(finaleSound);
        Haptics.PulseBoth(Haptics.Strength.Strong);
        StartCoroutine(RampMusicPitch(finaleMusicPitch, 2f));

        FinaleStart?.Invoke();
    }

    // Turns the music down to `level` of where it is (e.g. under the Mafia cat's theme in Cat Cafe Waves) for `hold`
    // seconds, then back up. EndDuck brings it back up sooner.
    public void DuckMusic(float level, float hold)
    {
        if (musicSource == null) return;
        if (duckRoutine != null) StopCoroutine(duckRoutine);
        else unduckedVolume = musicSource.volume;
        duckRoutine = StartCoroutine(DuckRoutine(level, hold));
    }

    public void EndDuck()
    {
        if (duckRoutine == null) return;
        StopCoroutine(duckRoutine);
        duckRoutine = StartCoroutine(DuckRoutine(1f, 0f));
    }

    // Cat Cafe Waves: the clock shows the time of day through a shift (e.g. 9:00 AM to 5:00 PM, in five-minute steps)
    // instead of seconds left, filling the clock face
    public void UseShiftClock(float startHour, float endHour)
    {
        shiftClock = true;
        shiftStartHour = startHour;
        shiftEndHour = endHour;
        if (timerText == null) return;
        // The clock's other labels (its canvas can still be switched off here, so it's found by the parent, not .canvas)
        Transform face = timerText.transform.parent != null ? timerText.transform.parent : timerText.transform;
        foreach (TMP_Text other in face.GetComponentsInChildren<TMP_Text>(true))
        {
            if (other != timerText) other.gameObject.SetActive(false);
        }
        RectTransform box = timerText.rectTransform;
        box.anchorMin = Vector2.zero;
        box.anchorMax = Vector2.one;
        box.offsetMin = box.offsetMax = Vector2.zero;
        timerText.enableWordWrapping = false;
        timerText.enableAutoSizing = true;
        timerText.fontSizeMax = Mathf.Max(timerText.fontSizeMax, 80f);
        timerText.text = ClockText(1f, 1f);
    }

    bool shiftClock;
    float shiftStartHour = 9f;
    float shiftEndHour = 17f;

    string ClockText(float secondsLeft, float roundLength)
    {
        if (!shiftClock) return Mathf.CeilToInt(secondsLeft).ToString();
        float through = roundLength > 0 ? Mathf.Clamp01(1f - secondsLeft / roundLength) : 1f;
        int minutes = Mathf.FloorToInt((shiftStartHour + (shiftEndHour - shiftStartHour) * through) * 12f) * 5;
        int hour = (minutes / 60) % 24;
        string half = hour < 12 ? "AM" : "PM";
        int shown = hour % 12 == 0 ? 12 : hour % 12;
        return $"{shown}:{minutes % 60:00} {half}";
    }

    // Sets how long a single-round game lasts, in seconds (before it starts: e.g. Cat Cafe Waves, to fit its waves)
    public void SetRoundLength(float seconds)
    {
        if (roundMaxTimes != null && roundMaxTimes.Length == 1 && seconds > 0) roundMaxTimes[0] = seconds;
    }

    Coroutine duckRoutine;
    float unduckedVolume = 1f;

    IEnumerator DuckRoutine(float level, float hold)
    {
        // Eased both ways (down fairly quickly, back up gently), so it doesn't lurch
        yield return FadeMusicTo(unduckedVolume * level, level < 1f ? 0.6f : 1.5f);
        if (level < 1f)
        {
            yield return new WaitForSeconds(hold);
            yield return FadeMusicTo(unduckedVolume, 1.5f);
        }
        duckRoutine = null;
    }

    IEnumerator FadeMusicTo(float target, float seconds)
    {
        float from = musicSource.volume;
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / seconds;
            musicSource.volume = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
    }

    IEnumerator RampMusicPitch(float targetPitch, float duration)
    {
        if (musicSource == null) yield break;

        float startPitch = musicSource.pitch;
        float timer = 0;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            musicSource.pitch = Mathf.Lerp(startPitch, targetPitch, timer / duration);
            yield return null;
        }
    }

    IEnumerator CelebrateRemainingCats()
    {
        CatMovement[] cats = FindObjectsOfType<CatMovement>();
        foreach (CatMovement cat in cats)
        {
            if (endCelebrationSpread > 0)
            {
                yield return new WaitForSeconds(endCelebrationSpread / Mathf.Max(cats.Length, 1));
            }

            if (cat != null)
            {
                cat.CelebrateAndVanish();
            }
        }
    }

    void ShowCombo(bool levelledUp)
    {
        if (comboText == null) return;

        if (comboLevel <= 1)
        {
            comboText.gameObject.SetActive(false);
            return;
        }

        comboText.gameObject.SetActive(true);
        comboText.text = string.Format(comboTextFormat, comboLevel);

        if (levelledUp)
        {
            if (comboPunchRoutine != null) StopCoroutine(comboPunchRoutine);
            comboPunchRoutine = StartCoroutine(PunchScale(comboText.transform, 1.6f, 0.35f));
        }
    }

    IEnumerator PunchScale(Transform target, float peakScale, float duration)
    {
        float timer = 0;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            // Grows quickly, then settles back to normal size
            float scale = Mathf.Lerp(peakScale, 1f, t * t);
            target.localScale = Vector3.one * scale;
            yield return null;
        }
        target.localScale = Vector3.one;
        comboPunchRoutine = null;
    }

    void ShowRankAndBestScore()
    {
        RectTransform panel = finalScorePanel != null ? finalScorePanel.GetComponent<RectTransform>() : null;

        if (rankText == null && finalScoreText != null && panel != null)
        {
            rankText = CreateTextBelow(finalScoreText, panel, 0.8f, true);
        }
        if (bestScoreText == null && finalScoreText != null && rankText != null)
        {
            bestScoreText = CreateTextBelow(finalScoreText, rankText.rectTransform, 0.5f, true);
        }

        if (rankText != null)
        {
            rankText.gameObject.SetActive(true);
            rankText.text = GetRankTitle(score);
        }

        // No best score: Liminal keeps nothing between sessions (and doesn't allow saving), so there's nothing to beat
        if (bestScoreText != null) bestScoreText.gameObject.SetActive(false);
    }

    string GetRankTitle(int finalScore)
    {
        if (ranks == null || ranks.Length == 0) return "";

        string title = ranks[0].title;
        foreach (Rank rank in ranks)
        {
            if (finalScore >= rank.minimumScore)
            {
                title = rank.title;
            }
        }
        return title;
    }

    // Makes a copy of a text object and places it just below another UI element, so new
    // text can be shown without anyone having to edit the scene.
    TMP_Text CreateTextBelow(TMP_Text template, RectTransform below, float sizeScale, bool placeOutside)
    {
        TMP_Text copy = Instantiate(template, placeOutside ? below : below.parent);
        copy.name = template.name + " (auto)";
        foreach (Transform child in copy.transform)
        {
            Destroy(child.gameObject);
        }

        RectTransform rect = copy.rectTransform;
        float height = template.rectTransform.rect.height * sizeScale;

        if (placeOutside)
        {
            // Anchored to the bottom edge of the target, hanging just underneath it
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -height * 0.1f);
            rect.sizeDelta = new Vector2(Mathf.Max(below.rect.width, template.rectTransform.rect.width), height);
        }
        else
        {
            // Same anchors as the target, moved down by its height
            rect.anchorMin = below.anchorMin;
            rect.anchorMax = below.anchorMax;
            rect.pivot = below.pivot;
            rect.sizeDelta = new Vector2(below.sizeDelta.x, below.sizeDelta.y * sizeScale);
            rect.anchoredPosition = below.anchoredPosition - new Vector2(0, below.rect.height);
        }

        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        copy.fontSize = template.fontSize * sizeScale;
        copy.fontSizeMax = template.fontSizeMax * sizeScale;
        copy.alignment = TextAlignmentOptions.Center;
        copy.enableWordWrapping = false;
        copy.text = "";
        return copy;
    }

    void FadeOut()
    {
        var fader = ScreenFader.Instance;
        fader?.FadeToBlack(fadeDuration);
    }

    void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        ExperienceApp.End();
#if UNITY_STANDALONE
        // Our own PC test builds only: Liminal never allows Application.Quit (the Liminal package is an Android build)
        Application.Quit();
#endif
#endif
    }

    void UpdateScore(int change, Vector3 position, bool playSoundFX = true)
    {
        if (state != GameState.DuringGame) return;

        if (change > 0)
        {
            change = Mathf.FloorToInt(change * scoreMultiplier * comboLevel);
        }
        score = Mathf.Max(score + change, 0);
        scoreText.text = score.ToString();

        if (playSoundFX)
        {
            if (change > 0)
            {
                // The score sound climbs in pitch as the combo grows
                if (soundEffectSource != null)
                {
                    scoreUpSound.PlayOneShot(soundEffectSource, 1f + (comboLevel - 1) * comboPitchStep);
                }
            }
            else if (change < 0)
            {
                PlaySoundEffect(scoreDownSound);
            }
        }

        if (scoreParticlePool != null)
        {
            ScoreParticle scoreParticle = scoreParticlePool.GetObjectFromPool<ScoreParticle>(0);
            if (scoreParticle != null)
            {
                scoreParticle.gameObject.SetActive(true);
                scoreParticle.Initialise(change, position);

            }
        }
    }

    public void PlayCatHitSound(AudioSource source, float pitchMultiplier)
    {
        if (catHitSounds == null || catHitSounds.Length == 0 || source == null) return;
        catHitSounds[Random.Range(0, catHitSounds.Length)].PlayOneShot(source, pitchMultiplier * Random.Range(0.95f, 1.05f));
    }

    public void PlayCatLaunchSound(AudioSource source)
    {
        catLaunchSound.PlayOneShot(source, Random.Range(0.9f, 1.15f));
    }

    public void PlayCatPopSound(Vector3 position)
    {
        if (catPopSound.soundClip != null)
        {
            AudioSource.PlayClipAtPoint(catPopSound.soundClip, position, catPopSound.volume);
        }
    }

    public void CatSpawned()
    {
        catsInScene++;
    }

    public void CatDespawned()
    {
        catsInScene--;
    }

    public void FeedCat(int scoreValue, Vector3 position)
    {
        catsFed++;

        if (state == GameState.DuringGame)
        {
            comboCount++;
            comboTimer = comboWindow;
            int newLevel = Mathf.Min(1 + comboCount / catsPerComboLevel, maxComboLevel);
            bool levelledUp = newLevel > comboLevel;
            comboLevel = newLevel;
            ShowCombo(levelledUp);
            if (levelledUp)
            {
                Haptics.PulseBoth(Haptics.Strength.Strong);
            }
        }

        UpdateScore(scoreValue, position);
    }

    // Points for something other than feeding a cat (e.g. catching a flying prop). Combo and multiplier still apply.
    public void AddBonus(int scoreValue, Vector3 position)
    {
        UpdateScore(scoreValue, position);
    }

    public void ReceivePenalty(int penaltyValue, Vector3 position, bool playSoundFX)
    {
        penalties += penaltyValue;
        UpdateScore(-penaltyValue, position, playSoundFX);
    }

    public void EndRoundEarly()
    {
        endRoundEarly = true;
    }

    void PlaySoundEffect(SoundData sound)
    {
        if (soundEffectSource != null && sound.soundClip != null)
        {
            sound.PlayOneShot(soundEffectSource);
        }
    }

    void PlayMusic(SoundData music, float startPoint = 0, bool loop = true)
    {
        if (musicSource != null)
        {
            if (music.soundClip != null)
            {
                music.PlayMusic(musicSource, startPoint, loop);
                musicSource.volume *= SoundLevels.music;
            }
            else
            {
                StopMusic();
            }
        }
    }

    void BeginLoop()
    {
        PlayMusic(gameMusicLoop);
    }

    IEnumerator FadeOutMusic()
    {
        float timer = outroLength;
        float currentVolume = musicSource.volume;
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            musicSource.volume = currentVolume * timer / outroLength;
            yield return null;
        }
        musicSource.Stop();
    }

    void StopMusic()
    {
        musicSource?.Stop();
    }

    public DefendPoint GetRandomDefendPoint()
    {
        if (defendPoints == null || defendPoints.Count == 0) return null;
        return defendPoints[Random.Range(0, defendPoints.Count)];
    }

    public DefendPoint GetClosestDefendPoint(Vector3 position)
    {
        if (defendPoints == null || defendPoints.Count == 0) return null;

        DefendPoint output = null;
        float closestDistance = Mathf.Infinity;
        foreach (DefendPoint defendPoint in defendPoints)
        {
            float distance = Vector3.Distance(position, defendPoint.position);
            if (distance < closestDistance)
            {
                output = defendPoint;
                closestDistance = distance;
            }
        }

        return output;
    }

    public void ShieldAllDefendPoints(float duration)
    {
        foreach (DefendPoint dP in defendPoints)
        {
            if (dP != null)
            {
                dP.DeployShield(duration);
            }
        }
    }

    public void ChangeScoreMultiplier(float multiplier)
    {
        scoreMultiplier *= multiplier;

        if (scoreMultiplier != 1)
        {
            bonusDisplay.SetActive(true);
            bonusText.text = $"x{scoreMultiplier}";
        }
        else
        {
            bonusDisplay.SetActive(false);
        }
    }

    [System.Serializable]
    public struct Rank
    {
        public int minimumScore;
        public string title;

        public Rank(int minimumScore, string title)
        {
            this.minimumScore = minimumScore;
            this.title = title;
        }
    }

    public enum GameState
    {
        Initialising,
        PreGame,
        DuringGame,
        PostGame,
        FadeOut
    }
}
