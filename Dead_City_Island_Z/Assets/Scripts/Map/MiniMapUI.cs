using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 화면 우상단 미니맵 UI
/// 플레이어 중심 · 구역 색상 테두리 · 마커 아이콘
/// </summary>
public class MiniMapUI : MonoBehaviour
{
    [Header("미니맵 컴포넌트")]
    [SerializeField] private RawImage     mapImage;          // 미니맵 카메라 렌더텍스처
    [SerializeField] private RectTransform playerDot;        // 플레이어 위치 (항상 중앙)
    [SerializeField] private Image        borderImage;       // 외곽 테두리 (위험도 색상)
    [SerializeField] private TextMeshProUGUI zoneNameText;   // 현재 구역명
    [SerializeField] private TextMeshProUGUI dangerText;     // 위험도 텍스트

    [Header("마커 아이콘 풀")]
    [SerializeField] private GameObject   miniMarkerPrefab;
    [SerializeField] private RectTransform markerContainer;
    [SerializeField] private float         miniMapWorldRange = 20f;

    [Header("위험도 색상")]
    [SerializeField] private Color colorSafe    = new(0.2f, 0.9f, 0.2f);
    [SerializeField] private Color colorLow     = new(0.9f, 0.9f, 0.2f);
    [SerializeField] private Color colorMedium  = new(1f,   0.5f, 0f);
    [SerializeField] private Color colorHigh    = new(0.9f, 0.1f, 0.1f);
    [SerializeField] private Color colorExtreme = new(0.3f, 0f,   0.3f);

    private Dictionary<string, RectTransform> _miniMarkers = new();
    private Transform _playerTransform;
    private float     _mapSize;

    // ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        MapSystem.OnZoneEntered += OnZoneEntered;
        MapSystem.OnZoneExited  += OnZoneExited;
    }

    private void OnDisable()
    {
        MapSystem.OnZoneEntered -= OnZoneEntered;
        MapSystem.OnZoneExited  -= OnZoneExited;
    }

    private void Start()
    {
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) _playerTransform = pc.transform;

        if (mapImage != null)
            _mapSize = mapImage.rectTransform.rect.width;

        // 기본 상태 (안전지대)
        SetDangerColor(DangerLevel.Safe);
        UpdateZoneText("섬 (안전지대)", DangerLevel.Safe);
    }

    private void LateUpdate()
    {
        UpdateMarkerPositions();
    }

    // ─── 구역 변경 ───────────────────────────────────────────

    private void OnZoneEntered(ZoneData zone)
    {
        SetDangerColor(zone.dangerLevel);
        UpdateZoneText(zone.zoneNameKR, zone.dangerLevel);
        UIManager.Instance?.ShowNotification(
            $"구역 진입: {zone.zoneNameKR}  [{GetDangerLabel(zone.dangerLevel)}]");
    }

    private void OnZoneExited(ZoneData zone) { }

    private void SetDangerColor(DangerLevel level)
    {
        if (borderImage == null) return;
        borderImage.color = level switch
        {
            DangerLevel.Safe    => colorSafe,
            DangerLevel.Low     => colorLow,
            DangerLevel.Medium  => colorMedium,
            DangerLevel.High    => colorHigh,
            DangerLevel.Extreme => colorExtreme,
            _                   => Color.white
        };
    }

    private void UpdateZoneText(string zoneName, DangerLevel level)
    {
        if (zoneNameText != null) zoneNameText.text = zoneName;
        if (dangerText   != null)
        {
            dangerText.text  = GetDangerLabel(level);
            dangerText.color = borderImage?.color ?? Color.white;
        }
    }

    private string GetDangerLabel(DangerLevel level) => level switch
    {
        DangerLevel.Safe    => "🟢 안전",
        DangerLevel.Low     => "🟡 낮음",
        DangerLevel.Medium  => "🟠 보통",
        DangerLevel.High    => "🔴 위험",
        DangerLevel.Extreme => "⚫ 블랙존",
        _                   => "?"
    };

    // ─── 마커 ────────────────────────────────────────────────

    public void AddMiniMarker(string id, Sprite icon, Color color)
    {
        if (_miniMarkers.ContainsKey(id)) return;
        if (miniMarkerPrefab == null || markerContainer == null) return;

        var go  = Instantiate(miniMarkerPrefab, markerContainer);
        var img = go.GetComponentInChildren<Image>();
        if (img != null) { img.sprite = icon; img.color = color; }

        _miniMarkers[id] = go.GetComponent<RectTransform>();
    }

    public void RemoveMiniMarker(string id)
    {
        if (_miniMarkers.TryGetValue(id, out var rt))
        {
            Destroy(rt.gameObject);
            _miniMarkers.Remove(id);
        }
    }

    private void UpdateMarkerPositions()
    {
        if (_playerTransform == null || markerContainer == null) return;

        var mapSys = MapSystem.Instance;
        if (mapSys == null) return;

        // TODO: MapSystem의 마커 리스트와 동기화
        // 현재는 플레이어 도트만 항상 중앙에 고정
        if (playerDot != null)
            playerDot.anchoredPosition = Vector2.zero;
    }
}
