using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 전체 아이템 레지스트리 — 런타임에서 ID로 아이템 조회
/// </summary>
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "LastShore/Item/ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    public static ItemDatabase Instance { get; private set; }

    [SerializeField] private List<ItemData> items = new();
    private Dictionary<string, ItemData> _lookup;

    private void OnEnable()
    {
        Instance = this;
        BuildLookup();
    }

    private void BuildLookup()
    {
        _lookup = new Dictionary<string, ItemData>();
        foreach (var item in items)
            if (item != null && !string.IsNullOrEmpty(item.name))
                _lookup[item.name] = item;
    }

    public ItemData Get(string id)
        => _lookup != null && _lookup.TryGetValue(id, out var item) ? item : null;

    public List<ItemData> GetAll() => items;

    public List<ItemData> GetByCategory(ItemCategory category)
        => items.FindAll(i => i != null && i.category == category);

    public void Register(ItemData item)
    {
        if (!items.Contains(item))
        {
            items.Add(item);
            _lookup[item.name] = item;
        }
    }
}


// ══════════════════════════════════════════════════════════
// 에디터 전용: 기본 30종 아이템 ScriptableObject 일괄 생성
// ══════════════════════════════════════════════════════════
#if UNITY_EDITOR
public static class ItemDatabaseGenerator
{
    private static readonly string SAVE_PATH = "Assets/ScriptableObjects/Items/";

    [MenuItem("LastShore/Generate Default Items")]
    public static void GenerateDefaultItems()
    {
        System.IO.Directory.CreateDirectory(SAVE_PATH + "Materials/");
        System.IO.Directory.CreateDirectory(SAVE_PATH + "Weapons/");
        System.IO.Directory.CreateDirectory(SAVE_PATH + "Armor/");
        System.IO.Directory.CreateDirectory(SAVE_PATH + "Food/");
        System.IO.Directory.CreateDirectory(SAVE_PATH + "Medicine/");
        System.IO.Directory.CreateDirectory(SAVE_PATH + "Tools/");

        var created = new List<ItemData>();
        created.AddRange(CreateMaterials());
        created.AddRange(CreateWeapons());
        created.AddRange(CreateArmor());
        created.AddRange(CreateFood());
        created.AddRange(CreateMedicine());
        created.AddRange(CreateTools());

        // DB에 등록
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(
            "Assets/ScriptableObjects/ItemDatabase.asset");
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<ItemDatabase>();
            AssetDatabase.CreateAsset(db, "Assets/ScriptableObjects/ItemDatabase.asset");
        }

        foreach (var item in created)
            db.Register(item);

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ItemDatabase] {created.Count}개 아이템 생성 완료");
    }

    // ─── 재료 (10종) ─────────────────────────────────────────

    private static List<ItemData> CreateMaterials()
    {
        return new List<ItemData>
        {
            Make("목재",        "Wood",        ItemCategory.Material, 0.5f,  99, "나무를 채집해 얻는 기본 건축 재료"),
            Make("나뭇가지",    "Branch",      ItemCategory.Material, 0.1f,  99, "가벼운 나뭇가지. 다양하게 활용 가능"),
            Make("돌",          "Stone",       ItemCategory.Material, 1.0f,  99, "채석 또는 지표에서 채집한 돌"),
            Make("부싯돌",      "Flint",       ItemCategory.Material, 0.3f,  30, "날카로운 부싯돌. 도구 제작 필수"),
            Make("철광석",      "IronOre",     ItemCategory.Material, 2.0f,  30, "제련하면 철 주괴가 됨"),
            Make("철 주괴",     "IronIngot",   ItemCategory.Material, 1.5f,  30, "제련된 철. 무기/방어구 제작 재료"),
            Make("식물 섬유",   "PlantFiber",  ItemCategory.Material, 0.1f,  99, "옷, 로프 제작에 사용"),
            Make("가죽",        "Leather",     ItemCategory.Material, 0.5f,  50, "동물 가죽. 방어구 제작 재료"),
            Make("천 조각",     "Cloth",       ItemCategory.Material, 0.1f,  99, "붕대, 의류 제작에 사용"),
            Make("연료통",      "FuelCan",     ItemCategory.Material, 2.0f,  10, "발전기, 차량에 사용하는 연료"),
        };
    }

    // ─── 무기 (5종) ──────────────────────────────────────────

    private static List<ItemData> CreateWeapons()
    {
        var list = new List<ItemData>();

        var stick = Make("나뭇가지 창", "StickSpear", ItemCategory.Weapon, 1.0f, 1,
            "나뭇가지로 만든 원시적인 창. 내구도가 낮다");
        stick.isEquippable  = true;
        stick.equipSlot     = EquipSlot.MainHand;
        stick.attackDamage  = 8f;
        stick.attackSpeed   = 0.8f;
        stick.attackRange   = 1.5f;
        stick.hasDurability = true;
        stick.maxDurability = 30f;
        stick.rarity        = ItemRarity.Common;
        list.Add(Save(stick, "Weapons/"));

        var ironSword = Make("철 단검", "IronDagger", ItemCategory.Weapon, 1.5f, 1,
            "철로 만든 단검. 빠른 공격이 특징");
        ironSword.isEquippable  = true;
        ironSword.equipSlot     = EquipSlot.MainHand;
        ironSword.attackDamage  = 18f;
        ironSword.attackSpeed   = 1.3f;
        ironSword.attackRange   = 1.0f;
        ironSword.hasDurability = true;
        ironSword.maxDurability = 80f;
        ironSword.rarity        = ItemRarity.Uncommon;
        list.Add(Save(ironSword, "Weapons/"));

        var baseballBat = Make("야구 방망이", "BaseballBat", ItemCategory.Weapon, 1.2f, 1,
            "도시에서 찾은 알루미늄 야구 방망이");
        baseballBat.isEquippable  = true;
        baseballBat.equipSlot     = EquipSlot.MainHand;
        baseballBat.attackDamage  = 22f;
        baseballBat.attackSpeed   = 0.9f;
        baseballBat.attackRange   = 1.2f;
        baseballBat.hasDurability = true;
        baseballBat.maxDurability = 120f;
        baseballBat.rarity        = ItemRarity.Uncommon;
        list.Add(Save(baseballBat, "Weapons/"));

        var pistol = Make("권총", "Pistol", ItemCategory.Weapon, 0.8f, 1,
            "9mm 권총. 근거리 전투에 유용");
        pistol.isEquippable  = true;
        pistol.equipSlot     = EquipSlot.MainHand;
        pistol.attackDamage  = 35f;
        pistol.attackSpeed   = 1.5f;
        pistol.attackRange   = 8f;
        pistol.hasDurability = true;
        pistol.maxDurability = 200f;
        pistol.rarity        = ItemRarity.Rare;
        list.Add(Save(pistol, "Weapons/"));

        var shotgun = Make("샷건", "Shotgun", ItemCategory.Weapon, 3.0f, 1,
            "산탄총. 한 번에 여러 목표를 공격");
        shotgun.isEquippable  = true;
        shotgun.equipSlot     = EquipSlot.MainHand;
        shotgun.attackDamage  = 60f;
        shotgun.attackSpeed   = 0.4f;
        shotgun.attackRange   = 5f;
        shotgun.hasDurability = true;
        shotgun.maxDurability = 150f;
        shotgun.rarity        = ItemRarity.Rare;
        list.Add(Save(shotgun, "Weapons/"));

        return list;
    }

    // ─── 방어구 (4종) ─────────────────────────────────────────

    private static List<ItemData> CreateArmor()
    {
        var list = new List<ItemData>();

        var clothShirt = Make("천 셔츠",    "ClothShirt",   ItemCategory.Armor, 0.5f, 1, "기본적인 천 상의");
        clothShirt.isEquippable = true; clothShirt.equipSlot = EquipSlot.Chest;
        clothShirt.defense = 3f; clothShirt.hasDurability = true; clothShirt.maxDurability = 50f;
        list.Add(Save(clothShirt, "Armor/"));

        var leatherArmor = Make("가죽 갑옷",  "LeatherArmor", ItemCategory.Armor, 2.5f, 1, "가죽으로 만든 방어구");
        leatherArmor.isEquippable = true; leatherArmor.equipSlot = EquipSlot.Chest;
        leatherArmor.defense = 12f; leatherArmor.hasDurability = true; leatherArmor.maxDurability = 100f;
        leatherArmor.rarity = ItemRarity.Uncommon;
        list.Add(Save(leatherArmor, "Armor/"));

        var ironHelmet = Make("철 투구",     "IronHelmet",   ItemCategory.Armor, 2.0f, 1, "철로 만든 투구");
        ironHelmet.isEquippable = true; ironHelmet.equipSlot = EquipSlot.Head;
        ironHelmet.defense = 8f; ironHelmet.hasDurability = true; ironHelmet.maxDurability = 80f;
        ironHelmet.rarity = ItemRarity.Uncommon;
        list.Add(Save(ironHelmet, "Armor/"));

        var backpack = Make("배낭",          "Backpack",     ItemCategory.Armor, 1.0f, 1, "최대 무게를 15kg 증가");
        backpack.isEquippable = true; backpack.equipSlot = EquipSlot.Backpack;
        backpack.rarity = ItemRarity.Uncommon;
        list.Add(Save(backpack, "Armor/"));

        return list;
    }

    // ─── 음식 (5종) ──────────────────────────────────────────

    private static List<ItemData> CreateFood()
    {
        var list = new List<ItemData>();

        var berry = MakeConsumable("야생 베리",    "WildBerry",    0.1f, 10, 0,  5f,  0f);
        berry.description = "새콤달콤한 야생 베리. 약간의 허기를 채워준다";
        list.Add(Save(berry, "Food/"));

        var cookedMeat = MakeConsumable("구운 고기",   "CookedMeat",   0.3f, 25, 5f, 30f, 0f);
        cookedMeat.description = "불에 구운 고기. 풍부한 영양을 제공한다";
        cookedMeat.rarity = ItemRarity.Common;
        list.Add(Save(cookedMeat, "Food/"));

        var water = MakeConsumable("물통",          "WaterBottle",  0.5f,  0, 0f,  0f, 40f);
        water.description = "깨끗한 물. 갈증을 해소한다";
        list.Add(Save(water, "Food/"));

        var energyDrink = MakeConsumable("에너지 드링크", "EnergyDrink", 0.3f, 10, 0f, 10f, 20f);
        energyDrink.description = "도시에서 발견한 에너지 드링크. 스태미나도 회복";
        energyDrink.staminaRestore = 30f;
        energyDrink.rarity = ItemRarity.Uncommon;
        list.Add(Save(energyDrink, "Food/"));

        var cannedFood = MakeConsumable("통조림",      "CannedFood",   0.4f, 20, 0f, 25f,  5f);
        cannedFood.description = "유통기한이 긴 통조림. 도시 탐색에서 자주 발견된다";
        list.Add(Save(cannedFood, "Food/"));

        return list;
    }

    // ─── 의약품 (3종) ────────────────────────────────────────

    private static List<ItemData> CreateMedicine()
    {
        var list = new List<ItemData>();

        var bandage = MakeConsumable("붕대",       "Bandage",      0.1f, 10, 20f, 0f, 0f);
        bandage.description = "출혈을 멈추고 체력을 회복한다";
        list.Add(Save(bandage, "Medicine/"));

        var firstAid = MakeConsumable("구급상자",  "FirstAidKit",  0.5f,  5, 60f, 0f, 0f);
        firstAid.description = "응급처치 세트. 많은 체력을 회복한다";
        firstAid.rarity = ItemRarity.Uncommon;
        list.Add(Save(firstAid, "Medicine/"));

        var antibiotics = MakeConsumable("항생제",  "Antibiotics",  0.1f,  5, 10f, 0f, 0f);
        antibiotics.description = "감염 및 독 상태를 치료한다";
        antibiotics.rarity = ItemRarity.Rare;
        list.Add(Save(antibiotics, "Medicine/"));

        return list;
    }

    // ─── 도구 (3종) ──────────────────────────────────────────

    private static List<ItemData> CreateTools()
    {
        var list = new List<ItemData>();

        var stonePick = Make("돌 곡괭이",   "StonePick",   ItemCategory.Tool, 1.5f, 1, "돌과 광물을 채집하는 기본 도구");
        stonePick.isEquippable = true; stonePick.equipSlot = EquipSlot.MainHand;
        stonePick.hasDurability = true; stonePick.maxDurability = 40f;
        stonePick.attackDamage = 6f;
        list.Add(Save(stonePick, "Tools/"));

        var stoneAxe = Make("돌 도끼",     "StoneAxe",    ItemCategory.Tool, 1.0f, 1, "나무를 채집하는 기본 도구");
        stoneAxe.isEquippable = true; stoneAxe.equipSlot = EquipSlot.MainHand;
        stoneAxe.hasDurability = true; stoneAxe.maxDurability = 40f;
        stoneAxe.attackDamage = 8f;
        list.Add(Save(stoneAxe, "Tools/"));

        var ironAxe = Make("철 도끼",      "IronAxe",     ItemCategory.Tool, 1.8f, 1, "철로 만든 도끼. 채집 효율이 높다");
        ironAxe.isEquippable = true; ironAxe.equipSlot = EquipSlot.MainHand;
        ironAxe.hasDurability = true; ironAxe.maxDurability = 100f;
        ironAxe.attackDamage = 14f; ironAxe.rarity = ItemRarity.Uncommon;
        list.Add(Save(ironAxe, "Tools/"));

        return list;
    }

    // ─── 유틸 ────────────────────────────────────────────────

    private static ItemData Make(string nameKR, string id, ItemCategory cat,
        float weight, int maxStack, string desc)
    {
        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemNameKR  = nameKR;
        item.itemName    = id;
        item.category    = cat;
        item.weight      = weight;
        item.maxStackSize= maxStack;
        item.canStack    = maxStack > 1;
        item.description = desc;
        item.rarity      = ItemRarity.Common;
        return item;
    }

    private static ItemData MakeConsumable(string nameKR, string id,
        float weight, int maxStack, float hp, float hunger, float thirst)
    {
        var item = Make(nameKR, id, ItemCategory.Food, weight, maxStack, "");
        item.isConsumable   = true;
        item.healthRestore  = hp;
        item.hungerRestore  = hunger;
        item.thirstRestore  = thirst;
        return item;
    }

    private static ItemData Save(ItemData item, string subfolder)
    {
        string path = $"{SAVE_PATH}{subfolder}{item.itemName}.asset";
        AssetDatabase.CreateAsset(item, path);
        return item;
    }
}
#endif
