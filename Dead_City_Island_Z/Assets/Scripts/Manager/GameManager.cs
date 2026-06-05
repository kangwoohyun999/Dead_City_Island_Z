using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 전체 상태를 관리하는 싱글턴 매니저
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("게임 상태")]
    public GameState CurrentState { get; private set; } = GameState.MainMenu;

    [Header("씬 이름")]
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private string islandScene   = "Island";    // 섬 (안전지대)
    [SerializeField] private string cityScene     = "City";      // 도시 (위험지대)

    [Header("게임 설정")]
    [SerializeField] private float dayLengthSeconds = 1200f;     // 낮 길이 (20분)
    [SerializeField] private float nightLengthSeconds = 600f;    // 밤 길이 (10분)

    // ─── 시간 시스템 ───────────────────────────────────────
    public float CurrentDayTime  { get; private set; } = 0f;    // 0~1 (0=새벽, 0.5=정오, 1=자정)
    public int   DayCount        { get; private set; } = 1;
    public bool  IsDay           => CurrentDayTime is >= 0.25f and <= 0.75f;

    // ─── 이벤트 ────────────────────────────────────────────
    public static event Action<GameState> OnGameStateChanged;
    public static event Action<int>       OnDayChanged;
    public static event Action<bool>      OnDayNightCycleChanged;   // true = 낮

    private bool _wasDay = true;

    // ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Application.targetFrameRate = 60;
        ChangeState(GameState.MainMenu);
    }

    private void Update()
    {
        if (CurrentState == GameState.Playing)
            UpdateDayNightCycle();
    }

    // ─── 게임 상태 변경 ─────────────────────────────────────
    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState);

        switch (newState)
        {
            case GameState.MainMenu:
                SceneManager.LoadScene(mainMenuScene);
                break;
            case GameState.Playing:
                // 씬은 별도 로딩 (LoadIsland / LoadCity 에서 처리)
                break;
            case GameState.Paused:
                Time.timeScale = 0f;
                break;
            case GameState.GameOver:
                Time.timeScale = 1f;
                // TODO: 게임 오버 UI 표시
                break;
        }
    }

    public void LoadIsland()
    {
        Time.timeScale = 1f;
        ChangeState(GameState.Playing);
        SceneManager.LoadScene(islandScene);
    }

    public void LoadCity()
    {
        Time.timeScale = 1f;
        ChangeState(GameState.Playing);
        SceneManager.LoadScene(cityScene);
    }

    public void PauseGame()
    {
        if (CurrentState == GameState.Playing)
            ChangeState(GameState.Paused);
    }

    public void ResumeGame()
    {
        if (CurrentState == GameState.Paused)
        {
            CurrentState = GameState.Playing;
            Time.timeScale = 1f;
            OnGameStateChanged?.Invoke(GameState.Playing);
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ─── 낮/밤 사이클 ───────────────────────────────────────
    private void UpdateDayNightCycle()
    {
        float totalCycleLength = dayLengthSeconds + nightLengthSeconds;
        CurrentDayTime += Time.deltaTime / totalCycleLength;

        if (CurrentDayTime >= 1f)
        {
            CurrentDayTime = 0f;
            DayCount++;
            OnDayChanged?.Invoke(DayCount);
        }

        bool isCurrentlyDay = IsDay;
        if (isCurrentlyDay != _wasDay)
        {
            _wasDay = isCurrentlyDay;
            OnDayNightCycleChanged?.Invoke(isCurrentlyDay);
        }
    }

    /// <summary>현재 시각을 "HH:MM" 형식 문자열로 반환</summary>
    public string GetFormattedTime()
    {
        int totalMinutes = Mathf.FloorToInt(CurrentDayTime * 24 * 60);
        int hours   = totalMinutes / 60;
        int minutes = totalMinutes % 60;
        return $"{hours:D2}:{minutes:D2}";
    }
}

public enum GameState
{
    MainMenu,
    Playing,
    Paused,
    GameOver
}
