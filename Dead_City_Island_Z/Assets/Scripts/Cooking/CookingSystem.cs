using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 요리 시스템 — 모닥불/주방에서 재료 조합 → 음식 완성 → 버프 적용
/// 듀랑고 스타일: 재료 자유 조합 + 레시피 발견 시스템
/// </summary>
public class CookingSystem : MonoBehaviour
{
    public static CookingSystem Instance { get; private set; }

    [Header("요리 레시피")]
    [SerializeField] private List<CookingRecipe> allRecipes = new();

    // 플레이어가 발견한 레시피
    private HashSet<string> _discoveredRecipes = new();

    // 현재 요리 슬롯 (재료 최대 4개)
    private List<ItemData> _cookingSlots = new(4);
    private CookingStationType _currentStationType = CookingStationType.None;
    private bool _isCooking;

    public static event Action<List<ItemData>>  OnCookingSlotChanged;
    public static event Action<CookingRecipe>   OnCookingStarted;
    public static event Action<ItemData, FoodBuff[]> OnCookingDone;
    public static event Action<string>          OnRecipeDiscovered;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    // ─── 요리대 상호작용 ──────────────────────────────────────

    public void OpenCookingStation(CookingStationType type)
    {
        _currentStationType = type;
        _cookingSlots.Clear();
        UIManager.Instance?.ShowNotification($"{type.ToKorean()} 사용 중");
        // TODO: CookingUI 패널 열기
    }

    public void CloseCookingStation()
    {
        _currentStationType = CookingStationType.None;
        _cookingSlots.Clear();
    }

    // ─── 재료 슬롯 관리 ───────────────────────────────────────

    public bool AddIngredient(ItemData item)
    {
        if (_cookingSlots.Count >= 4)
        {
            UIManager.Instance?.ShowNotification("재료 슬롯이 가득 찼습니다 (최대 4개)");
            return false;
        }
        if (item.category != ItemCategory.Food && item.category != ItemCategory.Material)
        {
            UIManager.Instance?.ShowNotification("요리 재료로 사용할 수 없는 아이템입니다");
            return false;
        }

        _cookingSlots.Add(item);
        InventorySystem.Instance?.RemoveItem(item, 1);
        OnCookingSlotChanged?.Invoke(new List<ItemData>(_cookingSlots));
        return true;
    }

    public void RemoveIngredient(int index)
    {
        if (index < 0 || index >= _cookingSlots.Count) return;
        var item = _cookingSlots[index];
        _cookingSlots.RemoveAt(index);
        InventorySystem.Instance?.AddItem(item, 1);
        OnCookingSlotChanged?.Invoke(new List<ItemData>(_cookingSlots));
    }

    public void ClearSlots()
    {
        foreach (var item in _cookingSlots)
            InventorySystem.Instance?.AddItem(item, 1);
        _cookingSlots.Clear();
        OnCookingSlotChanged?.Invoke(new List<ItemData>(_cookingSlots));
    }

    // ─── 요리 실행 ────────────────────────────────────────────

    public void StartCooking()
    {
        if (_isCooking || _cookingSlots.Count == 0) return;
        if (_currentStationType == CookingStationType.None) return;

        CookingRecipe matched = FindMatchingRecipe();
        StartCoroutine(CookingRoutine(matched));
    }

    private IEnumerator CookingRoutine(CookingRecipe recipe)
    {
        _isCooking = true;
        float cookTime = recipe != null ? recipe.cookTime : 5f;

        OnCookingStarted?.Invoke(recipe);
        UIManager.Instance?.ShowNotification($"요리 중... ({cookTime}초)");

        // TODO: CookingUI에 진행 바 표시
        yield return new WaitForSeconds(cookTime);

        ItemData result;
        FoodBuff[] buffs;

        if (recipe != null)
        {
            // 레시피 매칭 성공
            result = recipe.resultItem;
            buffs  = recipe.buffs;

            // 첫 발견 시 레시피 등록
            if (!_discoveredRecipes.Contains(recipe.recipeID))
            {
                _discoveredRecipes.Add(recipe.recipeID);
                OnRecipeDiscovered?.Invoke(recipe.recipeID);
                UIManager.Instance?.ShowNotification($"🍳 새 레시피 발견: {recipe.recipeNameKR}!");
            }
        }
        else
        {
            // 매칭 실패 → 탄 음식 생성 (패널티)
            result = CreateBurntFood();
            buffs  = Array.Empty<FoodBuff>();
            UIManager.Instance?.ShowNotification("🔥 음식을 태웠습니다!");
        }

        InventorySystem.Instance?.AddItem(result, 1);
        OnCookingDone?.Invoke(result, buffs);
        SkillSystem.Instance?.AddExp(SkillType.Cooking, recipe != null ? 15f : 3f);

        _cookingSlots.Clear();
        OnCookingSlotChanged?.Invoke(new List<ItemData>(_cookingSlots));
        _isCooking = false;
    }

    // ─── 레시피 매칭 ──────────────────────────────────────────

    private CookingRecipe FindMatchingRecipe()
    {
        // 현재 슬롯 재료 이름 셋
        var slotSet = new Dictionary<string, int>();
        foreach (var item in _cookingSlots)
        {
            string key = item.name;
            slotSet[key] = slotSet.TryGetValue(key, out int cnt) ? cnt + 1 : 1;
        }

        foreach (var recipe in allRecipes)
        {
            // 요리대 타입 체크
            if (recipe.requiredStation != CookingStationType.Any
                && recipe.requiredStation != _currentStationType) continue;

            // 재료 완전 매칭
            var reqSet = new Dictionary<string, int>();
            foreach (var ing in recipe.ingredients)
                reqSet[ing.item.name] = reqSet.TryGetValue(ing.item.name, out int c) ? c + ing.amount : ing.amount;

            bool match = true;
            foreach (var kv in reqSet)
            {
                if (!slotSet.TryGetValue(kv.Key, out int have) || have < kv.Value)
                { match = false; break; }
            }

            if (match) return recipe;
        }

        return null;
    }

    private ItemData CreateBurntFood()
    {
        // TODO: "탄 음식" ScriptableObject 레퍼런스 반환
        return ItemDatabase.Instance?.Get("BurntFood");
    }

    // ─── 음식 버프 적용 ───────────────────────────────────────

    public void ApplyFoodBuff(FoodBuff[] buffs)
    {
        if (buffs == null || buffs.Length == 0) return;
        var buffMgr = FindFirstObjectByType<BuffManager>();
        buffMgr?.ApplyBuffs(buffs);
    }

    // ─── 조회 ────────────────────────────────────────────────

    public List<ItemData>      GetCookingSlots()      => new(_cookingSlots);
    public bool                IsCooking              => _isCooking;
    public CookingStationType  CurrentStationType     => _currentStationType;

    public CookingRecipe PreviewRecipe()              => FindMatchingRecipe();
    public bool HasDiscoveredRecipe(string id)        => _discoveredRecipes.Contains(id);
}

// ══════════════════════════════════════════════════════════
// BuffManager — 음식/아이템 버프 관리
// ══════════════════════════════════════════════════════════
public class BuffManager : MonoBehaviour
{
    private List<ActiveBuff> _activeBuffs = new();

    public static event Action<List<ActiveBuff>> OnBuffsChanged;

    private void Update()
    {
        bool changed = false;
        for (int i = _activeBuffs.Count - 1; i >= 0; i--)
        {
            _activeBuffs[i].remaining -= Time.deltaTime;
            if (_activeBuffs[i].remaining <= 0)
            {
                _activeBuffs.RemoveAt(i);
                changed = true;
            }
        }
        if (changed) OnBuffsChanged?.Invoke(_activeBuffs);
    }

    public void ApplyBuffs(FoodBuff[] buffs)
    {
        foreach (var buff in buffs)
        {
            // 같은 타입 버프 갱신
            var existing = _activeBuffs.Find(b => b.type == buff.type);
            if (existing != null)
                existing.remaining = Mathf.Max(existing.remaining, buff.duration);
            else
                _activeBuffs.Add(new ActiveBuff { type = buff.type, value = buff.value, remaining = buff.duration });
        }
        OnBuffsChanged?.Invoke(_activeBuffs);
    }

    public float GetBuffValue(BuffType type)
    {
        var b = _activeBuffs.Find(b => b.type == type);
        return b?.value ?? 0f;
    }

    public bool HasBuff(BuffType type) => _activeBuffs.Exists(b => b.type == type);
}

// ─── 데이터 ──────────────────────────────────────────────────

[CreateAssetMenu(fileName = "NewCookingRecipe", menuName = "LastShore/Cooking/Recipe")]
public class CookingRecipe : ScriptableObject
{
    [Header("기본")]
    public string   recipeID;
    public string   recipeNameKR;
    public ItemData resultItem;
    public Sprite   icon;
    [TextArea] public string description;

    [Header("재료")]
    public List<ItemAmount> ingredients = new();

    [Header("요리 설정")]
    public CookingStationType requiredStation = CookingStationType.Any;
    public float              cookTime        = 5f;

    [Header("버프")]
    public FoodBuff[] buffs;
}

[Serializable]
public class FoodBuff
{
    public BuffType type;
    public float    value;
    public float    duration;   // 초
}

[Serializable]
public class ActiveBuff
{
    public BuffType type;
    public float    value;
    public float    remaining;
}

public enum BuffType
{
    HealthRegen,        // 체력 재생 +
    HungerReduction,    // 배고픔 감소율 -
    ThirstReduction,    // 갈증 감소율 -
    StaminaRegen,       // 스태미나 재생 +
    SpeedBoost,         // 이동속도 +
    StrengthBoost,      // 공격력 +
    DefenseBoost,       // 방어력 +
    TemperatureUp,      // 체온 유지 +
    ExpBoost            // 경험치 획득 +
}

public enum CookingStationType
{
    None,
    Any,
    Campfire,   // 모닥불
    Kitchen,    // 주방 (업그레이드된 요리대)
    Smoker      // 훈제기
}

public static class CookingStationExtensions
{
    public static string ToKorean(this CookingStationType t) => t switch
    {
        CookingStationType.Campfire => "모닥불",
        CookingStationType.Kitchen  => "주방",
        CookingStationType.Smoker   => "훈제기",
        _                           => "요리대"
    };
}
