using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 인벤토리 창 UI — Project Zomboid 스타일 그리드
/// 드래그&드롭, 우클릭 컨텍스트 메뉴, 장비 슬롯
/// </summary>
public class InventoryUI : MonoBehaviour
{
    // ─── 인벤토리 그리드 ────────────────────────────────────
    [Header("인벤토리 그리드")]
    [SerializeField] private Transform      slotContainer;
    [SerializeField] private GameObject     slotPrefab;
    [SerializeField] private int            columns = 6;

    // ─── 장비 슬롯 ──────────────────────────────────────────
    [Header("장비 슬롯")]
    [SerializeField] private EquipSlotUI    headSlot;
    [SerializeField] private EquipSlotUI    chestSlot;
    [SerializeField] private EquipSlotUI    legsSlot;
    [SerializeField] private EquipSlotUI    feetSlot;
    [SerializeField] private EquipSlotUI    mainHandSlot;
    [SerializeField] private EquipSlotUI    offHandSlot;
    [SerializeField] private EquipSlotUI    backpackSlot;

    // ─── 무게 표시 ──────────────────────────────────────────
    [Header("무게")]
    [SerializeField] private Slider             weightSlider;
    [SerializeField] private TextMeshProUGUI    weightText;

    // ─── 아이템 툴팁 ────────────────────────────────────────
    [Header("툴팁")]
    [SerializeField] private GameObject         tooltipPanel;
    [SerializeField] private TextMeshProUGUI    tooltipName;
    [SerializeField] private TextMeshProUGUI    tooltipRarity;
    [SerializeField] private TextMeshProUGUI    tooltipDesc;
    [SerializeField] private TextMeshProUGUI    tooltipStats;

    // ─── 컨텍스트 메뉴 ──────────────────────────────────────
    [Header("우클릭 메뉴")]
    [SerializeField] private GameObject         contextMenu;
    [SerializeField] private Button             btnUse;
    [SerializeField] private Button             btnEquip;
    [SerializeField] private Button             btnDrop;
    [SerializeField] private Button             btnDiscard;

    // ─── 드래그 ─────────────────────────────────────────────
    [Header("드래그")]
    [SerializeField] private Image              dragIcon;    // 드래그 중 표시 아이콘

    // ─── 내부 상태 ───────────────────────────────────────────
    private List<InventorySlotUI> _slots    = new();
    private InventorySlotUI       _dragFrom;
    private InventorySlotUI       _contextTarget;
    private Canvas                _canvas;

    // ───────────────────────────────────────────────────────

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        BuildGrid();
        HideTooltip();
        HideContextMenu();
        if (dragIcon) dragIcon.enabled = false;
    }

    private void OnEnable()
    {
        InventorySystem.OnInventoryChanged += RefreshAll;
        RefreshAll();
    }

    private void OnDisable()
    {
        InventorySystem.OnInventoryChanged -= RefreshAll;
    }

    private void Update()
    {
        // 드래그 아이콘 따라다니기
        if (_dragFrom != null && dragIcon != null && dragIcon.enabled)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.GetComponent<RectTransform>(),
                Input.mousePosition, _canvas.worldCamera,
                out Vector2 localPoint);
            dragIcon.rectTransform.anchoredPosition = localPoint;
        }

        // ESC로 컨텍스트 메뉴 닫기
        if (Input.GetKeyDown(KeyCode.Escape))
            HideContextMenu();
    }

    // ─── 그리드 생성 ─────────────────────────────────────────

    private void BuildGrid()
    {
        if (slotContainer == null || slotPrefab == null) return;

        var inv = InventorySystem.Instance;
        int slotCount = inv?.GetInventory().Length ?? 30;

        for (int i = 0; i < slotCount; i++)
        {
            var go   = Instantiate(slotPrefab, slotContainer);
            var slot = go.GetComponent<InventorySlotUI>();
            if (slot == null) slot = go.AddComponent<InventorySlotUI>();

            int idx = i;
            slot.Init(i,
                onLeftClick:  () => OnSlotLeftClick(slot),
                onRightClick: () => OnSlotRightClick(slot),
                onBeginDrag:  () => OnBeginDrag(slot),
                onDrop:       () => OnDropOnSlot(slot),
                onHover:      () => ShowTooltip(slot),
                onHoverExit:  () => HideTooltip());
            _slots.Add(slot);
        }
    }

    // ─── 데이터 갱신 ─────────────────────────────────────────

    private void RefreshAll()
    {
        var inv = InventorySystem.Instance;
        if (inv == null) return;

        var inventory = inv.GetInventory();
        for (int i = 0; i < _slots.Count && i < inventory.Length; i++)
            _slots[i].SetData(inventory[i]);

        RefreshEquipSlots(inv);
        RefreshWeight(inv);
    }

    private void RefreshEquipSlots(InventorySystem inv)
    {
        headSlot?    .SetData(inv.GetEquipSlot(EquipSlot.Head));
        chestSlot?   .SetData(inv.GetEquipSlot(EquipSlot.Chest));
        legsSlot?    .SetData(inv.GetEquipSlot(EquipSlot.Legs));
        feetSlot?    .SetData(inv.GetEquipSlot(EquipSlot.Feet));
        mainHandSlot?.SetData(inv.GetEquipSlot(EquipSlot.MainHand));
        offHandSlot? .SetData(inv.GetEquipSlot(EquipSlot.OffHand));
        backpackSlot?.SetData(inv.GetEquipSlot(EquipSlot.Backpack));
    }

    private void RefreshWeight(InventorySystem inv)
    {
        if (weightSlider != null)
        {
            weightSlider.maxValue = inv.MaxWeight;
            weightSlider.value    = inv.CurrentWeight;

            // 색상: 여유→경고→위험
            float ratio = inv.CurrentWeight / inv.MaxWeight;
            var fillImg = weightSlider.fillRect?.GetComponent<Image>();
            if (fillImg != null)
                fillImg.color = ratio < 0.7f ? new Color(0.3f, 0.8f, 0.3f)
                              : ratio < 0.9f ? new Color(0.9f, 0.7f, 0.1f)
                                             : new Color(0.9f, 0.2f, 0.2f);
        }
        if (weightText != null)
            weightText.text = $"{inv.CurrentWeight:F1} / {inv.MaxWeight:F1} kg";
    }

    // ─── 슬롯 이벤트 ─────────────────────────────────────────

    private void OnSlotLeftClick(InventorySlotUI slot)
    {
        HideContextMenu();
        // 더블클릭 → 사용/장착
        // TODO: 더블클릭 타이머 추가
    }

    private void OnSlotRightClick(InventorySlotUI slot)
    {
        if (slot.SlotData == null || slot.SlotData.IsEmpty) return;
        _contextTarget = slot;
        ShowContextMenu(slot);
    }

    // ─── 드래그 & 드롭 ───────────────────────────────────────

    private void OnBeginDrag(InventorySlotUI slot)
    {
        if (slot.SlotData == null || slot.SlotData.IsEmpty) return;
        _dragFrom = slot;

        if (dragIcon != null && slot.SlotData.Item?.icon != null)
        {
            dragIcon.sprite  = slot.SlotData.Item.icon;
            dragIcon.enabled = true;
        }
    }

    private void OnDropOnSlot(InventorySlotUI targetSlot)
    {
        if (_dragFrom == null) return;

        // 슬롯 데이터 스왑
        var inv = InventorySystem.Instance;
        // TODO: inv.SwapSlots(fromIndex, toIndex) 구현
        // 현재는 시각적 갱신만
        RefreshAll();

        _dragFrom = null;
        if (dragIcon) dragIcon.enabled = false;
    }

    // ─── 툴팁 ────────────────────────────────────────────────

    private void ShowTooltip(InventorySlotUI slot)
    {
        if (tooltipPanel == null) return;
        if (slot.SlotData == null || slot.SlotData.IsEmpty) { HideTooltip(); return; }

        var item = slot.SlotData.Item;
        tooltipPanel.SetActive(true);

        if (tooltipName   != null) { tooltipName.text   = item.itemNameKR; tooltipName.color = item.RarityColor; }
        if (tooltipRarity != null)   tooltipRarity.text = item.rarity.ToString();
        if (tooltipDesc   != null)   tooltipDesc.text   = item.description;

        if (tooltipStats != null)
        {
            string stats = $"무게: {item.weight}kg\n";
            if (item.isConsumable)
            {
                if (item.healthRestore > 0)  stats += $"HP +{item.healthRestore}\n";
                if (item.hungerRestore > 0)  stats += $"포만감 +{item.hungerRestore}\n";
                if (item.thirstRestore > 0)  stats += $"수분 +{item.thirstRestore}\n";
            }
            if (item.IsWeapon)
            {
                stats += $"공격력: {item.attackDamage}\n";
                stats += $"공격속도: {item.attackSpeed}\n";
                stats += $"사거리: {item.attackRange}\n";
            }
            if (item.IsArmor)
                stats += $"방어력: {item.defense}\n";
            if (item.hasDurability)
                stats += $"내구도: {item.maxDurability}";
            tooltipStats.text = stats;
        }

        // 마우스 위치 근처에 배치
        PositionTooltipNearMouse();
    }

    private void PositionTooltipNearMouse()
    {
        if (tooltipPanel == null || _canvas == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.GetComponent<RectTransform>(),
            Input.mousePosition, _canvas.worldCamera, out Vector2 lp);

        var rt = tooltipPanel.GetComponent<RectTransform>();
        lp.x += rt.sizeDelta.x * 0.5f + 10f;
        lp.y -= rt.sizeDelta.y * 0.5f + 10f;
        rt.anchoredPosition = lp;
    }

    private void HideTooltip() => tooltipPanel?.SetActive(false);

    // ─── 컨텍스트 메뉴 ───────────────────────────────────────

    private void ShowContextMenu(InventorySlotUI slot)
    {
        if (contextMenu == null || slot.SlotData?.Item == null) return;
        contextMenu.SetActive(true);

        var item = slot.SlotData.Item;
        btnUse?  .gameObject.SetActive(item.isConsumable);
        btnEquip?.gameObject.SetActive(item.isEquippable);
        btnDrop? .gameObject.SetActive(true);

        btnUse?  .onClick.RemoveAllListeners();
        btnEquip?.onClick.RemoveAllListeners();
        btnDrop? .onClick.RemoveAllListeners();
        btnDiscard?.onClick.RemoveAllListeners();

        btnUse?  .onClick.AddListener(() => { InventorySystem.Instance?.UseSelectedItem(); HideContextMenu(); RefreshAll(); });
        btnEquip?.onClick.AddListener(() => { InventorySystem.Instance?.Equip(item);       HideContextMenu(); RefreshAll(); });
        btnDrop? .onClick.AddListener(() => { DropItem(item, slot.SlotData.Amount);        HideContextMenu(); });
        btnDiscard?.onClick.AddListener(() => { InventorySystem.Instance?.RemoveItem(item, slot.SlotData.Amount); HideContextMenu(); RefreshAll(); });

        // 마우스 위치에 배치
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.GetComponent<RectTransform>(),
            Input.mousePosition, _canvas.worldCamera, out Vector2 lp);
        contextMenu.GetComponent<RectTransform>().anchoredPosition = lp;
    }

    private void HideContextMenu() => contextMenu?.SetActive(false);

    private void DropItem(ItemData item, int amount)
    {
        InventorySystem.Instance?.RemoveItem(item, amount);
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null || item.prefab == null) return;
        Vector3 dropPos = player.transform.position + (Vector3)Random.insideUnitCircle;
        dropPos.z = 0;
        var go = Instantiate(item.prefab, dropPos, Quaternion.identity);
        if (go.TryGetComponent(out WorldItem wi)) wi.Initialize(item, amount);
        RefreshAll();
    }
}

// ══════════════════════════════════════════════════════════
// InventorySlotUI — 개별 슬롯 컴포넌트
// ══════════════════════════════════════════════════════════

public class InventorySlotUI : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDropHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image            background;
    [SerializeField] private Image            iconImage;
    [SerializeField] private TextMeshProUGUI  countText;
    [SerializeField] private Image            rarityBorder;

    public  ItemSlot  SlotData   { get; private set; }
    public  int       SlotIndex  { get; private set; }

    private System.Action _onLeft, _onRight, _onBeginDrag, _onDrop, _onHover, _onHoverExit;

    private static readonly Color BG_NORMAL   = new(0.15f, 0.15f, 0.15f, 0.9f);
    private static readonly Color BG_HOVER    = new(0.25f, 0.25f, 0.25f, 0.9f);
    private static readonly Color BG_SELECTED = new(0.35f, 0.35f, 0.15f, 0.9f);

    public void Init(int index,
        System.Action onLeftClick, System.Action onRightClick,
        System.Action onBeginDrag, System.Action onDrop,
        System.Action onHover,     System.Action onHoverExit)
    {
        SlotIndex    = index;
        _onLeft      = onLeftClick;
        _onRight     = onRightClick;
        _onBeginDrag = onBeginDrag;
        _onDrop      = onDrop;
        _onHover     = onHover;
        _onHoverExit = onHoverExit;
    }

    public void SetData(ItemSlot slot)
    {
        SlotData = slot;

        bool hasItem = slot != null && !slot.IsEmpty;
        if (iconImage  != null) { iconImage.enabled  = hasItem; if (hasItem) iconImage.sprite = slot.Item.icon; }
        if (countText  != null) { countText.enabled  = hasItem && slot.Item.canStack && slot.Amount > 1; if (hasItem) countText.text = slot.Amount.ToString(); }
        if (rarityBorder!= null){ rarityBorder.enabled= hasItem; if (hasItem) rarityBorder.color = slot.Item.RarityColor; }
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left)  _onLeft?.Invoke();
        if (e.button == PointerEventData.InputButton.Right) _onRight?.Invoke();
    }

    public void OnBeginDrag(PointerEventData e)  => _onBeginDrag?.Invoke();
    public void OnDrop(PointerEventData e)        => _onDrop?.Invoke();
    public void OnPointerEnter(PointerEventData e){ if (background) background.color = BG_HOVER; _onHover?.Invoke(); }
    public void OnPointerExit(PointerEventData e) { if (background) background.color = BG_NORMAL; _onHoverExit?.Invoke(); }
}

// ══════════════════════════════════════════════════════════
// EquipSlotUI — 장비 슬롯 UI 컴포넌트
// ══════════════════════════════════════════════════════════

public class EquipSlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image           iconImage;
    [SerializeField] private Image           slotBg;
    [SerializeField] private TextMeshProUGUI slotLabel;
    [SerializeField] private EquipSlot       equipSlot;

    private ItemSlot _data;

    public void SetData(ItemSlot slot)
    {
        _data = slot;
        bool hasItem = slot != null && !slot.IsEmpty;
        if (iconImage != null) { iconImage.enabled = hasItem; if (hasItem) iconImage.sprite = slot.Item.icon; }
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Right && _data != null && !_data.IsEmpty)
            InventorySystem.Instance?.Unequip(equipSlot);
    }
}
