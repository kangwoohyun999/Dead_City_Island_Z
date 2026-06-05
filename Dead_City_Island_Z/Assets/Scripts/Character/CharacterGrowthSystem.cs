using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 성장 시스템 — 스킬 트리 노드 잠금 해제 + 스탯 포인트 분배
/// 듀랑고처럼 행동 기반 숙련도 + 별도 스킬 트리 투자 가능
/// </summary>
public class CharacterGrowthSystem : MonoBehaviour
{
    public static CharacterGrowthSystem Instance { get; private set; }

    [Header("스킬 트리 데이터")]
    [SerializeField] private SkillTreeData skillTreeData;

    [Header("스탯 포인트")]
    [SerializeField] private int statPointsPerLevel = 2;

    // 잠금 해제된 노드 ID
    private HashSet<string>           _unlockedNodes   = new();
    // 스탯 투자 현황
    private Dictionary<StatType, int> _allocatedStats  = new();
    // 사용 가능한 스탯 포인트
    private int                       _availablePoints = 0;

    public static event Action<string>      OnNodeUnlocked;
    public static event Action<StatType, int> OnStatAllocated;
    public static event Action<int>         OnStatPointsChanged;

    public int AvailablePoints => _availablePoints;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        foreach (StatType t in Enum.GetValues(typeof(StatType)))
            _allocatedStats[t] = 0;
    }

    private void OnEnable()
    {
        SkillSystem.OnSkillLevelUp += OnSkillLevelUp;
    }
    private void OnDisable()
    {
        SkillSystem.OnSkillLevelUp -= OnSkillLevelUp;
    }

    // ─── 스킬 레벨업 → 포인트 지급 ──────────────────────────

    private void OnSkillLevelUp(SkillType skill, int newLevel)
    {
        // 특정 레벨마다 스킬 포인트 지급
        if (newLevel % 5 == 0)
        {
            _availablePoints += statPointsPerLevel;
            OnStatPointsChanged?.Invoke(_availablePoints);
            UIManager.Instance?.ShowNotification($"✨ 스탯 포인트 +{statPointsPerLevel} 획득!");
        }
    }

    // ─── 스킬 트리 노드 잠금 해제 ────────────────────────────

    public bool UnlockNode(string nodeID)
    {
        if (_unlockedNodes.Contains(nodeID))
        {
            UIManager.Instance?.ShowNotification("이미 해제된 스킬입니다");
            return false;
        }

        var node = skillTreeData?.GetNode(nodeID);
        if (node == null) return false;

        // 선행 노드 확인
        foreach (var req in node.requiredNodes)
        {
            if (!_unlockedNodes.Contains(req))
            {
                UIManager.Instance?.ShowNotification($"선행 스킬이 필요합니다");
                return false;
            }
        }

        // 스킬 레벨 요구 확인
        int skillLevel = SkillSystem.Instance?.GetLevel(node.requiredSkill) ?? 0;
        if (skillLevel < node.requiredSkillLevel)
        {
            UIManager.Instance?.ShowNotification(
                $"{node.requiredSkill} Lv.{node.requiredSkillLevel} 이상 필요");
            return false;
        }

        // 포인트 소비
        if (_availablePoints < node.cost)
        {
            UIManager.Instance?.ShowNotification($"스킬 포인트 부족 ({node.cost}P 필요)");
            return false;
        }

        _availablePoints -= node.cost;
        _unlockedNodes.Add(nodeID);

        ApplyNodeEffect(node);
        OnNodeUnlocked?.Invoke(nodeID);
        OnStatPointsChanged?.Invoke(_availablePoints);
        UIManager.Instance?.ShowNotification($"⭐ {node.nodeNameKR} 습득!");
        return true;
    }

    private void ApplyNodeEffect(SkillTreeNode node)
    {
        // 패시브 효과 적용 (버프 매니저 또는 스탯에 직접 반영)
        // 예: 공격력 +10%, 체력 최대치 +20 등
        Debug.Log($"[Growth] 노드 효과 적용: {node.nodeNameKR} — {node.effectDescription}");
    }

    // ─── 스탯 포인트 분배 ────────────────────────────────────

    public bool AllocateStat(StatType stat, int points = 1)
    {
        if (_availablePoints < points)
        {
            UIManager.Instance?.ShowNotification("스탯 포인트 부족");
            return false;
        }

        _availablePoints        -= points;
        _allocatedStats[stat]   += points;

        OnStatAllocated?.Invoke(stat, _allocatedStats[stat]);
        OnStatPointsChanged?.Invoke(_availablePoints);

        // 실제 스탯에 반영
        ApplyStatAllocation(stat, points);
        return true;
    }

    private void ApplyStatAllocation(StatType stat, int points)
    {
        var survival = FindFirstObjectByType<SurvivalStats>();
        // 실제 적용은 SurvivalStats에 SetMaxHealth 등 메서드 추가 필요
        // 현재는 로그로 대체
        Debug.Log($"[Growth] {stat} +{points} 투자 → 효과 적용");
    }

    // ─── 조회 ────────────────────────────────────────────────

    public bool IsUnlocked(string nodeID)       => _unlockedNodes.Contains(nodeID);
    public int  GetAllocated(StatType stat)      => _allocatedStats.TryGetValue(stat, out int v) ? v : 0;
    public HashSet<string> GetUnlockedNodes()   => new(_unlockedNodes);
}

// ─── 데이터 ──────────────────────────────────────────────────

[CreateAssetMenu(fileName = "SkillTreeData", menuName = "LastShore/Character/SkillTreeData")]
public class SkillTreeData : ScriptableObject
{
    public List<SkillTreeNode> nodes = new();

    public SkillTreeNode GetNode(string id) => nodes.Find(n => n.nodeID == id);
}

[Serializable]
public class SkillTreeNode
{
    [Header("기본")]
    public string     nodeID;
    public string     nodeNameKR;
    [TextArea(1, 3)]
    public string     effectDescription;
    public Sprite     icon;

    [Header("요구 조건")]
    public List<string> requiredNodes      = new();
    public SkillType    requiredSkill      = SkillType.None;
    public int          requiredSkillLevel = 0;
    public int          cost               = 1;     // 스킬 포인트 비용

    [Header("효과")]
    public NodeEffectType effectType;
    public float          effectValue;

    [Header("UI 위치 (스킬 트리 레이아웃)")]
    public Vector2 treePosition;    // 스킬 트리 UI에서의 좌표
}

public enum NodeEffectType
{
    MaxHealthUp,        // 최대 체력 증가
    MaxStaminaUp,       // 최대 스태미나 증가
    AttackDamageUp,     // 공격력 증가
    DefenseUp,          // 방어력 증가
    MoveSpeedUp,        // 이동속도 증가
    HarvestAmountUp,    // 채집량 증가
    CraftSpeedDown,     // 제작 시간 감소
    HungerRateDown,     // 배고픔 감소 속도 낮춤
    ThirstRateDown,     // 갈증 감소 속도 낮춤
    CritChanceUp,       // 크리티컬 확률 증가
    DodgeChanceUp       // 회피 확률 증가
}

public enum StatType
{
    Strength,       // 힘 — 공격력, 무게 한도
    Agility,        // 민첩 — 이동속도, 회피, 공격속도
    Endurance,      // 체력 — 최대 HP, 스태미나
    Intelligence,   // 지능 — 제작 효율, 경험치 획득
    Survival        // 생존 — 배고픔/갈증 감소율, 체온 유지
}
