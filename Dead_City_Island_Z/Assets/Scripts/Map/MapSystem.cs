using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>맵 시스템 — XZ 평면 기준 구역 감지 (Z가 2D의 Y 역할)</summary>
public class MapSystem : MonoBehaviour
{
    public static MapSystem Instance { get; private set; }

    [SerializeField] private Camera miniMapCamera;
    [SerializeField] private List<ZoneData> zones = new();
    [SerializeField] private float miniMapRange = 25f;
    [SerializeField] private GameObject worldMapPanel;
    [SerializeField] private float worldMapScale = 8f;
    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private Sprite playerMarkerSprite, enemyMarkerSprite, lootMarkerSprite, baseMarkerSprite;

    private Transform _player;
    private ZoneData  _currentZone;
    private List<MapMarker>  _markers     = new();
    private List<GameObject> _markerIcons = new();

    public static event Action<ZoneData> OnZoneEntered;
    public static event Action<ZoneData> OnZoneExited;

    private void Awake() { if (Instance != null && Instance != this) { Destroy(this); return; } Instance = this; }

    private void Start()
    {
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc) _player = pc.transform;
    }

    private void Update()
    {
        if (_player == null) return;
        UpdateMiniMapCamera();
        CheckZone();
    }

    private void UpdateMiniMapCamera()
    {
        if (miniMapCamera == null) return;
        // 미니맵 카메라: XZ 추적, Y 고정, 위에서 아래 바라봄 (Rotation 90,0,0)
        miniMapCamera.transform.position = new Vector3(
            _player.position.x,
            miniMapCamera.transform.position.y,
            _player.position.z);
        miniMapCamera.orthographicSize = miniMapRange;
    }

    private void CheckZone()
    {
        // 3D → XZ 평면 비교 (position.z가 2D의 position.y 역할)
        Vector2 playerXZ = new Vector2(_player.position.x, _player.position.z);

        ZoneData nearest = null;
        float minDist = float.MaxValue;
        foreach (var z in zones)
        {
            float dist = Vector2.Distance(playerXZ, z.center);
            if (dist < z.radius && dist < minDist) { minDist = dist; nearest = z; }
        }

        if (nearest != _currentZone)
        {
            if (_currentZone != null) OnZoneExited?.Invoke(_currentZone);
            _currentZone = nearest;
            if (_currentZone != null) OnZoneEntered?.Invoke(_currentZone);
        }
    }

    public ZoneData GetCurrentZone() => _currentZone;

    public void AddMarker(MapMarker marker)
    {
        _markers.Add(marker);
        if (markerPrefab != null) { var icon = Instantiate(markerPrefab); _markerIcons.Add(icon); }
    }

    public void ToggleWorldMap() => worldMapPanel?.SetActive(!worldMapPanel.activeSelf);
}
