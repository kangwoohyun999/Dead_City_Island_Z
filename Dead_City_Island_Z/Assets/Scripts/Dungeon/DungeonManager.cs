using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도시 특수 구역 던전 매니저
/// 병원 / 군사기지 / 지하벙커 — 각각 고유 레이아웃 + 보상 + 보스
/// </summary>
public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance { get; private set; }

    [Header("던전 데이터")]
    [SerializeField] private List<DungeonData> dungeons = new();

    [Header("클리어 보상 배율")]
    [SerializeField] private float firstClearBonusMult = 2f;

    private DungeonData          _activeDungeon;
    private DungeonState         _state = DungeonState.Idle;
    private int                  _currentFloor;
    private int                  _enemiesRemaining;
    private HashSet<string>      _clearedDungeons = new();

    public static event Action<DungeonData>  OnDungeonEntered;
    public static event Action<int>          OnFloorChanged;
    public static event Action<DungeonData>  OnDungeonCleared;
    public static event Action               OnDungeonFailed;

    public DungeonState State          => _state;
    public DungeonData  ActiveDungeon  => _activeDungeon;
    public int          CurrentFloor   => _currentFloor;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    // ─── 던전 진입 ────────────────────────────────────────────

    public bool EnterDungeon(string dungeonID)
    {
        if (_state != DungeonState.Idle) return false;

        var dungeon = dungeons.Find(d => d.dungeonID == dungeonID);
        if (dungeon == null)
        {
            Debug.LogWarning($"[DungeonManager] 던전 없음: {dungeonID}");
            return false;
        }

        _activeDungeon = dungeon;
        _currentFloor  = 1;
        _state         = DungeonState.InProgress;

        OnDungeonEntered?.Invoke(dungeon);
        UIManager.Instance?.ShowNotification($"⚠️ {dungeon.dungeonNameKR} 진입!");

        StartCoroutine(StartFloor(_currentFloor));
        return true;
    }

    // ─── 층 진행 ─────────────────────────────────────────────

    private IEnumerator StartFloor(int floor)
    {
        OnFloorChanged?.Invoke(floor);
        UIManager.Instance?.ShowNotification($"🏢 {floor}층 / {_activeDungeon.totalFloors}층");

        // 해당 층 적 스폰
        var floorData = _activeDungeon.GetFloorData(floor);
        _enemiesRemaining = floorData.enemyCount;

        SpawnFloorEnemies(floorData);

        // 모든 적 처치 대기
        while (_enemiesRemaining > 0 && _state == DungeonState.InProgress)
            yield return new WaitForSeconds(0.5f);

        if (_state != DungeonState.InProgress) yield break;

        // 층 클리어
        DropFloorLoot(floorData);
        UIManager.Instance?.ShowNotification($"✅ {floor}층 클리어!");

        yield return new WaitForSeconds(1.5f);

        if (floor >= _activeDungeon.totalFloors)
            StartCoroutine(ClearDungeon());
        else
        {
            _currentFloor++;
            StartCoroutine(StartFloor(_currentFloor));
        }
    }

    private void SpawnFloorEnemies(FloorData floor)
    {
        var spawner = EnemySpawner.Instance;
        if (spawner == null) return;

        // 던전 내 고정 위치에 스폰
        for (int i = 0; i < floor.enemyCount; i++)
        {
            // TODO: 던전 씬 내 SpawnPoint 트랜스폼 배열 사용
            // 현재는 EnemySpawner의 기존 스폰 로직 활용
        }
    }

    private void DropFloorLoot(FloorData floor)
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null || floor.lootTable == null) return;
        floor.lootTable.DropLoot(player.transform.position);
    }

    // ─── 던전 클리어 ──────────────────────────────────────────

    private IEnumerator ClearDungeon()
    {
        _state = DungeonState.Cleared;

        bool firstClear = !_clearedDungeons.Contains(_activeDungeon.dungeonID);
        _clearedDungeons.Add(_activeDungeon.dungeonID);

        UIManager.Instance?.ShowNotification(
            firstClear ? $"🏆 {_activeDungeon.dungeonNameKR} 최초 클리어!" : $"🏆 {_activeDungeon.dungeonNameKR} 클리어!");

        // 최종 보상
        GrantClearRewards(firstClear);
        OnDungeonCleared?.Invoke(_activeDungeon);

        yield return new WaitForSeconds(3f);
        ExitDungeon();
    }

    private void GrantClearRewards(bool firstClear)
    {
        var inv = InventorySystem.Instance;
        if (inv == null || _activeDungeon.clearRewards == null) return;

        float mult = firstClear ? firstClearBonusMult : 1f;

        foreach (var reward in _activeDungeon.clearRewards)
        {
            int amount = Mathf.RoundToInt(reward.amount * mult);
            inv.AddItem(reward.item, amount);
        }

        // 스킬 경험치 대량 지급
        SkillSystem.Instance?.AddExp(SkillType.Combat, _activeDungeon.expReward * (firstClear ? 2f : 1f));
    }

    // ─── 던전 탈출 / 실패 ─────────────────────────────────────

    public void ExitDungeon()
    {
        _state         = DungeonState.Idle;
        _activeDungeon = null;
        _currentFloor  = 0;
    }

    public void OnPlayerDied()
    {
        if (_state != DungeonState.InProgress) return;
        _state = DungeonState.Failed;
        OnDungeonFailed?.Invoke();
        UIManager.Instance?.ShowNotification("💀 던전 실패 — 보상 없음");
        ExitDungeon();
    }

    // ─── 적 처치 알림 ────────────────────────────────────────

    public void NotifyEnemyKilled()
    {
        if (_state != DungeonState.InProgress) return;
        _enemiesRemaining = Mathf.Max(0, _enemiesRemaining - 1);
    }

    public bool HasCleared(string id) => _clearedDungeons.Contains(id);
}

// ─── 데이터 ──────────────────────────────────────────────────

[CreateAssetMenu(fileName = "NewDungeon", menuName = "LastShore/Dungeon/DungeonData")]
public class DungeonData : ScriptableObject
{
    [Header("기본")]
    public string      dungeonID;
    public string      dungeonNameKR;
    public DungeonType dungeonType;
    [TextArea] public string description;
    public Sprite      icon;

    [Header("구조")]
    public int         totalFloors     = 3;
    public DangerLevel dangerLevel     = DangerLevel.High;

    [Header("층별 데이터")]
    public List<FloorData> floors = new();

    [Header("클리어 보상")]
    public List<ItemAmount> clearRewards = new();
    public float            expReward    = 500f;

    public FloorData GetFloorData(int floor)
    {
        int idx = Mathf.Clamp(floor - 1, 0, floors.Count - 1);
        return floors.Count > 0 ? floors[idx] : new FloorData { enemyCount = 5 };
    }
}

[Serializable]
public class FloorData
{
    public string    floorName;
    public int       enemyCount  = 5;
    public bool      hasBoss     = false;
    public string    bossID;
    public LootTable lootTable;
}

public enum DungeonType
{
    Hospital,       // 🏥 병원 — 의약품, 의료 장비
    MilitaryBase,   // 🪖 군사기지 — 총기, 탄약, 군용 장비
    UndergroundBunker, // 🔦 지하벙커 — 희귀 자원, 설계도
    ShoppingMall,   // 🏬 쇼핑몰 — 생필품, 랜덤 전리품
    ResearchLab     // 🔬 연구소 — 특수 재료, 기술 설계도
}

public enum DungeonState { Idle, InProgress, Cleared, Failed }
