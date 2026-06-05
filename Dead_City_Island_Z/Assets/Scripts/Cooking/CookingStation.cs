using UnityEngine;

/// <summary>요리대 (3D OnTriggerEnter)</summary>
public class CookingStation : MonoBehaviour, IInteractable
{
    [SerializeField] private CookingStationType stationType = CookingStationType.Campfire;
    [SerializeField] private GameObject fireEffect, smokeEffect;
    [SerializeField] private AudioSource fireAudio;
    [SerializeField] private AudioClip   fireClip;
    [SerializeField] private bool  requiresFuel = true;
    [SerializeField] private float maxFuelTime  = 300f;
    [SerializeField] private float currentFuel;
    [SerializeField] private ItemData woodItem;

    private bool _isLit;
    public string InteractPrompt => (requiresFuel && !_isLit) ? $"{stationType.ToKorean()} 불 붙이기" : $"{stationType.ToKorean()} 사용";

    private void Start() { if (!requiresFuel) { _isLit = true; currentFuel = maxFuelTime; } UpdateVisuals(); }

    private void Update()
    {
        if (!_isLit || !requiresFuel) return;
        currentFuel -= Time.deltaTime;
        if (currentFuel <= 0) { _isLit = false; currentFuel = 0; UpdateVisuals(); UIManager.Instance?.ShowNotification("🔥 모닥불이 꺼졌습니다"); }
    }

    // 3D OnTriggerEnter (PlayerController.DetectInteractable에서 처리)
    private void OnTriggerEnter(Collider other) { }

    public void Interact(PlayerController player)
    {
        if (requiresFuel && !_isLit) { TryLight(); return; }
        CookingSystem.Instance?.OpenCookingStation(stationType);
    }

    private void TryLight()
    {
        var inv = InventorySystem.Instance;
        if (inv != null && woodItem != null && inv.HasItem(woodItem, 1))
        { inv.RemoveItem(woodItem, 1); _isLit = true; currentFuel = maxFuelTime; UpdateVisuals(); UIManager.Instance?.ShowNotification("🔥 모닥불을 피웠습니다"); }
        else UIManager.Instance?.ShowNotification($"{woodItem?.itemNameKR ?? "연료"} 필요");
    }

    private void UpdateVisuals()
    {
        fireEffect?.SetActive(_isLit); smokeEffect?.SetActive(_isLit);
        if (fireAudio) { if (_isLit && fireClip) { fireAudio.clip = fireClip; fireAudio.Play(); } else fireAudio.Stop(); }
    }

    public float FuelRatio => maxFuelTime > 0 ? currentFuel / maxFuelTime : 0f;
    public bool  IsLit     => _isLit;
}
