using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>건설 시스템 — XZ 평면 그리드, Physics.CheckBox(3D)</summary>
public class BuildingSystem : MonoBehaviour
{
    public static BuildingSystem Instance { get; private set; }

    [SerializeField] private float  cellSize   = 1f;
    [SerializeField] private int    gridWidth  = 100, gridHeight = 100;
    [SerializeField] private Color  validColor = new(0.2f,1f,0.2f,0.5f), invalidColor = new(1f,0.2f,0.2f,0.5f);
    [SerializeField] private LayerMask obstacleLayer, groundLayer;

    private Dictionary<Vector2Int, PlacedObject> _placed = new();
    private BuildingPrefabData _selectedPrefab;
    private GameObject _ghost;
    private bool _isBuilding;
    private Camera _cam;

    public static event Action<BuildingPrefabData> OnBuildModeEntered;
    public static event Action                     OnBuildModeExited;

    public bool IsBuilding => _isBuilding;

    private void Awake() { if (Instance != null && Instance != this) { Destroy(this); return; } Instance = this; _cam = Camera.main; groundLayer = LayerMask.GetMask("Ground"); }

    private void Update()
    {
        if (!_isBuilding) return;
        if (PlayerController.IsPointerOverUI()) return;
        UpdateGhost();
        if (Input.GetMouseButtonDown(0)) TryPlace();
        if (Input.GetMouseButtonDown(1)) ExitBuildMode();
        if (Input.GetKeyDown(KeyCode.R))  RotateGhost();
    }

    public void EnterBuildMode(BuildingPrefabData data) { ExitBuildMode(); _selectedPrefab = data; _isBuilding = true; _ghost = Instantiate(data.prefab); SetGhostColor(validColor); OnBuildModeEntered?.Invoke(data); }
    public void ExitBuildMode() { if (_ghost) Destroy(_ghost); _isBuilding = false; _selectedPrefab = null; OnBuildModeExited?.Invoke(); }

    private void UpdateGhost()
    {
        if (_ghost == null) return;
        var pos = GetMouseWorldPos();
        if (!pos.HasValue) return;
        var cell = WorldToCell(pos.Value);
        _ghost.transform.position = CellToWorld(cell);
        SetGhostColor(CanPlace(cell, _selectedPrefab.size) ? validColor : invalidColor);
    }

    private void RotateGhost()
    {
        if (_ghost) _ghost.transform.Rotate(0f, 90f, 0f); // 3D Y축 회전
        (_selectedPrefab.size.x, _selectedPrefab.size.y) = (_selectedPrefab.size.y, _selectedPrefab.size.x);
    }

    private void TryPlace()
    {
        var pos = GetMouseWorldPos(); if (!pos.HasValue) return;
        var cell = WorldToCell(pos.Value);
        if (!CanPlace(cell, _selectedPrefab.size) || !HasMaterials(_selectedPrefab)) return;
        ConsumeMaterials(_selectedPrefab);
        var go = Instantiate(_selectedPrefab.prefab, CellToWorld(cell), _ghost.transform.rotation);
        var po = go.GetComponent<PlacedObject>() ?? go.AddComponent<PlacedObject>();
        po.Initialize(_selectedPrefab, cell);
        for (int x = 0; x < _selectedPrefab.size.x; x++)
            for (int z = 0; z < _selectedPrefab.size.y; z++)
                _placed[new Vector2Int(cell.x+x, cell.y+z)] = po;
    }

    // 3D Physics.CheckBox — XZ 평면
    private bool CanPlace(Vector2Int origin, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                var cell = new Vector2Int(origin.x+x, origin.y+z);
                if (cell.x < 0 || cell.x >= gridWidth || cell.y < 0 || cell.y >= gridHeight) return false;
                if (_placed.ContainsKey(cell)) return false;
                Vector3 checkPos = CellToWorld(cell) + Vector3.up * 0.5f;
                if (Physics.CheckBox(checkPos, new Vector3(cellSize*0.45f, 1f, cellSize*0.45f), Quaternion.identity, obstacleLayer)) return false;
            }
        }
        return true;
    }

    // 3D 좌표 변환 (XZ 평면)
    private Vector2Int WorldToCell(Vector3 w) => new Vector2Int(Mathf.FloorToInt(w.x / cellSize), Mathf.FloorToInt(w.z / cellSize));
    private Vector3    CellToWorld(Vector2Int c) => new Vector3(c.x * cellSize + cellSize * 0.5f, 0f, c.y * cellSize + cellSize * 0.5f);

    // 마우스 → Ground Raycast → 3D 위치
    private Vector3? GetMouseWorldPos()
    {
        if (_cam == null) return null;
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayer) ? hit.point : (Vector3?)null;
    }

    private bool HasMaterials(BuildingPrefabData d) { var inv = InventorySystem.Instance; if (inv == null) return false; foreach (var m in d.requiredMaterials) if (!inv.HasItem(m.item, m.amount)) return false; return true; }
    private void ConsumeMaterials(BuildingPrefabData d) { foreach (var m in d.requiredMaterials) InventorySystem.Instance?.RemoveItem(m.item, m.amount); }

    private void SetGhostColor(Color color)
    {
        if (_ghost == null) return;
        foreach (var r in _ghost.GetComponentsInChildren<Renderer>())
            foreach (var mat in r.materials) mat.color = color;
    }
}

// ─── BuildingSystem 보조 데이터 클래스 ──────────────────────


[CreateAssetMenu(fileName = "NewBuilding", menuName = "LastShore/Building/BuildingPrefab")]
public class BuildingPrefabData : ScriptableObject
{
    public string         buildingName;
    public string         buildingNameKR;
    public GameObject     prefab;
    public Sprite         icon;
    public Vector2Int     size = Vector2Int.one;
    public BuildingTier   tier = BuildingTier.Wood;
    public List<ItemAmount> requiredMaterials = new();
    [TextArea] public string description;
}

public class PlacedObject : MonoBehaviour
{
    public BuildingPrefabData PrefabData { get; private set; }
    public Vector2Int GridOrigin { get; private set; }

    public void Initialize(BuildingPrefabData data, Vector2Int origin)
    {
        PrefabData = data;
        GridOrigin = origin;
    }
}

public enum BuildingTier { Wood, Stone, Metal, Reinforced }

