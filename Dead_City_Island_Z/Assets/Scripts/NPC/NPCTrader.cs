using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>NPC 상인 (3D OnTriggerEnter + Billboard 연동)</summary>
public class NPCTrader : MonoBehaviour, IInteractable
{
    [SerializeField] private NPCData npcData;
    [SerializeField] private float   dialogueRange = 2f;
    [SerializeField] private GameObject speechBubble;  // World Space Canvas + Billboard.cs
    [SerializeField] private TMPro.TextMeshProUGUI speechText;

    private List<TradeOffer> _offers = new();
    private int   _lastRefreshDay = -1;
    private float _favorability   = 0f;
    private const float MAX_FAVOR = 100f;

    public static event Action<NPCTrader>        OnTradeOpened;
    public static event Action<TradeOffer, bool> OnTradeCompleted;
    public static event Action<NPCTrader, float> OnFavorabilityChanged;

    public string InteractPrompt => $"💬 {npcData?.npcNameKR ?? "NPC"}와 대화";
    public NPCData Data          => npcData;
    public float   Favorability  => _favorability;
    public IReadOnlyList<TradeOffer> CurrentOffers => _offers;

    private void Start() { RefreshOffers(); GameManager.OnDayChanged += d => { if (d != _lastRefreshDay) { _lastRefreshDay = d; RefreshOffers(); } }; if (speechBubble) speechBubble.SetActive(false); }

    // 3D OnTriggerEnter
    private void OnTriggerEnter(Collider other) { if (other.CompareTag("Player")) ShowSpeech(npcData?.GetRandomDialogue(NPCDialogueType.DayGreeting)); }
    private void OnTriggerExit(Collider other)  { if (other.CompareTag("Player")) HideSpeech(); }

    public void Interact(PlayerController player) { ShowSpeech(npcData?.GetRandomDialogue(NPCDialogueType.Greeting)); OnTradeOpened?.Invoke(this); }

    public bool ExecuteTrade(TradeOffer offer, int qty = 1)
    {
        if (offer == null || offer.stock < qty) return false;
        var inv = InventorySystem.Instance; if (inv == null) return false;
        int needed = offer.requiredAmount * qty;
        if (!inv.HasItem(offer.requiredItem, needed)) { UIManager.Instance?.ShowNotification($"{offer.requiredItem.itemNameKR} x{needed} 부족"); return false; }
        if (inv.CurrentWeight + offer.offeredItem.weight * offer.offeredAmount * qty > inv.MaxWeight) { UIManager.Instance?.ShowNotification("무게 초과"); return false; }
        inv.RemoveItem(offer.requiredItem, needed);
        inv.AddItem(offer.offeredItem, offer.offeredAmount * qty);
        offer.stock -= qty;
        ChangeFavorability(2f + (offer.isSpecial ? 5f : 0f));
        OnTradeCompleted?.Invoke(offer, true);
        UIManager.Instance?.ShowNotification($"교역 완료: {offer.offeredItem.itemNameKR} x{offer.offeredAmount * qty}");
        ShowSpeech(npcData?.GetRandomDialogue(NPCDialogueType.Trade));
        return true;
    }

    private void RefreshOffers()
    {
        _offers.Clear(); if (npcData == null) return;
        float bonus = _favorability / MAX_FAVOR;
        foreach (var t in npcData.tradeTemplates)
        {
            if (UnityEngine.Random.value > t.availabilityChance) continue;
            int stock = Mathf.RoundToInt(UnityEngine.Random.Range(t.minStock, t.maxStock+1) * (1f + bonus * 0.3f));
            float ratio = t.baseExchangeRatio * (1f - bonus * 0.2f);
            _offers.Add(new TradeOffer { offeredItem=t.offeredItem, offeredAmount=1, requiredItem=t.requiredItem, requiredAmount=Mathf.Max(1,Mathf.RoundToInt(t.baseRequiredAmount*ratio)), stock=stock, isSpecial=t.isSpecial });
        }
    }

    public void ChangeFavorability(float delta)
    {
        float prev = _favorability;
        _favorability = Mathf.Clamp(_favorability + delta, 0f, MAX_FAVOR);
        if (Mathf.Abs(_favorability - prev) > 0.01f)
        {
            OnFavorabilityChanged?.Invoke(this, _favorability);
            if (prev < 30f && _favorability >= 30f) UIManager.Instance?.ShowNotification($"💛 {npcData?.npcNameKR}와 친해졌습니다!");
            else if (prev < 70f && _favorability >= 70f) UIManager.Instance?.ShowNotification($"💚 {npcData?.npcNameKR}가 특별 물건을 제공합니다!");
        }
    }

    private void ShowSpeech(string text)
    {
        if (speechBubble == null || string.IsNullOrEmpty(text)) return;
        speechBubble.SetActive(true); if (speechText) speechText.text = text;
        CancelInvoke(nameof(HideSpeech)); Invoke(nameof(HideSpeech), 3f);
    }
    private void HideSpeech() => speechBubble?.SetActive(false);

#if UNITY_EDITOR
    private void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, dialogueRange); }
#endif
}

// ─── NPC 보조 데이터 클래스 ──────────────────────────────────

[UnityEngine.CreateAssetMenu(fileName = "NewNPC", menuName = "LastShore/NPC/NPCData")]
public class NPCData : UnityEngine.ScriptableObject
{
    [UnityEngine.Header("기본")]
    public string npcID;
    public string npcNameKR;
    public UnityEngine.Sprite portrait;
    [UnityEngine.TextArea(1,3)] public string backstory;

    [UnityEngine.Header("대화")]
    public System.Collections.Generic.List<NPCDialogueLine> dialogues = new();

    [UnityEngine.Header("교역 템플릿")]
    public System.Collections.Generic.List<TradeTemplate> tradeTemplates = new();

    public string GetRandomDialogue(NPCDialogueType type)
    {
        var lines = dialogues.FindAll(d => d.type == type);
        if (lines.Count == 0) return "";
        return lines[UnityEngine.Random.Range(0, lines.Count)].text;
    }
}

[System.Serializable]
public class NPCDialogueLine
{
    public NPCDialogueType type;
    [UnityEngine.TextArea(1,2)] public string text;
}

[System.Serializable]
public class TradeTemplate
{
    public ItemData offeredItem;
    public ItemData requiredItem;
    public int      baseRequiredAmount = 5;
    public float    baseExchangeRatio  = 1f;
    [UnityEngine.Range(0f,1f)] public float availabilityChance = 0.7f;
    public int      minStock = 1;
    public int      maxStock = 5;
    public bool     isSpecial = false;
}

[System.Serializable]
public class TradeOffer
{
    public ItemData offeredItem;
    public int      offeredAmount;
    public ItemData requiredItem;
    public int      requiredAmount;
    public int      stock;
    public bool     isSpecial;
}

public enum NPCDialogueType
{
    Greeting, DayGreeting, NightGreeting, Trade, Farewell, Hint, Quest
}
