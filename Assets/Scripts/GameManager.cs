using System.Collections;
using System.Collections.Generic;
using Liminal.Core.Fader;
using Liminal.SDK.Core; // This is needed for ExperienceApp.End()
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField]
    [Min(0)]
    float pregameDelay = 0.5f;

    [SerializeField]
    [Min(0)]
    int pregameCountdownTime = 3;

    [SerializeField]
    [Min(0)]
    float gameTime = 200f;

    [SerializeField]
    [Min(0)]
    float fadeDuration = 1f;

    [Header("UI")]

    [SerializeField]
    GameObject pregameCanvas;

    [SerializeField]
    [TextArea]
    [Tooltip("Optional. Shown on the countdown canvas before the round, until the countdown starts.")]
    string startupMessage = "";

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
    ObjectSpawner scoreParticlePool;

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
    float outroLength = 10f;

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

    [SerializeField]
    string newBestText = "NEW BEST SCORE!";

    [SerializeField]
    string bestScoreFormat = "Best: {0}";

    const string BestScoreKey = "CatastropheCafe_BestScore";

    public static GameManager instance;

    public delegate void GameStateChange();
    public event GameStateChange StartCountdown;
    public event GameStateChange GameStart;
    public event GameStateChange FinaleStart;
    public event GameStateChange GameEnd;

    bool playerInputsEnabled;
    int catsFed;
    int penalties;
    int score;
    float scoreMultiplier;
    GameState state;
    List<DefendPoint> defendPoints = new List<DefendPoint>();

    int comboCount;
    int comboLevel = 1;
    float comboTimer;
    Coroutine comboPunchRoutine;
    bool finaleStarted;

    public bool enablePlayerInput => playerInputsEnabled;

    void Awake()
    {
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
            countdownText.text = startupMessage;
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

        if (Mathf.Floor(gameTime) < gameTime)
        {
            timerText.text = Mathf.CeilToInt(gameTime).ToString();
            yield return new WaitForSeconds(gameTime % 1);
        }

        int steps = Mathf.FloorToInt(gameTime);
        for (int i = 0; i < steps; i++)
        {
            timerText.text = (steps - i).ToString();

            if (!finaleStarted && finaleDuration > 0 && steps - i <= finaleDuration)
            {
                StartFinale();
            }

            yield return wait;
        }

        timerText.text = "0";
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

    void InitiatePregame()
    {
        state = GameState.PreGame;
        pregameCanvas.SetActive(true);
        if (countdownSoundIsCombined) PlaySoundEffect(countdownSound);

        StartCoroutine(PreGameRoutine());
    }

    void InitiateGame()
    {
        if (separateGameStartSound) PlaySoundEffect(gameStartSound);
        PlayMusic(gameMusicIntroAndOutro);
        Invoke(nameof(BeginLoop), introLength);
        //Invoke(nameof(PlayOutro), gameTime - outroLength);
        StartCoroutine(FadeOutMusic());
        
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
        ShowBanner(finaleBannerText, finaleBannerDuration);

        PlaySoundEffect(finaleSound);
        Haptics.PulseBoth(Haptics.Strength.Strong);
        StartCoroutine(RampMusicPitch(finaleMusicPitch, 2f));

        FinaleStart?.Invoke();
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

        int best = PlayerPrefs.GetInt(BestScoreKey, 0);
        bool newBest = score > best;
        if (newBest)
        {
            best = score;
            PlayerPrefs.SetInt(BestScoreKey, best);
            PlayerPrefs.Save();
        }

        if (bestScoreText != null)
        {
            bestScoreText.gameObject.SetActive(true);
            bestScoreText.text = newBest ? newBestText : string.Format(bestScoreFormat, best);
        }
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
        Application.Quit(); // Delete this for the Liminal build
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

    void PlayOutro()
    {
        PlayMusic(gameMusicIntroAndOutro, gameMusicIntroAndOutro.soundClip.length - outroLength, false);
    }

    IEnumerator FadeOutMusic()
    {
        yield return new WaitForSeconds(gameTime - outroLength);

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
