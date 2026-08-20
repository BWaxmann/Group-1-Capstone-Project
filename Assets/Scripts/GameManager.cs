using Liminal.Core.Fader;
using Liminal.SDK.Core;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
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
    SpawnManager[] catSpawners;

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
    AudioClip countdownSound;

    [SerializeField]
    [Range(0f, 1f)]
    float countdownVolume = 1f;

    [SerializeField]
    AudioClip gameStartSound;

    [SerializeField]
    [Range(0f, 1f)]
    float gameStartVolume = 1f;

    [SerializeField]
    AudioClip scoreUpSound;

    [SerializeField]
    [Range(0f, 1f)]
    float scoreUpVolume = 1f;

    [SerializeField]
    AudioClip catEscapeSound;

    [SerializeField]
    [Range(0f, 1f)]
    float catEscapeVolume = 1f;

    [SerializeField]
    AudioClip gameEndSound;

    [SerializeField]
    [Range(0f, 1f)]
    float gameEndVolume = 1f;

    [SerializeField]
    AudioClip gameMusic;

    [SerializeField]
    [Range(0f, 1f)]
    float gameMusicVolume = 1f;

    [SerializeField]
    AudioClip postgameMusic;

    [SerializeField]
    [Range(0f, 1f)]
    float postgameMusicVolume = 1f;

    public static GameManager instance;

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

        if (catSpawners == null || catSpawners.Length == 0)
        {
            ExperienceApp.Initializing += FindCatSpawners;
        }
    }

    void Start()
    {
        // TODO: Do fade-in before initiating pre-game countdown
        ShowGameCanvases(false);
        postgameCanvas.SetActive(false);

        InitiatePregame();
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
                        PlaySoundEffect(countdownSound, countdownVolume);
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

    public void FindCatSpawners()
    {
        catSpawners = FindObjectsOfType<SpawnManager>();
    }

    void InitiatePregame()
    {
        state = GameState.PreGame;
        timer = pregameCountdownTime;
        pregameCanvas.SetActive(true);
    }

    void InitiateGame()
    {
        PlaySoundEffect(gameStartSound, gameStartVolume);
        PlayMusic(gameMusic, gameMusicVolume);
        state = GameState.DuringGame;
        timer = gameTime;
        score = 0;
        Invoke(nameof(HidePregameCanvas), countdownDisappearDelay);

        ShowGameCanvases(true);
        scoreText.text = score.ToString();

        foreach (SpawnManager spawner in catSpawners)
        {
            spawner.StartSpawningWaves();
        }
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
        PlaySoundEffect(gameEndSound, gameEndVolume);
        PlayMusic(postgameMusic, postgameMusicVolume);
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
            PlaySoundEffect(scoreUpSound, scoreUpVolume);
        }
        else if (change < 0)
        {
            PlaySoundEffect(catEscapeSound, catEscapeVolume);
        }
    }

    void PlaySoundEffect(AudioClip sound, float volume)
    {
        if (soundEffectSource != null && sound != null)
        {
            soundEffectSource.PlayOneShot(sound, volume);
        }
    }

    void PlayMusic(AudioClip music, float volume)
    {
        if (musicSource != null && music != null)
        {
            musicSource.clip = music;
            musicSource.volume = volume;
            musicSource.Play();
        }
    }

    void StopMusic()
    {
        musicSource?.Stop();
    }

    public enum GameState
    {
        PreGame,
        DuringGame,
        PostGame
    }
}
