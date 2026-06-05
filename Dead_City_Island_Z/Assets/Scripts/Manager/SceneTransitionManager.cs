using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 섬 ↔ 도시 씬 전환 매니저
/// 로딩 화면 + 자동 저장 + 전환 연출
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("로딩 화면")]
    [SerializeField] private GameObject     loadingPanel;
    [SerializeField] private Slider         loadingBar;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI tipText;
    [SerializeField] private Image          fadeImage;

    [Header("씬 이름")]
    [SerializeField] private string islandSceneName = "Island";
    [SerializeField] private string citySceneName   = "City";

    [Header("전환 설정")]
    [SerializeField] private float fadeDuration   = 0.5f;
    [SerializeField] private bool  autoSaveOnExit = true;

    private static readonly string[] LOADING_TIPS =
    {
        "💡 도시는 낮보다 밤이 훨씬 위험합니다. 낮에 탐색하세요.",
        "💡 배고픔과 갈증이 낮으면 스태미나 회복이 느려집니다.",
        "💡 출혈 상태를 방치하면 체력이 계속 감소합니다. 붕대를 사용하세요.",
        "💡 스킬은 행동을 반복할수록 자동으로 성장합니다.",
        "💡 밤에는 좀비 스폰이 2배가 됩니다. 조심하세요.",
        "💡 농작물에 매일 물을 주지 않으면 시들어버립니다.",
        "💡 블랙존에는 최고급 장비가 있지만, 극도로 위험합니다.",
        "💡 제작 스킬 레벨이 높을수록 같은 재료로 더 많이 제작됩니다.",
        "💡 도시에서 설계도를 찾으면 새 레시피가 잠금 해제됩니다.",
        "💡 체온이 낮거나 높으면 지속 데미지를 받습니다. 적절한 방어구를 착용하세요."
    };

    private bool _isTransitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (loadingPanel) loadingPanel.SetActive(false);
    }

    // ─── 씬 전환 공개 API ────────────────────────────────────

    public void GoToCity(int saveSlot = 0)
    {
        if (_isTransitioning) return;
        StartCoroutine(TransitionRoutine(citySceneName, saveSlot));
    }

    public void GoToIsland(int saveSlot = 0)
    {
        if (_isTransitioning) return;
        StartCoroutine(TransitionRoutine(islandSceneName, saveSlot));
    }

    public void GoToMainMenu()
    {
        if (_isTransitioning) return;
        StartCoroutine(TransitionRoutine("MainMenu", -1));
    }

    // ─── 전환 루틴 ───────────────────────────────────────────

    private IEnumerator TransitionRoutine(string targetScene, int saveSlot)
    {
        _isTransitioning = true;

        // 1. 자동 저장
        if (autoSaveOnExit && saveSlot >= 0)
        {
            SaveSystem.Instance?.Save(saveSlot);
            yield return new WaitForSeconds(0.3f);
        }

        // 2. 페이드 아웃
        yield return FadeOut();

        // 3. 로딩 화면 표시
        ShowLoadingScreen();

        // 4. 비동기 씬 로드
        var op = SceneManager.LoadSceneAsync(targetScene);
        op.allowSceneActivation = false;

        float fakeProgress = 0f;
        while (!op.isDone)
        {
            // op.progress는 0~0.9 (0.9에서 완료 대기)
            float realProgress = op.progress / 0.9f;
            fakeProgress = Mathf.MoveTowards(fakeProgress, realProgress, Time.deltaTime * 0.8f);

            if (loadingBar)  loadingBar.value = fakeProgress;
            if (loadingText) loadingText.text = $"로딩 중... {Mathf.RoundToInt(fakeProgress * 100)}%";

            // 95% 이상이면 씬 활성화
            if (fakeProgress >= 0.95f)
            {
                if (loadingBar)  loadingBar.value = 1f;
                if (loadingText) loadingText.text = "완료!";
                yield return new WaitForSeconds(0.3f);
                op.allowSceneActivation = true;
            }

            yield return null;
        }

        // 5. 로딩 화면 숨김 + 페이드 인
        HideLoadingScreen();
        yield return FadeIn();

        _isTransitioning = false;
    }

    // ─── 로딩 화면 ───────────────────────────────────────────

    private void ShowLoadingScreen()
    {
        if (loadingPanel) loadingPanel.SetActive(true);
        if (loadingBar)   loadingBar.value = 0f;

        // 랜덤 팁
        if (tipText)
            tipText.text = LOADING_TIPS[UnityEngine.Random.Range(0, LOADING_TIPS.Length)];
    }

    private void HideLoadingScreen()
    {
        if (loadingPanel) loadingPanel.SetActive(false);
    }

    // ─── 페이드 ──────────────────────────────────────────────

    private IEnumerator FadeOut()
    {
        if (fadeImage == null) yield break;
        fadeImage.gameObject.SetActive(true);
        float t = 0;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeImage.color = new Color(0, 0, 0, Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        fadeImage.color = Color.black;
    }

    private IEnumerator FadeIn()
    {
        if (fadeImage == null) yield break;
        float t = fadeDuration;
        while (t > 0)
        {
            t -= Time.deltaTime;
            fadeImage.color = new Color(0, 0, 0, Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, 0);
        fadeImage.gameObject.SetActive(false);
    }
}
