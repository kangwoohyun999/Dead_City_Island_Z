using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 날씨 HUD — 현재 날씨 아이콘 + 기온 + 체온 경고
/// 화면 우상단 미니맵 아래에 표시
/// </summary>
public class WeatherUI : MonoBehaviour
{
    [Header("날씨 표시")]
    [SerializeField] private TextMeshProUGUI weatherIcon;       // 이모지 아이콘
    [SerializeField] private TextMeshProUGUI weatherLabel;      // 날씨 이름 텍스트
    [SerializeField] private TextMeshProUGUI temperatureText;   // 기온 (°C)
    [SerializeField] private Slider          visionBar;         // 시야 범위 바

    [Header("체온 경고")]
    [SerializeField] private GameObject      coldWarning;       // 저체온 경고 패널
    [SerializeField] private GameObject      hotWarning;        // 고체온 경고 패널
    [SerializeField] private TextMeshProUGUI bodyTempText;      // 플레이어 체온

    [Header("날씨 전환 오버레이")]
    [SerializeField] private Image           rainOverlay;       // 빗줄기 화면 효과
    [SerializeField] private Image           fogOverlay;        // 안개 화면 효과
    [SerializeField] private Image           snowOverlay;       // 눈 화면 효과

    [Header("전환 속도")]
    [SerializeField] private float           overlayFadeSpeed = 1.5f;

    // ─── 목표 오버레이 알파 ──────────────────────────────────
    private float _targetRainAlpha;
    private float _targetFogAlpha;
    private float _targetSnowAlpha;

    // ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        WeatherSystem.OnWeatherChanged      += OnWeatherChanged;
        WeatherSystem.OnWeatherTransitioning += OnWeatherTransitioning;
        WeatherSystem.OnWeatherEffectChanged += OnEffectChanged;
        SurvivalStats.OnTemperatureChanged  += OnTemperatureChanged;
    }

    private void OnDisable()
    {
        WeatherSystem.OnWeatherChanged      -= OnWeatherChanged;
        WeatherSystem.OnWeatherTransitioning -= OnWeatherTransitioning;
        WeatherSystem.OnWeatherEffectChanged -= OnEffectChanged;
        SurvivalStats.OnTemperatureChanged  -= OnTemperatureChanged;
    }

    private void Update()
    {
        UpdateOverlayAlpha(rainOverlay, ref _targetRainAlpha);
        UpdateOverlayAlpha(fogOverlay,  ref _targetFogAlpha);
        UpdateOverlayAlpha(snowOverlay, ref _targetSnowAlpha);
    }

    // ─── 날씨 변경 ───────────────────────────────────────────

    private void OnWeatherChanged(WeatherType type)
    {
        // 아이콘
        if (weatherIcon != null)
            weatherIcon.text = type switch
            {
                WeatherType.Clear     => "☀️",
                WeatherType.Cloudy    => "⛅",
                WeatherType.Rain      => "🌧️",
                WeatherType.HeavyRain => "⛈️",
                WeatherType.Snow      => "❄️",
                WeatherType.Fog       => "🌫️",
                _                     => "❓"
            };

        if (weatherLabel != null)
            weatherLabel.text = type.ToKorean();

        // 오버레이 목표 알파
        _targetRainAlpha = (type == WeatherType.Rain)      ? 0.18f
                         : (type == WeatherType.HeavyRain) ? 0.35f : 0f;
        _targetFogAlpha  = (type == WeatherType.Fog)       ? 0.45f : 0f;
        _targetSnowAlpha = (type == WeatherType.Snow)      ? 0.15f : 0f;
    }

    private void OnWeatherTransitioning(WeatherType nextType, float t)
    {
        // 전환 중 레이블 깜빡임
        if (weatherLabel != null)
            weatherLabel.color = new Color(1, 1, 1, 0.5f + Mathf.Sin(Time.time * 4f) * 0.3f);
    }

    private void OnEffectChanged(WeatherEffectData effect)
    {
        // 기온
        if (temperatureText != null)
            temperatureText.text = $"{effect.ambientTemperature:F0}°C";

        // 시야 바
        if (visionBar != null)
            visionBar.value = effect.visionRange;

        // 레이블 색 정상화
        if (weatherLabel != null)
            weatherLabel.color = Color.white;
    }

    // ─── 체온 경고 ───────────────────────────────────────────

    private void OnTemperatureChanged(float bodyTemp, float normalTemp)
    {
        if (bodyTempText != null)
            bodyTempText.text = $"체온 {bodyTemp:F1}°C";

        bool isCold = bodyTemp < 33f;
        bool isHot  = bodyTemp > 40f;

        coldWarning?.SetActive(isCold);
        hotWarning? .SetActive(isHot);

        // 체온 텍스트 색상
        if (bodyTempText != null)
            bodyTempText.color = isCold ? new Color(0.4f, 0.7f, 1f)
                               : isHot  ? new Color(1f, 0.4f, 0.2f)
                                        : Color.white;
    }

    // ─── 오버레이 페이드 ─────────────────────────────────────

    private void UpdateOverlayAlpha(Image overlay, ref float target)
    {
        if (overlay == null) return;
        Color c = overlay.color;
        c.a         = Mathf.MoveTowards(c.a, target, overlayFadeSpeed * Time.deltaTime);
        overlay.color = c;
        overlay.gameObject.SetActive(c.a > 0.01f);
    }
}
