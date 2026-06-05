using UnityEngine;

/// <summary>밭 타일 (3D MeshRenderer, 3D Trigger)</summary>
public class FarmPlot : MonoBehaviour, IInteractable
{
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private MeshRenderer soilRenderer;
    [SerializeField] private GameObject cropVisual, waterIndicator;
    [SerializeField] private TMPro.TextMeshPro statusLabel;
    [SerializeField] private Material drySoilMaterial, wetSoilMaterial;

    private CropData _cropData;

    public string InteractPrompt
    {
        get
        {
            _cropData = FarmingSystem.Instance?.GetCrop(gridPosition);
            if (_cropData == null) return "씨앗 심기";
            if (_cropData.isWithered) return "시든 작물 제거";
            if (_cropData.stage == GrowthStage.Mature || _cropData.stage == GrowthStage.Overripe) return "수확하기";
            if (!_cropData.isWatered) return "물 주기";
            return $"{_cropData.definition?.cropNameKR} ({_cropData.stage})";
        }
    }

    private void Update() => RefreshVisuals();

    // 3D Trigger (감지는 PlayerController.DetectInteractable에서 처리)
    private void OnTriggerEnter(Collider other) { }

    public void Interact(PlayerController player)
    {
        var fs = FarmingSystem.Instance;
        if (fs == null) return;
        _cropData = fs.GetCrop(gridPosition);
        if (_cropData == null)            { UIManager.Instance?.ShowNotification("씨앗 선택 UI"); return; }
        if (_cropData.isWithered)         { UIManager.Instance?.ShowNotification("시든 작물 제거"); return; }
        if (_cropData.stage == GrowthStage.Mature || _cropData.stage == GrowthStage.Overripe) { fs.Harvest(gridPosition, player); return; }
        if (!_cropData.isWatered)         fs.WaterCrop(gridPosition, player);
    }

    private void RefreshVisuals()
    {
        _cropData = FarmingSystem.Instance?.GetCrop(gridPosition);
        // 3D 머티리얼 변경
        if (soilRenderer && drySoilMaterial && wetSoilMaterial)
            soilRenderer.material = (_cropData?.isWatered ?? false) ? wetSoilMaterial : drySoilMaterial;
        if (waterIndicator) waterIndicator.SetActive(_cropData != null && !_cropData.isWatered && !_cropData.isWithered);
        if (cropVisual)
        {
            if (_cropData == null) { cropVisual.SetActive(false); return; }
            cropVisual.SetActive(true);
            float scale = _cropData.stage switch { GrowthStage.Seedling=>0.3f, GrowthStage.Growing=>0.6f, GrowthStage.Mature=>1.0f, GrowthStage.Overripe=>1.1f, _=>1f };
            cropVisual.transform.localScale = Vector3.one * scale;
        }
        if (statusLabel)
            statusLabel.text = _cropData == null ? "" : _cropData.isWithered ? "💀" :
                _cropData.stage switch { GrowthStage.Seedling=>"🌱", GrowthStage.Growing=>"🌿", GrowthStage.Mature=>"🌾", GrowthStage.Overripe=>"⚠️", _=>"" };
    }

    public void SetGridPosition(Vector2Int pos) => gridPosition = pos;
}
