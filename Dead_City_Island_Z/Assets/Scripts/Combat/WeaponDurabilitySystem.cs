using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기/방어구 내구도 시스템
/// 사용할수록 마모 → 수리 가능 → 완전 파손 시 인벤토리에서 제거
/// Project Zomboid 스타일
/// </summary>
public class WeaponDurabilitySystem : MonoBehaviour
{
    public static WeaponDurabilitySystem Instance { get; private set; }

    [Header("내구도 설정")]
    [SerializeField] private float durabilityLossPerHit    = 1.5f;   // 공격 1회당 감소량
    [SerializeField] private float durabilityLossOnBlock   = 0.8f;   // 방어 시 감소량
    [SerializeField] private float repairEfficiency        = 0.8f;   // 수리 시 회복 비율

    // 아이템별 현재 내구도 (itemName → durability)
    // 실제로는 ItemSlot에 float durability 필드 추가가 이상적 — 여기선 Dictionary로 관리
    private Dictionary<string, float> _durabilityMap = new();

    public static event Action<string, float, float> OnDurabilityChanged;  // (itemName, current, max)
    public static event Action<string>               OnItemBroken;          // (itemName)

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        CombatSystem.OnAttack += OnWeaponUsed;
    }

    private void OnDisable()
    {
        CombatSystem.OnAttack -= OnWeaponUsed;
    }

    // ─── 내구도 감소 ─────────────────────────────────────────

    private void OnWeaponUsed()
    {
        var inv  = InventorySystem.Instance;
        if (inv == null) return;

        var slot = inv.GetEquipSlot(EquipSlot.MainHand);
        if (slot == null || slot.IsEmpty) return;
        if (!slot.Item.hasDurability) return;

        DecreaseDurability(slot.Item, durabilityLossPerHit);
    }

    public void DecreaseDurability(ItemData item, float amount)
    {
        if (item == null || !item.hasDurability) return;

        string key  = item.name;
        if (!_durabilityMap.ContainsKey(key))
            _durabilityMap[key] = item.maxDurability;

        _durabilityMap[key] = Mathf.Max(0f, _durabilityMap[key] - amount);
        float current = _durabilityMap[key];

        OnDurabilityChanged?.Invoke(key, current, item.maxDurability);

        // 경고 알림 (20% 이하)
        if (current / item.maxDurability <= 0.2f && current > 0)
            UIManager.Instance?.ShowNotification($"⚠️ {item.itemNameKR} 내구도 부족!");

        // 파손
        if (current <= 0)
            BreakItem(item);
    }

    private void BreakItem(ItemData item)
    {
        UIManager.Instance?.ShowNotification($"💥 {item.itemNameKR} 파손됨!");
        InventorySystem.Instance?.RemoveItem(item, 1);
        _durabilityMap.Remove(item.name);
        OnItemBroken?.Invoke(item.name);
    }

    // ─── 수리 ────────────────────────────────────────────────

    /// <summary>수리 재료를 소비해 내구도 회복</summary>
    public bool RepairItem(ItemData item, ItemData repairMaterial, int materialAmount)
    {
        if (item == null || !item.hasDurability) return false;

        var inv = InventorySystem.Instance;
        if (inv == null || !inv.HasItem(repairMaterial, materialAmount)) return false;

        string key = item.name;
        if (!_durabilityMap.ContainsKey(key))
            _durabilityMap[key] = item.maxDurability;

        float missing  = item.maxDurability - _durabilityMap[key];
        float restored = missing * repairEfficiency * materialAmount;
        _durabilityMap[key] = Mathf.Min(item.maxDurability, _durabilityMap[key] + restored);

        inv.RemoveItem(repairMaterial, materialAmount);
        OnDurabilityChanged?.Invoke(key, _durabilityMap[key], item.maxDurability);
        UIManager.Instance?.ShowNotification($"🔧 {item.itemNameKR} 수리 완료");

        SkillSystem.Instance?.AddExp(SkillType.Crafting, 5f);
        return true;
    }

    // ─── 조회 ────────────────────────────────────────────────

    public float GetDurability(ItemData item)
    {
        if (item == null || !item.hasDurability) return -1f;
        return _durabilityMap.TryGetValue(item.name, out float d) ? d : item.maxDurability;
    }

    public float GetDurabilityRatio(ItemData item)
    {
        float d = GetDurability(item);
        if (d < 0 || item.maxDurability <= 0) return 1f;
        return d / item.maxDurability;
    }

    /// <summary>내구도 비율에 따른 색상 (초록→노랑→빨강)</summary>
    public Color GetDurabilityColor(ItemData item)
    {
        float r = GetDurabilityRatio(item);
        return r > 0.6f ? Color.green
             : r > 0.3f ? new Color(1f, 0.7f, 0f)
                        : Color.red;
    }

    // ─── 저장/불러오기 연동 ──────────────────────────────────

    public Dictionary<string, float> GetSaveData() => new(_durabilityMap);

    public void LoadSaveData(Dictionary<string, float> data)
    {
        if (data == null) return;
        _durabilityMap = new Dictionary<string, float>(data);
    }
}
