using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 저장/불러오기 슬롯 UI 패널 (3슬롯)
/// 일시정지 메뉴에서 호출
/// </summary>
public class SaveLoadUI : MonoBehaviour
{
    public enum Mode { Save, Load }

    [Header("패널")]
    [SerializeField] private GameObject   panel;
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("슬롯 UI (3개)")]
    [SerializeField] private SaveSlotEntryUI[] slotEntries;

    [Header("확인 다이얼로그")]
    [SerializeField] private GameObject       confirmDialog;
    [SerializeField] private TextMeshProUGUI  confirmMessage;
    [SerializeField] private Button           confirmYes;
    [SerializeField] private Button           confirmNo;

    private Mode _mode;
    private int  _pendingSlot = -1;

    // ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        SaveSystem.OnGameSaved  += OnSaved;
        SaveSystem.OnGameLoaded += OnLoaded;
    }

    private void OnDisable()
    {
        SaveSystem.OnGameSaved  -= OnSaved;
        SaveSystem.OnGameLoaded -= OnLoaded;
    }

    public void Open(Mode mode)
    {
        _mode = mode;
        panel?.SetActive(true);
        if (confirmDialog) confirmDialog.SetActive(false);

        titleText.text = mode == Mode.Save ? "💾 게임 저장" : "📂 게임 불러오기";
        RefreshSlots();
    }

    public void Close()
    {
        panel?.SetActive(false);
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < slotEntries.Length && i < 3; i++)
        {
            var info = SaveSystem.Instance?.GetSlotInfo(i) ?? new SaveSlotInfo { slot = i, isEmpty = true };
            int slotIdx = i;
            slotEntries[i].Refresh(info,
                onClick: () => OnSlotClicked(slotIdx),
                onDelete: () => OnSlotDelete(slotIdx));
        }
    }

    private void OnSlotClicked(int slot)
    {
        _pendingSlot = slot;

        if (_mode == Mode.Save)
        {
            var info = SaveSystem.Instance?.GetSlotInfo(slot);
            if (info != null && !info.isEmpty)
            {
                ShowConfirm($"슬롯 {slot + 1}에 덮어쓸까요?", () =>
                {
                    SaveSystem.Instance?.Save(slot);
                    RefreshSlots();
                });
            }
            else
            {
                SaveSystem.Instance?.Save(slot);
                RefreshSlots();
            }
        }
        else // Load
        {
            var info = SaveSystem.Instance?.GetSlotInfo(slot);
            if (info == null || info.isEmpty)
            {
                UIManager.Instance?.ShowNotification("저장 데이터가 없습니다");
                return;
            }

            ShowConfirm($"슬롯 {slot + 1}을 불러올까요?\n현재 진행이 사라집니다.", () =>
            {
                SaveSystem.Instance?.Load(slot);
                Close();
            });
        }
    }

    private void OnSlotDelete(int slot)
    {
        ShowConfirm($"슬롯 {slot + 1} 데이터를 삭제할까요?", () =>
        {
            SaveSystem.Instance?.DeleteSlot(slot);
            RefreshSlots();
        });
    }

    private void ShowConfirm(string message, Action onConfirm)
    {
        if (confirmDialog == null) { onConfirm?.Invoke(); return; }

        confirmDialog.SetActive(true);
        if (confirmMessage) confirmMessage.text = message;

        confirmYes.onClick.RemoveAllListeners();
        confirmNo .onClick.RemoveAllListeners();
        confirmYes.onClick.AddListener(() => { onConfirm?.Invoke(); confirmDialog.SetActive(false); });
        confirmNo .onClick.AddListener(() => confirmDialog.SetActive(false));
    }

    private void OnSaved(int slot)  => RefreshSlots();
    private void OnLoaded(int slot) => RefreshSlots();
}

// ─── 슬롯 엔트리 UI ──────────────────────────────────────────

public class SaveSlotEntryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI slotLabel;
    [SerializeField] private TextMeshProUGUI infoText;
    [SerializeField] private TextMeshProUGUI dateText;
    [SerializeField] private Button          mainButton;
    [SerializeField] private Button          deleteButton;
    [SerializeField] private GameObject      emptyOverlay;

    public void Refresh(SaveSlotInfo info, Action onClick, Action onDelete)
    {
        if (slotLabel) slotLabel.text = $"슬롯 {info.slot + 1}";

        mainButton?.onClick.RemoveAllListeners();
        deleteButton?.onClick.RemoveAllListeners();
        mainButton?.onClick.AddListener(() => onClick?.Invoke());
        deleteButton?.onClick.AddListener(() => onDelete?.Invoke());

        if (info.isEmpty)
        {
            if (emptyOverlay) emptyOverlay.SetActive(true);
            if (infoText)  infoText.text  = "— 빈 슬롯 —";
            if (dateText)  dateText.text  = "";
            if (deleteButton) deleteButton.interactable = false;
            return;
        }

        if (emptyOverlay) emptyOverlay.SetActive(false);
        if (deleteButton) deleteButton.interactable = true;

        TimeSpan ts = TimeSpan.FromSeconds(info.playtime);
        string playtimeStr = $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";

        if (infoText) infoText.text =
            $"Day {info.dayCount}  |  {info.worldType}  |  플레이 {playtimeStr}";
        if (dateText) dateText.text = info.savedAt;
    }
}
