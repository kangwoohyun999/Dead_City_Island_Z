using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퀘스트 시스템 — 일일 퀘스트 + 스토리 미션
/// 조건: 처치/채집/제작/도달/생존 등
/// </summary>
public class QuestSystem : MonoBehaviour
{
    public static QuestSystem Instance { get; private set; }

    [Header("퀘스트 데이터")]
    [SerializeField] private List<QuestData> allQuests       = new();
    [SerializeField] private List<QuestData> dailyQuestPool  = new();

    [Header("일일 퀘스트 설정")]
    [SerializeField] private int maxDailyQuests  = 3;
    [SerializeField] private int maxActiveQuests = 5;

    // 활성 퀘스트 (진행 중)
    private List<ActiveQuest> _activeQuests   = new();
    // 완료된 퀘스트 ID
    private HashSet<string>   _completedIDs   = new();
    // 오늘의 일일 퀘스트
    private List<QuestData>   _dailyQuests    = new();
    private int               _lastDayRefresh = -1;

    public static event Action<ActiveQuest>  OnQuestAccepted;
    public static event Action<ActiveQuest>  OnQuestProgressChanged;
    public static event Action<ActiveQuest>  OnQuestCompleted;
    public static event Action<ActiveQuest>  OnQuestFailed;
    public static event Action<List<QuestData>> OnDailyQuestsRefreshed;

    public IReadOnlyList<ActiveQuest> ActiveQuests => _activeQuests;
    public IReadOnlyList<QuestData>   DailyQuests  => _dailyQuests;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        // 게임 이벤트 구독 → 퀘스트 조건 갱신
        ZombieAI.OnZombieDied            += OnEnemyKilled;
        ResourceNode.OnResourceHarvested += OnResourceHarvested;
        CraftingSystem.OnCraftResult     += OnItemCrafted;
        GameManager.OnDayChanged         += OnDayChanged;
    }

    private void OnDisable()
    {
        ZombieAI.OnZombieDied            -= OnEnemyKilled;
        ResourceNode.OnResourceHarvested -= OnResourceHarvested;
        CraftingSystem.OnCraftResult     -= OnItemCrafted;
        GameManager.OnDayChanged         -= OnDayChanged;
    }

    // ─── 일일 퀘스트 갱신 ────────────────────────────────────

    private void OnDayChanged(int day)
    {
        if (day == _lastDayRefresh) return;
        _lastDayRefresh = day;
        RefreshDailyQuests();
    }

    private void RefreshDailyQuests()
    {
        _dailyQuests.Clear();

        // 풀에서 랜덤 선택 (완료된 것 제외 — 일일 퀘스트는 매일 리셋)
        var pool = new List<QuestData>(dailyQuestPool);
        ShuffleList(pool);

        int count = Mathf.Min(maxDailyQuests, pool.Count);
        for (int i = 0; i < count; i++)
            _dailyQuests.Add(pool[i]);

        OnDailyQuestsRefreshed?.Invoke(_dailyQuests);
        UIManager.Instance?.ShowNotification("📋 새 일일 퀘스트가 도착했습니다!");
    }

    // ─── 퀘스트 수락 ─────────────────────────────────────────

    public bool AcceptQuest(string questID)
    {
        if (_activeQuests.Count >= maxActiveQuests)
        {
            UIManager.Instance?.ShowNotification("진행 중인 퀘스트가 너무 많습니다");
            return false;
        }
        if (_activeQuests.Exists(q => q.data.questID == questID))
        {
            UIManager.Instance?.ShowNotification("이미 진행 중인 퀘스트입니다");
            return false;
        }
        if (!AllowRepeat(questID) && _completedIDs.Contains(questID))
        {
            UIManager.Instance?.ShowNotification("이미 완료한 퀘스트입니다");
            return false;
        }

        var data = allQuests.Find(q => q.questID == questID)
                ?? dailyQuestPool.Find(q => q.questID == questID);
        if (data == null) return false;

        var active = new ActiveQuest(data);
        _activeQuests.Add(active);
        OnQuestAccepted?.Invoke(active);
        UIManager.Instance?.ShowNotification($"📋 퀘스트 수락: {data.questNameKR}");
        return true;
    }

    // ─── 진행도 업데이트 ─────────────────────────────────────

    private void UpdateProgress(QuestConditionType type, string targetID, int amount = 1)
    {
        foreach (var quest in _activeQuests)
        {
            bool changed = false;
            foreach (var cond in quest.conditions)
            {
                if (cond.type != type) continue;
                if (!string.IsNullOrEmpty(cond.targetID) && cond.targetID != targetID) continue;

                cond.current = Mathf.Min(cond.current + amount, cond.required);
                changed = true;
            }

            if (changed)
            {
                OnQuestProgressChanged?.Invoke(quest);
                if (quest.IsCompleted) CompleteQuest(quest);
            }
        }
    }

    // ─── 퀘스트 완료 ─────────────────────────────────────────

    private void CompleteQuest(ActiveQuest quest)
    {
        _activeQuests.Remove(quest);
        _completedIDs.Add(quest.data.questID);

        // 보상 지급
        GrantRewards(quest.data.rewards);
        OnQuestCompleted?.Invoke(quest);

        UIManager.Instance?.ShowNotification(
            $"✅ 퀘스트 완료: {quest.data.questNameKR}");
    }

    private void GrantRewards(QuestReward rewards)
    {
        if (rewards == null) return;
        var inv = InventorySystem.Instance;

        foreach (var item in rewards.items)
            inv?.AddItem(item.item, item.amount);

        if (rewards.expAmount > 0)
            SkillSystem.Instance?.AddExp(rewards.expSkill, rewards.expAmount);

        // 새 레시피 잠금 해제
        foreach (var recipeID in rewards.unlockedRecipes)
            CraftingSystem.Instance?.UnlockRecipe(recipeID);

        UIManager.Instance?.ShowNotification(
            $"🎁 보상: {string.Join(", ", rewards.items.ConvertAll(i => i.item.itemNameKR))}");
    }

    // ─── 이벤트 핸들러 ───────────────────────────────────────

    private void OnEnemyKilled(ZombieAI zombie)
        => UpdateProgress(QuestConditionType.KillEnemy, "Zombie", 1);

    private void OnResourceHarvested(ResourceNode node, ItemData item, int amount)
        => UpdateProgress(QuestConditionType.HarvestResource, item.name, amount);

    private void OnItemCrafted(CraftingRecipe recipe, bool success)
    {
        if (!success) return;
        foreach (var result in recipe.results)
            UpdateProgress(QuestConditionType.CraftItem, result.item.name, result.amount);
    }

    // ─── 유틸 ────────────────────────────────────────────────

    private bool AllowRepeat(string id)
        => dailyQuestPool.Exists(q => q.questID == id);   // 일일 퀘스트는 매일 반복

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public bool IsCompleted(string id) => _completedIDs.Contains(id);
}

// ─── 데이터 ──────────────────────────────────────────────────

[CreateAssetMenu(fileName = "NewQuest", menuName = "LastShore/Quest/QuestData")]
public class QuestData : ScriptableObject
{
    [Header("기본")]
    public string    questID;
    public string    questNameKR;
    [TextArea(2, 4)]
    public string    description;
    public QuestType questType = QuestType.Daily;
    public Sprite    icon;

    [Header("조건")]
    public List<QuestCondition> conditions = new();

    [Header("보상")]
    public QuestReward rewards;

    [Header("선행 퀘스트")]
    public List<string> prerequisiteIDs = new();
}

[Serializable]
public class QuestCondition
{
    public QuestConditionType type;
    public string             targetID;   // 아이템/적 ID
    public string             displayName;
    public int                required    = 1;
    public int                current     = 0;
}

[Serializable]
public class QuestReward
{
    public List<ItemAmount> items           = new();
    public SkillType        expSkill        = SkillType.None;
    public float            expAmount       = 0f;
    public List<string>     unlockedRecipes = new();
}

public class ActiveQuest
{
    public QuestData            data;
    public List<QuestCondition> conditions;

    public ActiveQuest(QuestData d)
    {
        data       = d;
        // 딥 카피 (진행도 독립 유지)
        conditions = new List<QuestCondition>();
        foreach (var c in d.conditions)
            conditions.Add(new QuestCondition
            {
                type        = c.type,
                targetID    = c.targetID,
                displayName = c.displayName,
                required    = c.required,
                current     = 0
            });
    }

    public bool IsCompleted => conditions.TrueForAll(c => c.current >= c.required);
    public float Progress   => conditions.Count == 0 ? 1f
        : conditions.ConvertAll(c => (float)c.current / c.required)
                    .Average();
}

public enum QuestType { Daily, Story, Side }

public enum QuestConditionType
{
    KillEnemy,          // 적 처치
    HarvestResource,    // 자원 채집
    CraftItem,          // 아이템 제작
    CookFood,           // 음식 요리
    ReachLocation,      // 특정 위치 도달
    SurviveDays,        // N일 생존
    ClearDungeon,       // 던전 클리어
    TradeWithNPC,       // NPC 교역
    PlantCrop,          // 농작물 심기
    BuildStructure      // 건물 건설
}
