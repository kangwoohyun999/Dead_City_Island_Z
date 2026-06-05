using UnityEngine;

// ─── 인터페이스 ───────────────────────────────────────────────
// (IDamageable, IInteractable)

public interface IDamageable
{
    void TakeDamage(float damage, Vector3 hitPoint);
}

public interface IInteractable
{
    string InteractPrompt { get; }
    void Interact(PlayerController player);
}

// ─── 아래 Enum들은 다른 파일에 없는 것만 정의 ────────────────
// GameState    → GameManager.cs
// DamageType   → SurvivalStats.cs
// EquipSlot    → ItemData.cs
// SkillType    → CraftingSystem.cs
// GrowthStage  → FarmingSystem.cs
// ItemCategory → ItemData.cs
// CookingStationType → CookingSystem.cs
// NPCDialogueType    → NPCTrader.cs
// TradeOffer         → NPCTrader.cs

public enum DangerLevel { Safe, Low, Medium, High, Extreme }
public enum WorldType   { Island, City }
public enum MarkerType  { Player, Enemy, Loot, Base, Quest }
public enum AmmoType    { Pistol, Shotgun, Rifle, Arrow }

public enum TileType
{
    Unknown, Grass, GrassDark, Sand, Dirt,
    Water, Rock, Road, Concrete, Brick, Rubble, Wall
}

// ─── 공용 데이터 클래스 ───────────────────────────────────────

[System.Serializable]
public class ZoneData
{
    public string      zoneID;
    public string      zoneNameKR;
    public Vector2     center;       // XZ 평면 좌표 (x, z)
    public float       radius     = 20f;
    public DangerLevel dangerLevel = DangerLevel.Medium;
    public bool        IsSafeZone => dangerLevel == DangerLevel.Safe;
}

[System.Serializable]
public class MapMarker
{
    public string    markerID;
    public MarkerType type;
    public Vector2   worldPosition;   // x = 3D X, y = 3D Z
    public Color     color = Color.white;
    public bool      isDynamic;
    public Transform followTarget;
}

[System.Serializable]
public class EnemySpawnConfig
{
    public string      enemyID;
    public GameObject  prefab;
    public float       weight     = 1f;
    public DangerLevel minDanger  = DangerLevel.Low;
    public DangerLevel maxDanger  = DangerLevel.Extreme;
}

[System.Serializable]
public class PropEntry
{
    public GameObject prefab;
    public int        minCount;
    public int        maxCount;
    public bool       randomRotation = true;
}

[System.Serializable]
public class ChunkData
{
    public Vector2Int coord;
    public System.Collections.Generic.Dictionary<Vector2Int, TileType> tileTypes = new();
    public ChunkData(Vector2Int c) { coord = c; }
}

[System.Serializable]
public class WeaponStats
{
    public float    damage;
    public float    fireRate;
    public float    reloadTime;
    public float    spread;
    public float    recoil;
    public float    projectileSpeed;
    public int      magazineSize;
    public int      pelletCount = 1;
    public AmmoType ammoType;
}

public static class WeaponStatsDatabase
{
    public static WeaponStats Get(string weaponName) => new WeaponStats
    {
        damage = 15f, fireRate = 2f, reloadTime = 2f,
        magazineSize = 12, projectileSpeed = 20f, pelletCount = 1,
        ammoType = AmmoType.Pistol
    };
}

public static class GrowthStageExtensions
{
    public static string ToKorean(this GrowthStage g) => g switch
    {
        GrowthStage.Seedling  => "새싹",
        GrowthStage.Growing   => "성장중",
        GrowthStage.Mature    => "수확 가능",
        GrowthStage.Overripe  => "과숙",
        _                     => "?"
    };
}
