using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그리드 기반 인벤토리 시스템
/// Project Zomboid / 듀랑고 스타일
/// </summary>
public class InventorySystem : MonoBehaviour
{
    public static InventorySystem Instance { get; private set; }

    // ─── 인벤토리 설정 ──────────────────────────────────────
    [Header("인벤토리 설정")]
    [SerializeField] private int   slots       = 30;
    [SerializeField] private float maxWeight   = 20f;              // kg

    [Header("핫바")]
    [SerializeField] private int   hotbarSlots = 8;

    // ─── 아이템 슬롯 ────────────────────────────────────────
    private ItemSlot[] _inventory;
    private ItemSlot[] _hotbar;
    private int        _selectedHotbarIndex = 0;

    // ─── 장비 슬롯 ──────────────────────────────────────────
    private Dictionary<EquipSlot, ItemSlot> _equipSlots;

    // ─── 이벤트 ─────────────────────────────────────────────
    public static event Action                OnInventoryChanged;
    public static event Action<int>           OnHotbarSelectionChanged;
    public static event Action<ItemData, int> OnItemAdded;         // (item, amount)
    public static event Action<ItemData, int> OnItemRemoved;

    // ─── 현재 무게 ──────────────────────────────────────────
    public float CurrentWeight { get; private set; }
    public float MaxWeight     => maxWeight;
    public bool  IsFull        => CurrentWeight >= maxWeight;

    // ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        InitializeInventory();
    }

    private void InitializeInventory()
    {
        _inventory = new ItemSlot[slots];
        _hotbar    = new ItemSlot[hotbarSlots];

        for (int i = 0; i < slots;       i++) _inventory[i] = new ItemSlot();
        for (int i = 0; i < hotbarSlots; i++) _hotbar[i]    = new ItemSlot();

        _equipSlots = new Dictionary<EquipSlot, ItemSlot>();
        foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
        {
            if (slot != EquipSlot.None)
                _equipSlots[slot] = new ItemSlot();
        }
    }

    // ─── 아이템 추가 ─────────────────────────────────────────

    /// <summary>아이템 추가. 스택 가능 아이템은 자동으로 합침.</summary>
    public bool AddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;

        float totalAddWeight = item.weight * amount;
        if (CurrentWeight + totalAddWeight > maxWeight)
        {
            Debug.Log("[Inventory] 무게 초과 — 아이템 추가 불가");
            return false;
        }

        int remaining = amount;

        // 1. 기존 스택에 추가 시도
        if (item.canStack)
        {
            foreach (var slot in _inventory)
            {
                if (!slot.IsEmpty && slot.Item == item && slot.Amount < item.maxStackSize)
                {
                    int canAdd = item.maxStackSize - slot.Amount;
                    int toAdd  = Mathf.Min(canAdd, remaining);
                    slot.Amount += toAdd;
                    remaining   -= toAdd;
                    if (remaining <= 0) break;
                }
            }
        }

        // 2. 빈 슬롯에 추가
        while (remaining > 0)
        {
            ItemSlot emptySlot = GetFirstEmptySlot();
            if (emptySlot == null)
            {
                Debug.Log("[Inventory] 빈 슬롯 없음");
                break;
            }

            int toAdd     = item.canStack ? Mathf.Min(remaining, item.maxStackSize) : 1;
            emptySlot.Set(item, toAdd);
            remaining -= toAdd;
        }

        if (remaining < amount)
        {
            int added = amount - remaining;
            CurrentWeight += item.weight * added;
            OnItemAdded?.Invoke(item, added);
            OnInventoryChanged?.Invoke();
            return true;
        }

        return false;
    }

    // ─── 아이템 제거 ─────────────────────────────────────────

    public bool RemoveItem(ItemData item, int amount = 1)
    {
        int remaining = amount;

        foreach (var slot in _inventory)
        {
            if (slot.IsEmpty || slot.Item != item) continue;

            int toRemove = Mathf.Min(slot.Amount, remaining);
            slot.Amount -= toRemove;
            remaining   -= toRemove;

            if (slot.Amount <= 0) slot.Clear();
            if (remaining   <= 0) break;
        }

        if (remaining < amount)
        {
            int removed = amount - remaining;
            CurrentWeight -= item.weight * removed;
            CurrentWeight  = Mathf.Max(0, CurrentWeight);
            OnItemRemoved?.Invoke(item, removed);
            OnInventoryChanged?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>보유 수량 확인</summary>
    public int GetItemCount(ItemData item)
    {
        int count = 0;
        foreach (var slot in _inventory)
            if (!slot.IsEmpty && slot.Item == item)
                count += slot.Amount;
        return count;
    }

    public bool HasItem(ItemData item, int amount = 1)
        => GetItemCount(item) >= amount;

    // ─── 핫바 ────────────────────────────────────────────────

    public void SelectHotbarSlot(int index)
    {
        if (index < 0 || index >= hotbarSlots) return;
        _selectedHotbarIndex = index;
        OnHotbarSelectionChanged?.Invoke(index);
    }

    public ItemSlot GetSelectedHotbarSlot() => _hotbar[_selectedHotbarIndex];

    public void UseSelectedItem()
    {
        var slot = GetSelectedHotbarSlot();
        if (slot.IsEmpty) return;

        if (slot.Item.isConsumable)
            UseConsumable(slot);
    }

    // ─── 장비 ────────────────────────────────────────────────

    public bool Equip(ItemData item)
    {
        if (!item.isEquippable || item.equipSlot == EquipSlot.None) return false;

        var targetSlot = _equipSlots[item.equipSlot];

        // 기존 장비 해제
        if (!targetSlot.IsEmpty)
            Unequip(item.equipSlot);

        // 장착
        RemoveItem(item, 1);
        targetSlot.Set(item, 1);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public void Unequip(EquipSlot slot)
    {
        var equipSlot = _equipSlots[slot];
        if (equipSlot.IsEmpty) return;

        AddItem(equipSlot.Item, equipSlot.Amount);
        equipSlot.Clear();
        OnInventoryChanged?.Invoke();
    }

    public ItemSlot GetEquipSlot(EquipSlot slot) => _equipSlots[slot];

    // ─── 유틸리티 ────────────────────────────────────────────

    private ItemSlot GetFirstEmptySlot()
    {
        foreach (var slot in _inventory)
            if (slot.IsEmpty) return slot;
        return null;
    }

    private void UseConsumable(ItemSlot slot)
    {
        var stats = FindFirstObjectByType<SurvivalStats>();
        if (stats == null) return;

        var item = slot.Item;
        stats.Heal(item.healthRestore);
        stats.Eat(item.hungerRestore);
        stats.Drink(item.thirstRestore);
        // TODO: 상태이상 효과 처리

        slot.Amount--;
        if (slot.Amount <= 0) slot.Clear();

        CurrentWeight -= item.weight;
        OnInventoryChanged?.Invoke();
    }

    public ItemSlot[] GetInventory() => _inventory;
    public ItemSlot[] GetHotbar()    => _hotbar;
}

/// <summary>인벤토리 슬롯 하나</summary>
[Serializable]
public class ItemSlot
{
    public ItemData Item   { get; private set; }
    public int      Amount { get; set; }

    public bool IsEmpty => Item == null || Amount <= 0;

    public void Set(ItemData item, int amount)
    {
        Item   = item;
        Amount = amount;
    }

    public void Clear()
    {
        Item   = null;
        Amount = 0;
    }
}
