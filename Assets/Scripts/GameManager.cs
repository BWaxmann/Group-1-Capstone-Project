using Liminal.SDK.Core;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] 
    int pregameCountdownTime = 3;

    [SerializeField] 
    float gameTime = 200f;

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
    float countdownDisappearDelay = 1f;

    [SerializeField]
    GameObject[] gameCanvases;

    [SerializeField]
    TMP_Text scoreText;

    [SerializeField]
    TMP_Text timerText;

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
                    countdownText.text = Mathf.CeilToInt(timer).ToString();
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
                // TODO: End game
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
        // TODO: Lock player input
        pregameCanvas.SetActive(true);
    }

    void InitiateGame()
    {
        state = GameState.DuringGame;
        timer = gameTime;
        score = 0;
        // TODO: Unlock player input
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
        state = GameState.PostGame;
        // TODO: Lock player input
        // TODO: Bring up post-game UI
    }

    public void UpdateScore(int change)
    {
        score = Mathf.Max(score + change, 0);
        scoreText.text = score.ToString();
    }

    public enum GameState
    {
        PreGame,
        DuringGame,
        PostGame
    }
}
