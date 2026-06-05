using System;
using System.Collections;
using UnityEngine;

/// <summary>자원 노드 (3D OnTriggerEnter/Exit)</summary>
public class ResourceNode : MonoBehaviour, IInteractable
{
    [SerializeField] private ResourceNodeData nodeData;
    [SerializeField] private int currentDurability;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private GameObject harvestEffect, depleteEffect, depletedPrefab;
    [SerializeField] private float shakeAmount = 0.05f, shakeDuration = 0.3f;

    private bool    _isDepleted, _isHarvesting;
    private Vector3 _originalPos;
    private float   _respawnTimer;

    public static event Action<ResourceNode, ItemData, int> OnResourceHarvested;

    public string InteractPrompt => _isDepleted ? $"{nodeData?.nodeNameKR} (고갈)" : $"{nodeData?.nodeNameKR} 채집";

    private void Awake() { _originalPos = transform.position; if (nodeData) currentDurability = nodeData.maxDurability; }

    private void Update()
    {
        if (_isDepleted && nodeData != null && nodeData.canRespawn)
        { _respawnTimer -= Time.deltaTime; if (_respawnTimer <= 0) Respawn(); }
    }

    // 3D OnTriggerEnter/Exit
    private void OnTriggerEnter(Collider other) { /* PlayerController.DetectInteractable가 처리 */ }
    private void OnTriggerExit(Collider other)  { /* 동일 */ }

    public void Interact(PlayerController player)
    {
        if (_isDepleted || _isHarvesting || nodeData == null) return;
        var tool = InventorySystem.Instance?.GetEquipSlot(EquipSlot.MainHand)?.Item;
        if (nodeData.requiresTool && !IsCorrectTool(tool)) { UIManager.Instance?.ShowNotification($"{nodeData.requiredToolNameKR} 필요"); return; }
        StartCoroutine(HarvestRoutine(player));
    }

    private IEnumerator HarvestRoutine(PlayerController player)
    {
        _isHarvesting = true;
        if (harvestEffect) Instantiate(harvestEffect, transform.position, Quaternion.identity);
        StartCoroutine(ShakeAnim());
        yield return new WaitForSeconds(nodeData.harvestTime);

        float mult = 1f + ((SkillSystem.Instance?.GetLevel(nodeData.relatedSkill) ?? 1) - 1) * 0.05f;
        foreach (var drop in nodeData.drops)
        {
            if (UnityEngine.Random.value > drop.dropChance) continue;
            int amount = Mathf.RoundToInt(UnityEngine.Random.Range(drop.minAmount, drop.maxAmount + 1) * mult);
            if (amount <= 0) continue;
            if (!(InventorySystem.Instance?.AddItem(drop.item, amount) ?? false))
            {
                // 3D 월드에 드롭
                Vector2 r = UnityEngine.Random.insideUnitCircle * 1.5f;
                var go = Instantiate(drop.item.prefab, player.transform.position + new Vector3(r.x, 0.5f, r.y), Quaternion.identity);
                go.GetComponent<WorldItem>()?.Initialize(drop.item, amount);
            }
            OnResourceHarvested?.Invoke(this, drop.item, amount);
        }
        SkillSystem.Instance?.AddExp(nodeData.relatedSkill, nodeData.expPerHarvest);
        currentDurability--;
        if (currentDurability <= 0) Deplete();
        _isHarvesting = false;
    }

    private void Deplete()
    {
        _isDepleted = true; _respawnTimer = nodeData.respawnTime;
        if (depleteEffect) Instantiate(depleteEffect, transform.position, Quaternion.identity);
        if (meshRenderer) meshRenderer.enabled = false;
        if (depletedPrefab) Instantiate(depletedPrefab, transform.position, transform.rotation);
        GetComponent<Collider>().enabled = false;
    }

    private void Respawn()
    {
        _isDepleted = false; currentDurability = nodeData.maxDurability;
        if (meshRenderer) meshRenderer.enabled = true;
        GetComponent<Collider>().enabled = true;
    }

    private IEnumerator ShakeAnim()
    {
        float e = 0f;
        while (e < shakeDuration)
        {
            transform.position = _originalPos + new Vector3(UnityEngine.Random.Range(-shakeAmount, shakeAmount), 0, UnityEngine.Random.Range(-shakeAmount, shakeAmount));
            e += Time.deltaTime; yield return null;
        }
        transform.position = _originalPos;
    }

    private bool IsCorrectTool(ItemData tool)
    {
        if (tool == null) return false;
        foreach (var c in nodeData.validToolCategories) if (tool.category == c) return true;
        return false;
    }
}
