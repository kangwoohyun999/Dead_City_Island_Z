using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 보스 전용 HUD — 화면 상단 큰 체력바 + 보스 이름 + 페이즈 아이콘
/// </summary>
public class BossHUDManager : MonoBehaviour
{
    public static BossHUDManager Instance { get; private set; }

    [Header("패널")]
    [SerializeField] private GameObject      bossHUDPanel;

    [Header("체력바")]
    [SerializeField] private Slider          bossHealthBar;
    [SerializeField] private Image           bossHealthFill;
    [SerializeField] private Image           bossHealthLag;     // 지연 체력바 (흰색)
    [SerializeField] private float           lagSpeed = 1.5f;

    [Header("텍스트")]
    [SerializeField] private TextMeshProUGUI bossNameText;
    [SerializeField] private TextMeshProUGUI bossHealthText;    // "850 / 1000"
    [SerializeField] private TextMeshProUGUI phaseText;         // "Phase 2"

    [Header("페이즈 아이콘 (3개)")]
    [SerializeField] private Image[]         phaseIcons;        // 3개
    [SerializeField] private Color           phaseActiveColor   = Color.white;
    [SerializeField] private Color           phaseInactiveColor = new Color(0.3f, 0.3f, 0.3f);

    [Header("등장/퇴장 애니메이션")]
    [SerializeField] private float           slideInDuration  = 0.5f;
    [SerializeField] private float           slideOutDuration = 0.4f;
    [SerializeField] private RectTransform   hudRect;

    // ─── 내부 상태 ───────────────────────────────────────────
    private float _targetHealthRatio;
    private float _lagRatio;
    private float _maxHealth;
    private Coroutine _slideCoroutine;

    // ─── 단계별 체력바 색상 ──────────────────────────────────
    private static readonly Color Phase1Color = new(0.2f, 0.85f, 0.3f);   // 초록
    private static readonly Color Phase2Color = new(1.0f, 0.65f, 0.1f);   // 주황
    private static readonly Color Phase3Color = new(0.9f, 0.15f, 0.15f);  // 빨강

    // ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        bossHUDPanel?.SetActive(false);
    }

    private void Update()
    {
        // 지연 체력바 스무딩
        if (bossHealthLag == null) return;
        _lagRatio = Mathf.MoveTowards(_lagRatio, _targetHealthRatio, lagSpeed * Time.deltaTime);
        bossHealthLag.fillAmount = _lagRatio;
    }

    // ─── 보스 HUD 표시 ───────────────────────────────────────

    public void ShowBossHUD(BossAI boss)
    {
        bossHUDPanel?.SetActive(true);
        _maxHealth        = boss.MaxHealth;  // BossAI에 MaxHealth 프로퍼티 필요
        _targetHealthRatio = 1f;
        _lagRatio          = 1f;

        if (bossNameText)    bossNameText.text = boss.BossNameKR;
        if (bossHealthFill)  bossHealthFill.color = Phase1Color;
        if (phaseText)       phaseText.text = "Phase 1";

        UpdatePhaseIcons(1);
        SetHealthBar(1f, boss.MaxHealth, boss.MaxHealth);

        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _slideCoroutine = StartCoroutine(SlideIn());
    }

    public void HideBossHUD()
    {
        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _slideCoroutine = StartCoroutine(SlideOut());
    }

    // ─── 체력 갱신 ───────────────────────────────────────────

    public void UpdateHealth(float current, float max)
    {
        _maxHealth         = max;
        _targetHealthRatio = current / max;

        if (bossHealthBar) bossHealthBar.value = _targetHealthRatio;
        SetHealthBar(_targetHealthRatio, current, max);
    }

    private void SetHealthBar(float ratio, float current, float max)
    {
        if (bossHealthBar)  bossHealthBar.value = ratio;
        if (bossHealthText) bossHealthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    // ─── 페이즈 변경 ─────────────────────────────────────────

    public void OnPhaseChanged(int newPhase)
    {
        if (phaseText) phaseText.text = $"Phase {newPhase}";
        UpdatePhaseIcons(newPhase);

        if (bossHealthFill != null)
        {
            bossHealthFill.color = newPhase switch
            {
                2 => Phase2Color,
                3 => Phase3Color,
                _ => Phase1Color
            };
        }

        // 페이즈 전환 펄스 애니메이션
        StartCoroutine(PulseHealthBar());
    }

    private void UpdatePhaseIcons(int currentPhase)
    {
        if (phaseIcons == null) return;
        for (int i = 0; i < phaseIcons.Length; i++)
        {
            if (phaseIcons[i] == null) continue;
            phaseIcons[i].color = (i + 1 <= currentPhase) ? phaseActiveColor : phaseInactiveColor;
        }
    }

    // ─── 애니메이션 ──────────────────────────────────────────

    private IEnumerator SlideIn()
    {
        if (hudRect == null) yield break;

        Vector2 offscreen  = new Vector2(hudRect.anchoredPosition.x, 100f);
        Vector2 onscreen   = new Vector2(hudRect.anchoredPosition.x, 0f);
        float elapsed = 0f;

        while (elapsed < slideInDuration)
        {
            elapsed += Time.deltaTime;
            hudRect.anchoredPosition = Vector2.Lerp(offscreen, onscreen,
                Mathf.SmoothStep(0f, 1f, elapsed / slideInDuration));
            yield return null;
        }
        hudRect.anchoredPosition = onscreen;
    }

    private IEnumerator SlideOut()
    {
        if (hudRect == null) { bossHUDPanel?.SetActive(false); yield break; }

        Vector2 start = hudRect.anchoredPosition;
        Vector2 end   = new Vector2(start.x, 100f);
        float elapsed = 0f;

        while (elapsed < slideOutDuration)
        {
            elapsed += Time.deltaTime;
            hudRect.anchoredPosition = Vector2.Lerp(start, end, elapsed / slideOutDuration);
            yield return null;
        }

        bossHUDPanel?.SetActive(false);
    }

    private IEnumerator PulseHealthBar()
    {
        if (bossHealthBar == null) yield break;
        var fillImg = bossHealthBar.fillRect?.GetComponent<Image>();
        if (fillImg == null) yield break;

        Color original = fillImg.color;
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            fillImg.color = Color.Lerp(Color.white, original, t / 0.4f);
            yield return null;
        }
        fillImg.color = original;
    }

    // BossAI에서 MaxHealth 프로퍼티 접근 필요 — BossAI.cs에 추가
    // public float MaxHealth => maxHealth;
}

// BossAI에 MaxHealth 프로퍼티 추가를 위한 partial 선언 불가 (단일 파일)
// → BossAI.cs의 BossAI 클래스에 아래 프로퍼티 수동 추가 필요:
// public float MaxHealth => maxHealth;
