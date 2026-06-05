using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 농업 시스템 — 씨앗 심기 → 성장 → 수확
/// 섬(안전지대) 전용, 듀랑고 스타일 실시간 성장
/// </summary>
public class FarmingSystem : MonoBehaviour
{
    public static FarmingSystem Instance { get; private set; }

    [Header("농장 설정")]
    [SerializeField] private LayerMask farmableTileLayer;    // 밭 타일 레이어
    [SerializeField] private GameObject cropPrefab;          // 작물 시각화 프리팹
    [SerializeField] private float      wateringInterval = 60f;  // 물주기 필요 주기 (초)

    private Dictionary<Vector2Int, CropData> _crops = new();

    public static event Action<Vector2Int, CropData> OnCropPlanted;
    public static event Action<Vector2Int, CropData> OnCropGrown;
    public static event Action<Vector2Int>            OnCropHarvested;
    public static event Action<Vector2Int>            OnCropWithered;   // 물 안 주면 시들음

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Update()
    {
        UpdateAllCrops(Time.deltaTime);
    }

    // ─── 씨앗 심기 ───────────────────────────────────────────

    public bool PlantSeed(Vector2Int gridPos, CropDefinition cropDef, PlayerController player)
    {
        if (_crops.ContainsKey(gridPos))
        {
            UIManager.Instance?.ShowNotification("이미 작물이 심어져 있습니다");
            return false;
        }

        // 씨앗 아이템 소비
        var inv = InventorySystem.Instance;
        if (inv == null || !inv.HasItem(cropDef.seedItem, 1))
        {
            UIManager.Instance?.ShowNotification($"{cropDef.seedItem.itemNameKR}이(가) 없습니다");
            return false;
        }
        inv.RemoveItem(cropDef.seedItem, 1);

        // 작물 데이터 생성
        var crop = new CropData
        {
            gridPos      = gridPos,
            definition   = cropDef,
            stage        = GrowthStage.Seedling,
            growthTimer  = 0f,
            waterTimer   = wateringInterval,
            isWatered    = true,
            plantedTime  = Time.time
        };

        _crops[gridPos] = crop;
        SpawnCropVisual(crop);
        SkillSystem.Instance?.AddExp(SkillType.Farming, 2f);

        OnCropPlanted?.Invoke(gridPos, crop);
        UIManager.Instance?.ShowNotification($"{cropDef.cropNameKR} 심기 완료");
        return true;
    }

    // ─── 물주기 ──────────────────────────────────────────────

    public bool WaterCrop(Vector2Int gridPos, PlayerController player)
    {
        if (!_crops.TryGetValue(gridPos, out var crop)) return false;
        if (crop.isWithered)
        {
            UIManager.Instance?.ShowNotification("시든 작물에는 물을 줄 수 없습니다");
            return false;
        }

        // 물 아이템 소비 (물통)
        // TODO: 특정 아이템 없이 손으로 물주기 허용 여부 설정
        crop.isWatered  = true;
        crop.waterTimer = wateringInterval;
        SkillSystem.Instance?.AddExp(SkillType.Farming, 1f);

        UIManager.Instance?.ShowNotification("물을 주었습니다");
        return true;
    }

    // ─── 수확 ────────────────────────────────────────────────

    public bool Harvest(Vector2Int gridPos, PlayerController player)
    {
        if (!_crops.TryGetValue(gridPos, out var crop)) return false;
        if (crop.stage != GrowthStage.Mature && crop.stage != GrowthStage.Overripe)
        {
            UIManager.Instance?.ShowNotification("아직 수확할 때가 아닙니다");
            return false;
        }

        var inv = InventorySystem.Instance;
        if (inv == null) return false;

        // 수확량 계산 (스킬 보정)
        int skillLevel = SkillSystem.Instance?.GetLevel(SkillType.Farming) ?? 1;
        float mult = 1f + (skillLevel - 1) * 0.08f;

        int yield = Mathf.RoundToInt(
            UnityEngine.Random.Range(crop.definition.minYield, crop.definition.maxYield + 1) * mult);

        // 과숙 상태면 수확량 감소
        if (crop.stage == GrowthStage.Overripe) yield = Mathf.Max(1, yield / 2);

        bool added = inv.AddItem(crop.definition.harvestItem, yield);
        if (!added)
        {
            UIManager.Instance?.ShowNotification("인벤토리가 꽉 찼습니다");
            return false;
        }

        // 씨앗 일부 반환 (확률)
        if (UnityEngine.Random.value < crop.definition.seedReturnChance)
            inv.AddItem(crop.definition.seedItem, 1);

        SkillSystem.Instance?.AddExp(SkillType.Farming, crop.definition.harvestExp);
        UIManager.Instance?.ShowNotification(
            $"{crop.definition.harvestItem.itemNameKR} x{yield} 수확!");

        RemoveCropVisual(gridPos);
        _crops.Remove(gridPos);
        OnCropHarvested?.Invoke(gridPos);
        return true;
    }

    // ─── 성장 업데이트 ───────────────────────────────────────

    private void UpdateAllCrops(float dt)
    {
        var toRemove = new List<Vector2Int>();

        foreach (var kv in _crops)
        {
            var crop = kv.Value;
            if (crop.isWithered) continue;

            // 물 타이머
            crop.waterTimer -= dt;
            if (crop.waterTimer <= 0)
            {
                crop.isWatered = false;
                // 물이 없으면 성장 정지 → 일정 시간 후 시듦
                if (crop.waterTimer < -wateringInterval)
                {
                    crop.isWithered = true;
                    UpdateCropVisual(crop);
                    OnCropWithered?.Invoke(kv.Key);
                    continue;
                }
            }

            // 성장 (물 있을 때만)
            if (crop.isWatered)
                crop.growthTimer += dt;

            // 단계 전환
            var def = crop.definition;
            GrowthStage newStage = crop.growthTimer switch
            {
                _ when crop.growthTimer < def.seedlingDuration                   => GrowthStage.Seedling,
                _ when crop.growthTimer < def.seedlingDuration + def.growingDuration => GrowthStage.Growing,
                _ when crop.growthTimer < def.seedlingDuration + def.growingDuration + def.matureDuration => GrowthStage.Mature,
                _                                                                 => GrowthStage.Overripe
            };

            if (newStage != crop.stage)
            {
                crop.stage = newStage;
                UpdateCropVisual(crop);
                OnCropGrown?.Invoke(kv.Key, crop);

                if (newStage == GrowthStage.Mature)
                    UIManager.Instance?.ShowNotification($"{def.cropNameKR} 수확 가능!");
            }
        }

        foreach (var k in toRemove) _crops.Remove(k);
    }

    // ─── 시각화 ──────────────────────────────────────────────

    private Dictionary<Vector2Int, GameObject> _cropVisuals = new();

    private void SpawnCropVisual(CropData crop)
    {
        if (cropPrefab == null) return;
        var go = Instantiate(cropPrefab,
            new Vector3(crop.gridPos.x + 0.5f, crop.gridPos.y + 0.5f, 0),
            Quaternion.identity);
        _cropVisuals[crop.gridPos] = go;
        UpdateCropVisual(crop);
    }

    private void UpdateCropVisual(CropData crop)
    {
        if (!_cropVisuals.TryGetValue(crop.gridPos, out var go)) return;
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        if (crop.isWithered)      { sr.color = new Color(0.4f, 0.3f, 0.1f); return; }

        sr.color = crop.stage switch
        {
            GrowthStage.Seedling  => new Color(0.8f, 1f, 0.7f),
            GrowthStage.Growing   => new Color(0.5f, 0.9f, 0.4f),
            GrowthStage.Mature    => Color.white,
            GrowthStage.Overripe  => new Color(0.9f, 0.7f, 0.3f),
            _                     => Color.white
        };

        // 크기도 단계별로
        float scale = crop.stage switch
        {
            GrowthStage.Seedling  => 0.4f,
            GrowthStage.Growing   => 0.65f,
            GrowthStage.Mature    => 1.0f,
            GrowthStage.Overripe  => 1.1f,
            _                     => 1f
        };
        go.transform.localScale = Vector3.one * scale;

        // 스프라이트 변경 (정의에 있을 때)
        if (crop.definition.stageSprites != null &&
            (int)crop.stage < crop.definition.stageSprites.Length &&
            crop.definition.stageSprites[(int)crop.stage] != null)
            sr.sprite = crop.definition.stageSprites[(int)crop.stage];
    }

    private void RemoveCropVisual(Vector2Int pos)
    {
        if (_cropVisuals.TryGetValue(pos, out var go))
        {
            Destroy(go);
            _cropVisuals.Remove(pos);
        }
    }

    // ─── 조회 ────────────────────────────────────────────────

    public CropData GetCrop(Vector2Int pos)
        => _crops.TryGetValue(pos, out var c) ? c : null;

    public IReadOnlyDictionary<Vector2Int, CropData> AllCrops => _crops;
}

// ─── 데이터 ──────────────────────────────────────────────────

[Serializable]
public class CropData
{
    public Vector2Int     gridPos;
    public CropDefinition definition;
    public GrowthStage    stage;
    public float          growthTimer;
    public float          waterTimer;
    public bool           isWatered;
    public bool           isWithered;
    public float          plantedTime;
}

[CreateAssetMenu(fileName = "NewCrop", menuName = "LastShore/Farming/CropDefinition")]
public class CropDefinition : ScriptableObject
{
    [Header("기본")]
    public string   cropID;
    public string   cropNameKR;
    public ItemData seedItem;
    public ItemData harvestItem;

    [Header("성장 시간 (초)")]
    public float seedlingDuration = 60f;
    public float growingDuration  = 120f;
    public float matureDuration   = 180f;   // 이 시간 지나면 과숙

    [Header("수확")]
    public int   minYield          = 2;
    public int   maxYield          = 5;
    [Range(0f,1f)]
    public float seedReturnChance  = 0.5f;
    public float harvestExp        = 10f;

    [Header("스프라이트")]
    public Sprite[] stageSprites;  // [0]=씨앗, [1]=성장, [2]=성숙, [3]=과숙
}

public enum GrowthStage { Seedling, Growing, Mature, Overripe }
