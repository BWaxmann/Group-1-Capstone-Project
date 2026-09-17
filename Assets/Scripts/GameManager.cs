using System.Collections;
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

    [SerializeField]
    DefendPoint[] defendPoints;

    [Header("UI")]

    [SerializeField]
    GameObject pregameCanvas;

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

    public static GameManager instance;

    public delegate void GameStateChange();
    public event GameStateChange StartCountdown;
    public event GameStateChange GameStart;
    public event GameStateChange GameEnd;

    bool playerInputsEnabled;
    int catsFed;
    int penalties;
    int score;
    float scoreMultiplier;
    GameState state;

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

        state = GameState.Initialising;
        playerInputsEnabled = true;
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
        Invoke(nameof(PlayOutro), gameTime - outroLength);
        
        state = GameState.DuringGame;
        playerInputsEnabled = true;
        score = 0;
        scoreMultiplier = 1;
        bonusDisplay.SetActive(false);
        Invoke(nameof(HidePregameCanvas), countdownDisappearDelay);

        ShowGameCanvases(true);
        scoreText.text = "0";

        StartCoroutine(GameRoutine());
        GameStart?.Invoke();
    }

    void HidePregameCanvas()
    {
        pregameCanvas.SetActive(false);
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

        StartCoroutine(PostGameRoutine());
        GameEnd?.Invoke();
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
#endif
    }

    void UpdateScore(int change, Vector3 position, bool playSoundFX = true)
    {
        if (state != GameState.DuringGame) return;

        if (change > 0)
        {
            change = Mathf.FloorToInt(change * scoreMultiplier);
        }
        score = Mathf.Max(score + change, 0);
        scoreText.text = score.ToString();

        if (playSoundFX)
        {
            if (change > 0)
            {
                PlaySoundEffect(scoreUpSound);
            }
            else if (change < 0)
            {
                PlaySoundEffect(scoreDownSound);
            }
        }

        if (scoreParticlePool != null)
        {
            ScoreParticle scoreParticle = scoreParticlePool.GetObjectFromPool<ScoreParticle>(0);
            scoreParticle.gameObject.SetActive(true);
            scoreParticle.Initialise(change, position);
        }
    }

    public void FeedCat(int scoreValue, Vector3 position)
    {
        catsFed++;
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

    void StopMusic()
    {
        musicSource?.Stop();
    }

    public DefendPoint GetRandomDefendPoint()
    {
        if (defendPoints == null || defendPoints.Length == 0) return null;
        return defendPoints[Random.Range(0, defendPoints.Length)];
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

    public enum GameState
    {
        Initialising,
        PreGame,
        DuringGame,
        PostGame,
        FadeOut
    }
}
