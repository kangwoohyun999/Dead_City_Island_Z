using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 저장/불러오기 시스템 — Unity JsonUtility + PlayerPrefs
/// SaveLoadUI가 요구하는 모든 인터페이스 포함
/// </summary>
public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    private const int    SAVE_SLOTS  = 3;
    private const string KEY_PREFIX  = "DeadCityIZ_Slot_";

    // SaveLoadUI가 구독하는 이벤트
    public static event Action<int> OnGameSaved;
    public static event Action<int> OnGameLoaded;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    // ─── 저장 ────────────────────────────────────────────────

    public void Save(int slot)
    {
        if (slot < 0 || slot >= SAVE_SLOTS) return;

        var data = new SaveData();
        CollectData(data);

        PlayerPrefs.SetString(KEY_PREFIX + slot,          JsonUtility.ToJson(data, true));
        PlayerPrefs.SetString(KEY_PREFIX + slot + "_at",  DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        PlayerPrefs.SetInt   (KEY_PREFIX + slot + "_day", data.currentDay);
        PlayerPrefs.SetFloat (KEY_PREFIX + slot + "_pt",  data.totalPlaytime);
        PlayerPrefs.Save();

        OnGameSaved?.Invoke(slot);
        UIManager.Instance?.ShowNotification($"슬롯 {slot + 1} 저장 완료");
    }

    // ─── 불러오기 ─────────────────────────────────────────────

    public bool Load(int slot)
    {
        if (slot < 0 || slot >= SAVE_SLOTS) return false;
        if (!HasSlot(slot)) { UIManager.Instance?.ShowNotification("저장 데이터 없음"); return false; }

        string json = PlayerPrefs.GetString(KEY_PREFIX + slot, "");
        var data = JsonUtility.FromJson<SaveData>(json);
        if (data == null) return false;

        ApplyData(data);
        OnGameLoaded?.Invoke(slot);
        UIManager.Instance?.ShowNotification($"슬롯 {slot + 1} 불러오기 완료");
        return true;
    }

    // ─── 슬롯 정보 ────────────────────────────────────────────

    public SaveSlotInfo GetSlotInfo(int slot)
    {
        if (!HasSlot(slot))
            return new SaveSlotInfo { slot = slot, isEmpty = true };

        return new SaveSlotInfo
        {
            slot      = slot,
            isEmpty   = false,
            dayCount  = PlayerPrefs.GetInt   (KEY_PREFIX + slot + "_day", 1),
            playtime  = PlayerPrefs.GetFloat  (KEY_PREFIX + slot + "_pt",  0f),
            savedAt   = PlayerPrefs.GetString (KEY_PREFIX + slot + "_at",  ""),
            worldType = "Island"
        };
    }

    public void DeleteSlot(int slot)
    {
        PlayerPrefs.DeleteKey(KEY_PREFIX + slot);
        PlayerPrefs.DeleteKey(KEY_PREFIX + slot + "_at");
        PlayerPrefs.DeleteKey(KEY_PREFIX + slot + "_day");
        PlayerPrefs.DeleteKey(KEY_PREFIX + slot + "_pt");
        PlayerPrefs.Save();
    }

    public bool HasSlot(int slot) => PlayerPrefs.HasKey(KEY_PREFIX + slot);

    // ─── 데이터 수집 ──────────────────────────────────────────

    private void CollectData(SaveData data)
    {
        var stats  = FindFirstObjectByType<SurvivalStats>();
        var player = FindFirstObjectByType<PlayerController>();

        if (stats != null)
        {
            data.playerHP      = stats.Health;
            data.playerHunger  = stats.Hunger;
            data.playerThirst  = stats.Thirst;
            data.playerStamina = stats.Stamina;
        }
        if (player != null)
            data.playerPosition = player.transform.position;

        data.currentDay    = GameManager.Instance?.DayCount ?? 1;
        data.totalPlaytime = Time.realtimeSinceStartup;
        data.currentScene  = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    }

    private void ApplyData(SaveData data)
    {
        var stats  = FindFirstObjectByType<SurvivalStats>();
        var player = FindFirstObjectByType<PlayerController>();

        if (stats != null)
        {
            // SurvivalStats.Heal로 체력 복원 (SetHealth 없음)
            stats.Heal(data.playerHP - stats.Health);
            stats.Eat(data.playerHunger - stats.Hunger);
            stats.Drink(data.playerThirst - stats.Thirst);
        }
        if (player != null && data.playerPosition != Vector3.zero)
            player.transform.position = data.playerPosition;
    }
}

// ─── 저장 데이터 구조 ─────────────────────────────────────────

[Serializable]
public class SaveData
{
    public int     saveVersion   = 1;
    public string  currentScene  = "Island";
    public int     currentDay    = 1;
    public float   totalPlaytime = 0f;

    public float   playerHP      = 100f;
    public float   playerHunger  = 100f;
    public float   playerThirst  = 100f;
    public float   playerStamina = 100f;
    public Vector3 playerPosition;
}

// ─── 슬롯 정보 (SaveLoadUI가 사용) ──────────────────────────

[Serializable]
public class SaveSlotInfo
{
    public int    slot;
    public bool   isEmpty;
    public int    dayCount;
    public float  playtime;
    public string savedAt;
    public string worldType;
}
