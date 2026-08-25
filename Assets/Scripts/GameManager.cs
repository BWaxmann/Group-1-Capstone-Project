using Liminal.Core.Fader;
using Liminal.SDK.Core;
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
    float endgameFadeDelay = 3f;

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
    TMP_Text timerText;

    [SerializeField]
    GameObject postgameCanvas;

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
    SoundData catEscapeSound = new SoundData(1f, 1f);

    [SerializeField]
    SoundData gameEndSound = new SoundData(1f, 1f);

    [SerializeField]
    SoundData gameMusic = new SoundData(1f, 1f);

    [SerializeField]
    SoundData postgameMusic = new SoundData(1f, 1f);

    public static GameManager instance;

    public delegate void GameStateChange();
    public event GameStateChange GameStart;

    float timer;
    int score;
    GameState state;

    public bool enablePlayerInput => state == GameState.DuringGame;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != null)
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
        Invoke(nameof(InitiatePregame), pregameDelay);
    }

    void Update()
    {
        switch (state)
        {
            case GameState.PreGame:
                timer -= Time.deltaTime;
                if (timer <= 0)
                {
                    countdownText.text = countdownEndText;
                    InitiateGame();
                }
                else
                {
                    string t = Mathf.CeilToInt(timer).ToString();
                    if (countdownText.text != t)
                    {
                        countdownText.text = t;
                        if (!countdownSoundIsCombined) PlaySoundEffect(countdownSound);
                    }
                }
                break;
            case GameState.DuringGame:
                timer -= Time.deltaTime;
                if (timer <= 0)
                {
                    timerText.text = 0.ToString();
                    EndGame();
                }
                else
                {
                    timerText.text = Mathf.CeilToInt(timer).ToString();
                }
                break;
            case GameState.PostGame:
                timer -= Time.deltaTime;
                if (timer <= 0)
                {
                    FadeOut();
                    Invoke(nameof(EndGame), fadeDuration);
                }
                break;
        }
    }

    void InitiatePregame()
    {
        state = GameState.PreGame;
        timer = pregameCountdownTime;
        pregameCanvas.SetActive(true);
        if (countdownSoundIsCombined) PlaySoundEffect(countdownSound);
    }

    void InitiateGame()
    {
        if (separateGameStartSound) PlaySoundEffect(gameStartSound);
        PlayMusic(gameMusic);
        state = GameState.DuringGame;
        timer = gameTime;
        score = 0;
        Invoke(nameof(HidePregameCanvas), countdownDisappearDelay);

        ShowGameCanvases(true);
        scoreText.text = score.ToString();

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
        PlayMusic(postgameMusic);
        state = GameState.PostGame;
        postgameCanvas.SetActive(true);
        timer = endgameFadeDelay;
    }

    void FadeOut()
    {
        var fader = ScreenFader.Instance;
        fader?.FadeToBlack(fadeDuration);
    }

    void ExitGame()
    {
        ExperienceApp.End();
    }

    public void UpdateScore(int change)
    {
        score = Mathf.Max(score + change, 0);
        scoreText.text = score.ToString();
        if (change > 0)
        {
            PlaySoundEffect(scoreUpSound);
        }
        else if (change < 0)
        {
            PlaySoundEffect(catEscapeSound);
        }
    }

    void PlaySoundEffect(SoundData sound)
    {
        if (soundEffectSource != null && sound.soundClip != null)
        {
            sound.PlayOneShot(soundEffectSource);
        }
    }

    void PlayMusic(SoundData music)
    {
        if (musicSource != null)
        {
            if (music.soundClip != null)
            {
                music.PlayMusic(musicSource);
            }
            else
            {
                StopMusic();
            }
        }
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

    public enum GameState
    {
        Initialising,
        PreGame,
        DuringGame,
        PostGame
    }
}
