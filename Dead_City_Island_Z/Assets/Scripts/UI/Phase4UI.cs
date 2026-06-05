using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ══════════════════════════════════════════════════════════
// QuestHUD — 화면 우측 활성 퀘스트 추적 위젯
// ══════════════════════════════════════════════════════════
public class QuestHUD : MonoBehaviour
{
    [SerializeField] private Transform      questEntryContainer;
    [SerializeField] private GameObject     questEntryPrefab;
    [SerializeField] private int            maxDisplayed = 3;

    private List<GameObject> _entries = new();

    private void OnEnable()
    {
        QuestSystem.OnQuestAccepted        += Refresh;
        QuestSystem.OnQuestProgressChanged += Refresh;
        QuestSystem.OnQuestCompleted       += Refresh;
    }

    private void OnDisable()
    {
        QuestSystem.OnQuestAccepted        -= Refresh;
        QuestSystem.OnQuestProgressChanged -= Refresh;
        QuestSystem.OnQuestCompleted       -= Refresh;
    }

    private void Refresh(ActiveQuest _) => RefreshAll();

    private void RefreshAll()
    {
        foreach (var e in _entries) Destroy(e);
        _entries.Clear();

        if (QuestSystem.Instance == null) return;

        var active = QuestSystem.Instance.ActiveQuests;
        int count  = Mathf.Min(maxDisplayed, active.Count);

        for (int i = 0; i < count; i++)
        {
            var quest = active[i];
            var go    = Instantiate(questEntryPrefab, questEntryContainer);
            _entries.Add(go);

            var nameText   = go.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            var progBar    = go.transform.Find("ProgressBar")?.GetComponent<Slider>();
            var condText   = go.transform.Find("Condition")?.GetComponent<TextMeshProUGUI>();

            if (nameText)  nameText.text = quest.data.questNameKR;
            if (progBar)   progBar.value = quest.Progress;

            if (condText && quest.conditions.Count > 0)
            {
                var cond = quest.conditions[0];   // 첫 번째 조건만 표시
                condText.text = $"{cond.displayName}: {cond.current}/{cond.required}";
            }
        }
    }
}


// ══════════════════════════════════════════════════════════
// SkillTreeUI — 스킬 트리 + 스탯 분배 창
// ══════════════════════════════════════════════════════════
public class SkillTreeUI : MonoBehaviour
{
    [Header("스킬 트리 영역")]
    [SerializeField] private Transform      nodeContainer;
    [SerializeField] private GameObject     nodePrefab;
    [SerializeField] private TextMeshProUGUI availablePointsText;

    [Header("스탯 분배")]
    [SerializeField] private StatAllocRow[] statRows;  // Inspector에서 연결

    [Header("선택된 노드 상세")]
    [SerializeField] private GameObject      detailPanel;
    [SerializeField] private TextMeshProUGUI detailName;
    [SerializeField] private TextMeshProUGUI detailEffect;
    [SerializeField] private TextMeshProUGUI detailCost;
    [SerializeField] private Button          unlockButton;

    private SkillTreeNode _selectedNode;
    private List<GameObject> _nodeObjects = new();

    private void OnEnable()
    {
        CharacterGrowthSystem.OnNodeUnlocked     += _ => RefreshAll();
        CharacterGrowthSystem.OnStatPointsChanged += _ => RefreshAll();
        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshPointsText();
        RefreshNodes();
        RefreshStatRows();
    }

    private void RefreshPointsText()
    {
        if (availablePointsText == null) return;
        int pts = CharacterGrowthSystem.Instance?.AvailablePoints ?? 0;
        availablePointsText.text = $"사용 가능 포인트: {pts}P";
    }

    private void RefreshNodes()
    {
        foreach (var go in _nodeObjects) Destroy(go);
        _nodeObjects.Clear();

        var sys = CharacterGrowthSystem.Instance;
        if (sys == null || nodeContainer == null || nodePrefab == null) return;

        // TODO: SkillTreeData의 노드들을 treePosition 기준으로 배치
        // 현재는 수직 리스트로 표시
    }

    private void RefreshStatRows()
    {
        if (statRows == null) return;
        foreach (var row in statRows)
            row?.Refresh();
    }

    public void SelectNode(SkillTreeNode node)
    {
        _selectedNode = node;
        if (detailPanel == null) return;

        detailPanel.SetActive(node != null);
        if (node == null) return;

        if (detailName)   detailName.text   = node.nodeNameKR;
        if (detailEffect) detailEffect.text = node.effectDescription;
        if (detailCost)   detailCost.text   = $"비용: {node.cost}P";

        bool unlocked = CharacterGrowthSystem.Instance?.IsUnlocked(node.nodeID) ?? false;
        if (unlockButton)
        {
            unlockButton.interactable = !unlocked;
            unlockButton.GetComponentInChildren<TextMeshProUGUI>().text = unlocked ? "✅ 습득됨" : "습득";
        }
    }

    public void OnClickUnlock()
    {
        if (_selectedNode == null) return;
        CharacterGrowthSystem.Instance?.UnlockNode(_selectedNode.nodeID);
    }
}

[System.Serializable]
public class StatAllocRow
{
    public StatType          statType;
    public TextMeshProUGUI   statLabel;
    public TextMeshProUGUI   valueText;
    public Button            addButton;

    public void Refresh()
    {
        int alloc = CharacterGrowthSystem.Instance?.GetAllocated(statType) ?? 0;
        if (valueText) valueText.text = alloc.ToString();

        int pts = CharacterGrowthSystem.Instance?.AvailablePoints ?? 0;
        if (addButton) addButton.interactable = pts > 0;
    }
}


// ══════════════════════════════════════════════════════════
// TradeUI — NPC 교역 창
// ══════════════════════════════════════════════════════════
public class TradeUI : MonoBehaviour
{
    [Header("NPC 정보")]
    [SerializeField] private Image            npcPortrait;
    [SerializeField] private TextMeshProUGUI  npcName;
    [SerializeField] private Slider           favorabilityBar;
    [SerializeField] private TextMeshProUGUI  favorabilityText;

    [Header("교역 목록")]
    [SerializeField] private Transform        offerContainer;
    [SerializeField] private GameObject       offerEntryPrefab;

    [Header("패널")]
    [SerializeField] private GameObject       panel;

    private NPCTrader _currentTrader;
    private List<GameObject> _offerEntries = new();

    private void OnEnable()
    {
        NPCTrader.OnTradeOpened      += Open;
        NPCTrader.OnFavorabilityChanged += OnFavorChanged;
        NPCTrader.OnTradeCompleted   += OnTradeCompleted;
    }

    private void OnDisable()
    {
        NPCTrader.OnTradeOpened      -= Open;
        NPCTrader.OnFavorabilityChanged -= OnFavorChanged;
        NPCTrader.OnTradeCompleted   -= OnTradeCompleted;
    }

    public void Open(NPCTrader trader)
    {
        _currentTrader = trader;
        panel?.SetActive(true);
        RefreshTraderInfo();
        RefreshOffers();
    }

    public void Close()
    {
        panel?.SetActive(false);
        _currentTrader = null;
    }

    private void RefreshTraderInfo()
    {
        if (_currentTrader?.Data == null) return;
        var data = _currentTrader.Data;
        if (npcPortrait && data.portrait) npcPortrait.sprite = data.portrait;
        if (npcName) npcName.text = data.npcNameKR;
        UpdateFavorability(_currentTrader.Favorability);
    }

    private void RefreshOffers()
    {
        foreach (var e in _offerEntries) Destroy(e);
        _offerEntries.Clear();

        if (_currentTrader == null || offerContainer == null || offerEntryPrefab == null) return;

        foreach (var offer in _currentTrader.CurrentOffers)
        {
            var go = Instantiate(offerEntryPrefab, offerContainer);
            _offerEntries.Add(go);

            // 제공 아이템
            var offeredIcon = go.transform.Find("OfferedIcon")?.GetComponent<Image>();
            var offeredText = go.transform.Find("OfferedText")?.GetComponent<TextMeshProUGUI>();
            // 요구 아이템
            var requiredIcon = go.transform.Find("RequiredIcon")?.GetComponent<Image>();
            var requiredText = go.transform.Find("RequiredText")?.GetComponent<TextMeshProUGUI>();
            var tradeBtn     = go.transform.Find("TradeButton")?.GetComponent<Button>();
            var stockText    = go.transform.Find("StockText")?.GetComponent<TextMeshProUGUI>();
            var specialBadge = go.transform.Find("SpecialBadge");

            if (offeredIcon  && offer.offeredItem?.icon)  offeredIcon.sprite  = offer.offeredItem.icon;
            if (requiredIcon && offer.requiredItem?.icon) requiredIcon.sprite = offer.requiredItem.icon;
            if (offeredText)  offeredText.text  = $"{offer.offeredItem?.itemNameKR} x{offer.offeredAmount}";
            if (requiredText) requiredText.text = $"{offer.requiredItem?.itemNameKR} x{offer.requiredAmount}";
            if (stockText)    stockText.text    = $"재고: {offer.stock}";
            if (specialBadge) specialBadge.gameObject.SetActive(offer.isSpecial);

            var o = offer;
            bool canTrade = InventorySystem.Instance?.HasItem(offer.requiredItem, offer.requiredAmount) ?? false;
            if (tradeBtn)
            {
                tradeBtn.interactable = canTrade && offer.stock > 0;
                tradeBtn.onClick.AddListener(() => _currentTrader?.ExecuteTrade(o));
            }
        }
    }

    private void UpdateFavorability(float favor)
    {
        if (favorabilityBar)  favorabilityBar.value = favor / 100f;
        if (favorabilityText) favorabilityText.text = $"호감도 {Mathf.RoundToInt(favor)}/100";
    }

    private void OnFavorChanged(NPCTrader t, float f) { if (t == _currentTrader) UpdateFavorability(f); }
    private void OnTradeCompleted(TradeOffer o, bool s) { if (s) RefreshOffers(); }
}


// ══════════════════════════════════════════════════════════
// CookingUI — 요리대 UI (재료 슬롯 + 레시피 미리보기 + 요리 버튼)
// ══════════════════════════════════════════════════════════
public class CookingUI : MonoBehaviour
{
    [Header("재료 슬롯 (4칸)")]
    [SerializeField] private Image[]          ingredientSlots;
    [SerializeField] private TextMeshProUGUI[] ingredientNames;

    [Header("레시피 미리보기")]
    [SerializeField] private GameObject       recipePreviewPanel;
    [SerializeField] private Image            recipeResultIcon;
    [SerializeField] private TextMeshProUGUI  recipeResultName;
    [SerializeField] private TextMeshProUGUI  recipeBonusText;

    [Header("버튼")]
    [SerializeField] private Button           cookButton;
    [SerializeField] private Button           clearButton;
    [SerializeField] private TextMeshProUGUI  cookButtonLabel;

    [Header("진행 바")]
    [SerializeField] private GameObject       progressPanel;
    [SerializeField] private Slider           progressBar;

    [Header("패널")]
    [SerializeField] private GameObject       panel;

    private void OnEnable()
    {
        CookingSystem.OnCookingSlotChanged += RefreshSlots;
        CookingSystem.OnCookingStarted     += OnCookingStarted;
        CookingSystem.OnCookingDone        += OnCookingDone;
    }

    private void OnDisable()
    {
        CookingSystem.OnCookingSlotChanged -= RefreshSlots;
        CookingSystem.OnCookingStarted     -= OnCookingStarted;
        CookingSystem.OnCookingDone        -= OnCookingDone;
    }

    private void RefreshSlots(List<ItemData> slots)
    {
        for (int i = 0; i < ingredientSlots.Length; i++)
        {
            bool hasItem = i < slots.Count && slots[i] != null;
            ingredientSlots[i].enabled = hasItem;
            if (hasItem) ingredientSlots[i].sprite = slots[i].icon;
            if (ingredientNames != null && i < ingredientNames.Length)
                ingredientNames[i].text = hasItem ? slots[i].itemNameKR : "";
        }

        // 레시피 미리보기
        var preview = CookingSystem.Instance?.PreviewRecipe();
        if (recipePreviewPanel) recipePreviewPanel.SetActive(preview != null);
        if (preview != null)
        {
            if (recipeResultIcon && preview.resultItem?.icon) recipeResultIcon.sprite = preview.resultItem.icon;
            if (recipeResultName) recipeResultName.text = preview.recipeNameKR;
            if (recipeBonusText && preview.buffs != null && preview.buffs.Length > 0)
                recipeBonusText.text = $"버프: {preview.buffs[0].type} +{preview.buffs[0].value} ({preview.buffs[0].duration}초)";
        }

        bool canCook = slots.Count > 0 && !(CookingSystem.Instance?.IsCooking ?? false);
        if (cookButton)    cookButton.interactable = canCook;
        if (cookButtonLabel) cookButtonLabel.text = preview != null ? "요리하기" : "재료 조합 시도";
    }

    private void OnCookingStarted(CookingRecipe recipe)
    {
        if (progressPanel) progressPanel.SetActive(true);
        if (progressBar)   progressBar.value = 0f;
        if (cookButton)    cookButton.interactable = false;
        StartCoroutine(AnimateProgress(recipe?.cookTime ?? 5f));
    }

    private System.Collections.IEnumerator AnimateProgress(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (progressBar) progressBar.value = elapsed / duration;
            yield return null;
        }
    }

    private void OnCookingDone(ItemData result, FoodBuff[] buffs)
    {
        if (progressPanel) progressPanel.SetActive(false);
        if (cookButton)    cookButton.interactable = true;
        CookingSystem.Instance?.ApplyFoodBuff(buffs);
        RefreshSlots(new List<ItemData>());
    }

    public void OnClickCook()   => CookingSystem.Instance?.StartCooking();
    public void OnClickClear()  => CookingSystem.Instance?.ClearSlots();
    public void OnClickClose()  => panel?.SetActive(false);
}
