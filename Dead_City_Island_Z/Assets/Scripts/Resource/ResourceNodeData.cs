using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자원 노드 ScriptableObject — 나무/돌/광물/식물 등
/// </summary>
[CreateAssetMenu(fileName = "NewResourceNode", menuName = "LastShore/Resource/NodeData")]
public class ResourceNodeData : ScriptableObject
{
    [Header("기본 정보")]
    public string   nodeID;
    public string   nodeName;
    public string   nodeNameKR;
    public NodeType nodeType    = NodeType.Tree;
    [TextArea(1, 3)]
    public string   description;

    [Header("내구도 / 재생성")]
    public int   maxDurability  = 3;          // 채집 횟수
    public bool  canRespawn     = true;
    public float respawnTime    = 300f;       // 초 (기본 5분)

    [Header("채집 설정")]
    public float harvestTime    = 1.5f;       // 1회 채집 소요 시간(초)
    public bool  requiresTool   = false;
    public string requiredToolNameKR = "";
    public List<ItemCategory> validToolCategories = new();

    [Header("드롭 아이템")]
    public List<ResourceDrop> drops = new();

    [Header("스킬")]
    public SkillType relatedSkill    = SkillType.Gathering;
    public float     expPerHarvest   = 5f;

    [Header("환경 조건")]
    public BiomeType requiredBiome   = BiomeType.Any;
}

// ─── 드롭 구조체 ─────────────────────────────────────────────

[System.Serializable]
public class ResourceDrop
{
    public ItemData item;
    [Range(0f, 1f)]
    public float    dropChance  = 1f;
    public int      minAmount   = 1;
    public int      maxAmount   = 3;
}

// ─── 열거형 ──────────────────────────────────────────────────

public enum NodeType
{
    Tree,           // 나무 — 목재, 나뭇가지, 나뭇잎
    Rock,           // 돌 — 돌, 부싯돌
    MetalOre,       // 금속 광석 — 철광석, 구리
    RareOre,        // 희귀 광석 — 금, 티타늄
    Bush,           // 덤불 — 베리, 잎
    Plant,          // 식물 — 약초, 섬유
    Mushroom,       // 버섯
    Ruins,          // 폐허 — 잡동사니, 설계도
    Crate,          // 상자 — 랜덤 전리품
    Vehicle,        // 차량 잔해 — 금속, 연료
    Corpse          // 시체 — 탄약, 보급품
}

public enum BiomeType
{
    Any,
    Forest,
    Beach,
    Mountain,
    Urban,          // 도시
    Underground
}
