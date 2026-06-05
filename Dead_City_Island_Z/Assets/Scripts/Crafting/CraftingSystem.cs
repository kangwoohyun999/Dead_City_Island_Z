using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레시피 기반 제작 시스템
/// 듀랑고 스타일 — 스킬 레벨 + 재료 요구
/// </summary>
public class CraftingSystem : MonoBehaviour
{
    public static CraftingSystem Instance { get; private set; }

    [Header("레시피 목록")]
    [SerializeField] private List<CraftingRecipe> allRecipes = new();

    // 잠금 해제된 레시피 (기본 + 설계도로 추가)
    private HashSet<string> _unlockedRecipeIDs = new();

    public static event Action OnRecipesChanged;
    public static event Action<CraftingRecipe, bool> OnCraftResult; // (recipe, success)

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        UnlockDefaultRecipes();
    }

    private void UnlockDefaultRecipes()
    {
        foreach (var recipe in allRecipes)
            if (recipe.isDefaultUnlocked)
                _unlockedRecipeIDs.Add(recipe.recipeID);
    }

    // ─── 레시피 잠금 해제 (설계도 아이템 사용 시) ───────────
    public void UnlockRecipe(string recipeID)
    {
        if (_unlockedRecipeIDs.Contains(recipeID)) return;
        _unlockedRecipeIDs.Add(recipeID);
        OnRecipesChanged?.Invoke();
        Debug.Log($"[Crafting] 레시피 잠금 해제: {recipeID}");
    }

    // ─── 제작 가능 여부 확인 ─────────────────────────────────
    public bool CanCraft(CraftingRecipe recipe)
    {
        if (recipe == null) return false;
        if (!_unlockedRecipeIDs.Contains(recipe.recipeID)) return false;

        var inventory = InventorySystem.Instance;
        if (inventory == null) return false;

        foreach (var ingredient in recipe.ingredients)
        {
            if (!inventory.HasItem(ingredient.item, ingredient.amount))
                return false;
        }

        // TODO: 스킬 레벨 체크
        // if (SkillSystem.Instance.GetLevel(recipe.requiredSkill) < recipe.requiredSkillLevel)
        //     return false;

        return true;
    }

    // ─── 제작 실행 ───────────────────────────────────────────
    public bool Craft(CraftingRecipe recipe)
    {
        if (!CanCraft(recipe))
        {
            OnCraftResult?.Invoke(recipe, false);
            return false;
        }

        var inventory = InventorySystem.Instance;

        // 재료 소비
        foreach (var ingredient in recipe.ingredients)
            inventory.RemoveItem(ingredient.item, ingredient.amount);

        // 결과물 추가
        foreach (var result in recipe.results)
        {
            bool added = inventory.AddItem(result.item, result.amount);
            if (!added)
            {
                // 인벤토리가 꽉 찬 경우 — 바닥에 드롭
                DropItemToWorld(result.item, result.amount);
            }
        }

        // TODO: 스킬 경험치 증가
        // SkillSystem.Instance.AddExp(recipe.requiredSkill, recipe.expReward);

        OnCraftResult?.Invoke(recipe, true);
        Debug.Log($"[Crafting] 제작 성공: {recipe.recipeName}");
        return true;
    }

    private void DropItemToWorld(ItemData item, int amount)
    {
        if (item.prefab == null) return;

        var player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;

        Vector3 dropPos = player.transform.position + UnityEngine.Random.insideUnitSphere * 1.5f;
        dropPos.z = 0;

        var obj = Instantiate(item.prefab, dropPos, Quaternion.identity);
        if (obj.TryGetComponent(out WorldItem worldItem))
            worldItem.Initialize(item, amount);
    }

    // ─── 레시피 조회 ─────────────────────────────────────────
    public List<CraftingRecipe> GetUnlockedRecipes()
    {
        return allRecipes.FindAll(r => _unlockedRecipeIDs.Contains(r.recipeID));
    }

    public List<CraftingRecipe> GetRecipesByCategory(CraftingCategory category)
    {
        return GetUnlockedRecipes().FindAll(r => r.category == category);
    }
}

// ─── 데이터 클래스 ──────────────────────────────────────────

[CreateAssetMenu(fileName = "NewRecipe", menuName = "LastShore/Crafting/Recipe")]
public class CraftingRecipe : ScriptableObject
{
    [Header("기본")]
    public string          recipeID;
    public string          recipeName;
    public string          recipeNameKR;
    public CraftingCategory category;
    public Sprite          icon;
    [TextArea(1, 3)]
    public string          description;

    [Header("잠금")]
    public bool            isDefaultUnlocked = false;

    [Header("재료")]
    public List<ItemAmount> ingredients = new();

    [Header("결과물")]
    public List<ItemAmount> results = new();

    [Header("제작 시간")]
    public float           craftTime    = 2f;             // 초

    [Header("스킬 요구")]
    public SkillType       requiredSkill     = SkillType.None;
    public int             requiredSkillLevel = 0;
    public float           expReward         = 10f;

    [Header("작업대 요구")]
    public WorkbenchType   requiredWorkbench = WorkbenchType.None;
}

[Serializable]
public struct ItemAmount
{
    public ItemData item;
    public int      amount;
}

public enum CraftingCategory
{
    Weapon,
    Armor,
    Tool,
    Food,
    Medicine,
    Building,
    Furniture,
    Misc
}

public enum WorkbenchType
{
    None,           // 맨손 제작
    Campfire,       // 모닥불
    Workbench,      // 작업대
    Forge,          // 대장간
    Laboratory,     // 연구소
    Kitchen         // 주방
}

public enum SkillType
{
    None,
    Crafting,
    Cooking,
    Smithing,
    Carpentry,
    Farming,
    Combat,
    Gathering,
    Medicine
}
