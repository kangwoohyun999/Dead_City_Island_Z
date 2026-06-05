using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 제작 창 UI — 카테고리 탭 + 레시피 목록 + 재료 확인 + 제작 버튼
/// </summary>
public class CraftingUI : MonoBehaviour
{
    // ─── 카테고리 탭 ────────────────────────────────────────
    [Header("카테고리 탭")]
    [SerializeField] private Transform   tabContainer;
    [SerializeField] private GameObject  tabPrefab;

    // ─── 레시피 목록 ────────────────────────────────────────
    [Header("레시피 목록")]
    [SerializeField] private Transform   recipeListContainer;
    [SerializeField] private GameObject  recipeEntryPrefab;

    // ─── 선택된 레시피 상세 ─────────────────────────────────
    [Header("레시피 상세")]
    [SerializeField] private Image            recipeIcon;
    [SerializeField] private TextMeshProUGUI  recipeName;
    [SerializeField] private TextMeshProUGUI  recipeDesc;
    [SerializeField] private Transform        ingredientContainer;
    [SerializeField] private GameObject       ingredientRowPrefab;
    [SerializeField] private TextMeshProUGUI  craftTimeText;
    [SerializeField] private Button           craftButton;
    [SerializeField] private TextMeshProUGUI  craftButtonText;

    // ─── 제작 진행 바 ───────────────────────────────────────
    [Header("제작 진행")]
    [SerializeField] private GameObject  progressPanel;
    [SerializeField] private Slider      progressSlider;
    [SerializeField] private TextMeshProUGUI progressText;

    // ─── 검색 ────────────────────────────────────────────────
    [Header("검색")]
    [SerializeField] private TMP_InputField searchField;

    // ─── 내부 상태 ───────────────────────────────────────────
    private CraftingCategory  _selectedCategory = CraftingCategory.Weapon;
    private CraftingRecipe    _selectedRecipe;
    private List<GameObject>  _recipeRows = new();
    private Coroutine         _craftCoroutine;

    // ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        CraftingSystem.OnRecipesChanged += RefreshRecipeList;
        CraftingSystem.OnCraftResult    += OnCraftResult;
        InventorySystem.OnInventoryChanged += RefreshIngredientStatus;

        BuildCategoryTabs();
        RefreshRecipeList();
        if (progressPanel) progressPanel.SetActive(false);
        if (searchField) searchField.onValueChanged.AddListener(_ => RefreshRecipeList());
    }

    private void OnDisable()
    {
        CraftingSystem.OnRecipesChanged    -= RefreshRecipeList;
        CraftingSystem.OnCraftResult       -= OnCraftResult;
        InventorySystem.OnInventoryChanged -= RefreshIngredientStatus;
    }

    // ─── 카테고리 탭 ────────────────────────────────────────

    private void BuildCategoryTabs()
    {
        if (tabContainer == null || tabPrefab == null) return;

        foreach (Transform child in tabContainer) Destroy(child.gameObject);

        var categories = System.Enum.GetValues(typeof(CraftingCategory));
        foreach (CraftingCategory cat in categories)
        {
            var go  = Instantiate(tabPrefab, tabContainer);
            var txt = go.GetComponentInChildren<TextMeshProUGUI>();
            if (txt) txt.text = CategoryNameKR(cat);

            var btn = go.GetComponent<Button>();
            var c   = cat;
            if (btn) btn.onClick.AddListener(() => SelectCategory(c));
        }
    }

    private void SelectCategory(CraftingCategory cat)
    {
        _selectedCategory = cat;
        RefreshRecipeList();
    }

    // ─── 레시피 목록 ────────────────────────────────────────

    private void RefreshRecipeList()
    {
        if (recipeListContainer == null) return;

        foreach (var row in _recipeRows) Destroy(row);
        _recipeRows.Clear();

        var sys = CraftingSystem.Instance;
        if (sys == null) return;

        string search = searchField?.text?.Trim().ToLower() ?? "";
        var recipes = sys.GetRecipesByCategory(_selectedCategory);

        foreach (var recipe in recipes)
        {
            if (!string.IsNullOrEmpty(search) &&
                !recipe.recipeNameKR.ToLower().Contains(search)) continue;

            var go   = Instantiate(recipeEntryPrefab, recipeListContainer);
            var icon = go.transform.Find("Icon")?.GetComponent<Image>();
            var name = go.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            var lock_ = go.transform.Find("LockIcon");
            var canCraft = sys.CanCraft(recipe);

            if (icon && recipe.icon) icon.sprite = recipe.icon;
            if (name) { name.text = recipe.recipeNameKR; name.color = canCraft ? Color.white : new Color(0.5f,0.5f,0.5f); }
            if (lock_) lock_.gameObject.SetActive(!canCraft);

            var btn = go.GetComponent<Button>();
            var r   = recipe;
            if (btn) btn.onClick.AddListener(() => SelectRecipe(r));

            _recipeRows.Add(go);
        }
    }

    // ─── 레시피 상세 ────────────────────────────────────────

    private void SelectRecipe(CraftingRecipe recipe)
    {
        _selectedRecipe = recipe;

        if (recipeIcon && recipe.icon) recipeIcon.sprite = recipe.icon;
        if (recipeName)  recipeName.text = recipe.recipeNameKR;
        if (recipeDesc)  recipeDesc.text = recipe.description;
        if (craftTimeText) craftTimeText.text = $"제작 시간: {recipe.craftTime}초";

        // 재료 목록 갱신
        if (ingredientContainer)
        {
            foreach (Transform child in ingredientContainer) Destroy(child.gameObject);

            foreach (var ing in recipe.ingredients)
            {
                var row  = Instantiate(ingredientRowPrefab, ingredientContainer);
                var icon = row.transform.Find("Icon")?.GetComponent<Image>();
                var txt  = row.transform.Find("Count")?.GetComponent<TextMeshProUGUI>();

                int owned = InventorySystem.Instance?.GetItemCount(ing.item) ?? 0;
                bool enough = owned >= ing.amount;

                if (icon && ing.item.icon) icon.sprite = ing.item.icon;
                if (txt)
                {
                    txt.text  = $"{ing.item.itemNameKR}  {owned}/{ing.amount}";
                    txt.color = enough ? Color.white : new Color(0.9f, 0.3f, 0.3f);
                }
            }
        }

        RefreshCraftButton();
    }

    private void RefreshIngredientStatus()
    {
        if (_selectedRecipe != null) SelectRecipe(_selectedRecipe);
    }

    private void RefreshCraftButton()
    {
        if (craftButton == null) return;
        bool can = _selectedRecipe != null && CraftingSystem.Instance.CanCraft(_selectedRecipe);
        craftButton.interactable = can;
        if (craftButtonText) craftButtonText.text = can ? "제작" : "재료 부족";
        craftButtonText.color = can ? Color.white : new Color(0.6f, 0.6f, 0.6f);
    }

    // ─── 제작 실행 ───────────────────────────────────────────

    public void OnClickCraft()
    {
        if (_selectedRecipe == null) return;
        if (_craftCoroutine != null) return;
        _craftCoroutine = StartCoroutine(CraftRoutine(_selectedRecipe));
    }

    private System.Collections.IEnumerator CraftRoutine(CraftingRecipe recipe)
    {
        craftButton.interactable = false;
        if (progressPanel) progressPanel.SetActive(true);

        float elapsed = 0f;
        while (elapsed < recipe.craftTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / recipe.craftTime;
            if (progressSlider) progressSlider.value = t;
            if (progressText)   progressText.text = $"제작 중... {Mathf.RoundToInt(t * 100)}%";
            yield return null;
        }

        if (progressPanel) progressPanel.SetActive(false);
        CraftingSystem.Instance?.Craft(recipe);
        _craftCoroutine = null;
    }

    private void OnCraftResult(CraftingRecipe recipe, bool success)
    {
        string msg = success
            ? $"✅ {recipe.recipeNameKR} 제작 완료!"
            : $"❌ {recipe.recipeNameKR} 제작 실패";
        UIManager.Instance?.ShowNotification(msg);
        RefreshRecipeList();
        RefreshCraftButton();
    }

    // ─── 유틸 ────────────────────────────────────────────────

    private string CategoryNameKR(CraftingCategory cat) => cat switch
    {
        CraftingCategory.Weapon    => "⚔️ 무기",
        CraftingCategory.Armor     => "🛡️ 방어구",
        CraftingCategory.Tool      => "🔧 도구",
        CraftingCategory.Food      => "🍖 음식",
        CraftingCategory.Medicine  => "💊 의약품",
        CraftingCategory.Building  => "🏠 건설",
        CraftingCategory.Furniture => "🪑 가구",
        _                          => "기타"
    };
}
