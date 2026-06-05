using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 전체 UI 관리 싱글턴
/// HUD, 인벤토리, 제작, 맵 등
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // ─── HUD ────────────────────────────────────────────────
    [Header("HUD")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider hungerSlider;
    [SerializeField] private Slider thirstSlider;
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private Image temperatureIcon;

    [Header("HUD 컬러")]
    [SerializeField] private Color healthColorHigh   = new(0.2f, 0.9f, 0.2f);
    [SerializeField] private Color healthColorMedium = new(0.9f, 0.9f, 0.2f);
    [SerializeField] private Color healthColorLow    = new(0.9f, 0.2f, 0.2f);

    // ─── 핫바 ────────────────────────────────────────────────
    [Header("핫바")]
    [SerializeField] private HotbarSlotUI[] hotbarSlots;
    [SerializeField] private Image selectedSlotHighlight;

    // ─── 패널들 ──────────────────────────────────────────────
    [Header("패널")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject craftingPanel;
    [SerializeField] private GameObject mapPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject deathPanel;

    // ─── 상호작용 프롬프트 ───────────────────────────────────
    [Header("상호작용")]
    [SerializeField] private GameObject interactPromptObj;
    [SerializeField] private TextMeshProUGUI interactPromptText;

    // ─── 알림 ────────────────────────────────────────────────
    [Header("알림")]
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private float notificationDuration = 2.5f;
    private Coroutine _notifCoroutine;

    // ─── 상태 ────────────────────────────────────────────────
    public bool IsInventoryOpen  { get; private set; }
    public bool IsCraftingOpen   { get; private set; }
    public bool IsMapOpen        { get; private set; }
    public bool IsAnyPanelOpen   => IsInventoryOpen || IsCraftingOpen || IsMapOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        SurvivalStats.OnHealthChanged      += UpdateHealthBar;
        SurvivalStats.OnHungerChanged      += UpdateHungerBar;
        SurvivalStats.OnThirstChanged      += UpdateThirstBar;
        SurvivalStats.OnStaminaChanged     += UpdateStaminaBar;
        SurvivalStats.OnPlayerDied         += ShowDeathScreen;
        SurvivalStats.OnTemperatureChanged += UpdateTemperatureUI;

        InventorySystem.OnInventoryChanged       += RefreshHotbar;
        InventorySystem.OnHotbarSelectionChanged += UpdateHotbarSelection;
        InventorySystem.OnItemAdded              += OnItemPickedUp;

        PlayerController.OnInteractableNear += ShowInteractPrompt;
        PlayerController.OnInteractableLeft += HideInteractPrompt;

        GameManager.OnGameStateChanged += OnGameStateChanged;
        GameManager.OnDayChanged       += UpdateDayText;
    }

    private void OnDisable()
    {
        SurvivalStats.OnHealthChanged      -= UpdateHealthBar;
        SurvivalStats.OnHungerChanged      -= UpdateHungerBar;
        SurvivalStats.OnThirstChanged      -= UpdateThirstBar;
        SurvivalStats.OnStaminaChanged     -= UpdateStaminaBar;
        SurvivalStats.OnPlayerDied         -= ShowDeathScreen;
        SurvivalStats.OnTemperatureChanged -= UpdateTemperatureUI;

        InventorySystem.OnInventoryChanged       -= RefreshHotbar;
        InventorySystem.OnHotbarSelectionChanged -= UpdateHotbarSelection;
        InventorySystem.OnItemAdded              -= OnItemPickedUp;

        PlayerController.OnInteractableNear -= ShowInteractPrompt;
        PlayerController.OnInteractableLeft -= HideInteractPrompt;

        GameManager.OnGameStateChanged -= OnGameStateChanged;
        GameManager.OnDayChanged       -= UpdateDayText;
    }

    private void Update()
    {
        if (timeText != null && GameManager.Instance != null)
            timeText.text = GameManager.Instance.GetFormattedTime();

        // 핫바 숫자키 선택
        for (int i = 0; i < 8; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                InventorySystem.Instance?.SelectHotbarSlot(i);

        // 맵 단축키
        if (Input.GetKeyDown(KeyCode.M)) ToggleMap();
    }

    // ─── HUD 업데이트 ────────────────────────────────────────

    private void UpdateHealthBar(float current, float max)
    {
        if (healthSlider == null) return;
        healthSlider.value = current / max;

        float ratio = current / max;
        Color col = ratio > 0.6f ? healthColorHigh
                  : ratio > 0.3f ? healthColorMedium
                                 : healthColorLow;
        healthSlider.fillRect.GetComponent<Image>().color = col;
    }

    private void UpdateHungerBar(float current, float max)
    {
        if (hungerSlider != null) hungerSlider.value = current / max;
    }

    private void UpdateThirstBar(float current, float max)
    {
        if (thirstSlider != null) thirstSlider.value = current / max;
    }

    private void UpdateStaminaBar(float current, float max)
    {
        if (staminaSlider != null) staminaSlider.value = current / max;
    }

    private void UpdateTemperatureUI(float current, float normal)
    {
        // TODO: 체온 아이콘 색상 변경
    }

    private void UpdateDayText(int day)
    {
        if (dayText != null) dayText.text = $"Day {day}";
    }

    // ─── 패널 토글 ───────────────────────────────────────────

    public void ToggleInventory()
    {
        IsInventoryOpen = !IsInventoryOpen;
        inventoryPanel?.SetActive(IsInventoryOpen);
        SetCursorState(IsAnyPanelOpen);
    }

    public void ToggleCrafting()
    {
        IsCraftingOpen = !IsCraftingOpen;
        craftingPanel?.SetActive(IsCraftingOpen);
        SetCursorState(IsAnyPanelOpen);
    }

    public void ToggleMap()
    {
        IsMapOpen = !IsMapOpen;
        mapPanel?.SetActive(IsMapOpen);
        SetCursorState(IsAnyPanelOpen);
    }

    public void ShowPauseMenu()  => pausePanel?.SetActive(true);
    public void HidePauseMenu()  => pausePanel?.SetActive(false);
    private void ShowDeathScreen() => deathPanel?.SetActive(true);

    // ─── 핫바 ────────────────────────────────────────────────

    private void RefreshHotbar()
    {
        var inv = InventorySystem.Instance;
        if (inv == null || hotbarSlots == null) return;

        var hotbar = inv.GetHotbar();
        for (int i = 0; i < hotbarSlots.Length && i < hotbar.Length; i++)
            hotbarSlots[i].UpdateSlot(hotbar[i]);
    }

    private void UpdateHotbarSelection(int index)
    {
        if (selectedSlotHighlight == null || hotbarSlots == null) return;
        if (index >= 0 && index < hotbarSlots.Length)
            selectedSlotHighlight.transform.position = hotbarSlots[index].transform.position;
    }

    // ─── 상호작용 프롬프트 ───────────────────────────────────

    private void ShowInteractPrompt(IInteractable interactable)
    {
        if (interactPromptObj == null) return;
        interactPromptObj.SetActive(true);
        if (interactPromptText != null)
            interactPromptText.text = $"[F] {interactable.InteractPrompt}";
    }

    private void HideInteractPrompt()
    {
        interactPromptObj?.SetActive(false);
    }

    // ─── 알림 ────────────────────────────────────────────────

    public void ShowNotification(string message)
    {
        if (notificationText == null) return;
        if (_notifCoroutine != null) StopCoroutine(_notifCoroutine);
        _notifCoroutine = StartCoroutine(ShowNotifCoroutine(message));
    }

    private System.Collections.IEnumerator ShowNotifCoroutine(string message)
    {
        notificationText.text = message;
        notificationText.gameObject.SetActive(true);
        yield return new WaitForSeconds(notificationDuration);
        notificationText.gameObject.SetActive(false);
    }

    private void OnItemPickedUp(ItemData item, int amount)
    {
        ShowNotification($"{item.itemNameKR} x{amount} 획득");
    }

    // ─── 유틸 ────────────────────────────────────────────────

    private void SetCursorState(bool visible)
    {
        Cursor.visible   = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }

    private void OnGameStateChanged(GameState state)
    {
        if (state == GameState.Paused) ShowPauseMenu();
        else HidePauseMenu();
    }
}

/// <summary>핫바 슬롯 UI 컴포넌트</summary>
public class HotbarSlotUI : MonoBehaviour
{
    [SerializeField] private Image            iconImage;
    [SerializeField] private TextMeshProUGUI  countText;

    public void UpdateSlot(ItemSlot slot)
    {
        if (slot == null || slot.IsEmpty)
        {
            iconImage.enabled   = false;
            countText.enabled   = false;
            return;
        }

        iconImage.enabled   = true;
        iconImage.sprite    = slot.Item.icon;
        countText.enabled   = slot.Item.canStack && slot.Amount > 1;
        countText.text      = slot.Amount.ToString();
    }
}
