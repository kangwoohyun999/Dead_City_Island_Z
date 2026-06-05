using UnityEngine;

/// <summary>
/// 모든 아이템의 기본 데이터 (ScriptableObject)
/// 듀랑고 / Last Day On Earth 스타일 아이템 시스템
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "LastShore/Item/ItemData")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    public string itemName        = "새 아이템";
    public string itemNameKR      = "새 아이템";
    [TextArea(2, 4)]
    public string description     = "아이템 설명";
    public Sprite icon;
    public GameObject prefab;     // 월드에 드롭될 때 사용

    [Header("분류")]
    public ItemCategory category  = ItemCategory.Material;
    public ItemRarity   rarity    = ItemRarity.Common;

    [Header("스택/무게")]
    public bool canStack          = true;
    public int  maxStackSize      = 99;
    public float weight           = 0.1f;             // kg

    [Header("내구도")]
    public bool  hasDurability    = false;
    public float maxDurability    = 100f;

    [Header("장비 설정 (무기/방어구)")]
    public bool       isEquippable= false;
    public EquipSlot  equipSlot   = EquipSlot.None;
    public float      attackDamage= 0f;
    public float      attackSpeed = 1f;
    public float      attackRange = 1f;
    public float      defense     = 0f;

    [Header("소비 아이템")]
    public bool  isConsumable     = false;
    public float healthRestore    = 0f;
    public float hungerRestore    = 0f;
    public float thirstRestore    = 0f;
    public float staminaRestore   = 0f;
    public StatusEffect[] effects;

    [Header("재료 (제작용)")]
    public bool isCraftingMaterial = false;

    // ─── 유틸리티 ────────────────────────────────────────────
    public bool IsWeapon  => equipSlot is EquipSlot.MainHand or EquipSlot.OffHand;
    public bool IsArmor   => equipSlot is EquipSlot.Head or EquipSlot.Chest
                                       or EquipSlot.Legs or EquipSlot.Feet;
    public Color RarityColor => rarity switch
    {
        ItemRarity.Common    => Color.white,
        ItemRarity.Uncommon  => new Color(0.3f, 0.9f, 0.3f),
        ItemRarity.Rare      => new Color(0.2f, 0.5f, 1f),
        ItemRarity.Epic      => new Color(0.7f, 0.2f, 1f),
        ItemRarity.Legendary => new Color(1f, 0.6f, 0f),
        _                    => Color.white
    };
}

[System.Serializable]
public struct StatusEffect
{
    public StatusEffectType type;
    public float value;
    public float duration;
}

public enum ItemCategory
{
    Weapon,       // 무기
    Armor,        // 방어구
    Food,         // 음식
    Medicine,     // 의약품
    Material,     // 재료
    Tool,         // 도구
    Blueprint,    // 설계도
    Ammo,         // 탄약
    Misc          // 기타
}

public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public enum EquipSlot
{
    None,
    Head,
    Chest,
    Legs,
    Feet,
    Hands,
    MainHand,
    OffHand,
    Backpack
}

public enum StatusEffectType
{
    Poison,
    Bleed,
    Burn,
    Freeze,
    Regen,
    SpeedBoost,
    StrengthBoost,
    Stun
}
