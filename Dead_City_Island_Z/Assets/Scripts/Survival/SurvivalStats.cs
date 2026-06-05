using System;
using UnityEngine;

/// <summary>
/// 플레이어 생존 스탯 — HP, 배고픔, 갈증, 스태미나, 체온, 출혈
/// 듀랑고 / Project Zomboid 스타일
/// </summary>
public class SurvivalStats : MonoBehaviour
{
    // ─── 이벤트 ────────────────────────────────────────────
    public static event Action<float, float> OnHealthChanged;      // (current, max)
    public static event Action<float, float> OnHungerChanged;
    public static event Action<float, float> OnThirstChanged;
    public static event Action<float, float> OnStaminaChanged;
    public static event Action<float, float> OnTemperatureChanged;
    public static event Action              OnPlayerDied;

    // ─── 체력 ───────────────────────────────────────────────
    [Header("체력")]
    [SerializeField] private float maxHealth    = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private float healthRegenRate = 0.5f;        // 초당 회복량 (조건 충족 시)

    // ─── 배고픔 ─────────────────────────────────────────────
    [Header("배고픔 / 갈증")]
    [SerializeField] private float maxHunger   = 100f;
    [SerializeField] private float currentHunger;
    [SerializeField] private float hungerDecayRate = 0.5f;        // 초당 감소량

    [SerializeField] private float maxThirst   = 100f;
    [SerializeField] private float currentThirst;
    [SerializeField] private float thirstDecayRate = 0.8f;        // 갈증이 더 빠름

    // ─── 스태미나 ───────────────────────────────────────────
    [Header("스태미나")]
    [SerializeField] private float maxStamina  = 100f;
    [SerializeField] private float currentStamina;
    [SerializeField] private float staminaRegenRate  = 10f;       // 초당 회복
    [SerializeField] private float staminaRegenDelay = 1.5f;      // 마지막 소비 후 회복 대기
    private float _staminaRegenTimer;

    // ─── 체온 ───────────────────────────────────────────────
    [Header("체온")]
    [SerializeField] private float normalTemperature  = 36.5f;
    [SerializeField] private float currentTemperature;
    [SerializeField] private float environmentTemp    = 20f;      // 외부 온도 (날씨/시간에 따라 변동)
    [SerializeField] private float tempAdjustRate     = 0.1f;     // 온도 조절 속도

    // ─── 출혈 ───────────────────────────────────────────────
    [Header("출혈")]
    [SerializeField] private bool  isBleeding;
    [SerializeField] private float bleedDamageRate = 0.3f;        // 초당 체력 감소

    // ─── 상태 임계값 ────────────────────────────────────────
    private const float STARVING_THRESHOLD    = 20f;
    private const float DEHYDRATED_THRESHOLD  = 20f;
    private const float HYPOTHERMIA_THRESHOLD = 33f;
    private const float HYPERTHERMIA_THRESHOLD= 40f;

    public bool IsAlive    => currentHealth > 0f;
    public bool IsStarving => currentHunger < STARVING_THRESHOLD;
    public bool IsThirsty  => currentThirst < DEHYDRATED_THRESHOLD;

    // ─── 프로퍼티 (읽기 전용) ───────────────────────────────
    public float Health      => currentHealth;
    public float MaxHealth   => maxHealth;
    public float Hunger      => currentHunger;
    public float Thirst      => currentThirst;
    public float Stamina     => currentStamina;
    public float MaxStamina  => maxStamina;
    public float Temperature => currentTemperature;
    public bool  IsBleeding  => isBleeding;

    // ───────────────────────────────────────────────────────

    private void Awake()
    {
        currentHealth      = maxHealth;
        currentHunger      = maxHunger;
        currentThirst      = maxThirst;
        currentStamina     = maxStamina;
        currentTemperature = normalTemperature;
    }

    private void Update()
    {
        if (!IsAlive) return;

        float dt = Time.deltaTime;

        UpdateHungerThirst(dt);
        UpdateTemperature(dt);
        UpdateStamina(dt);
        UpdateBleeding(dt);
        UpdatePassiveHealing(dt);
        CheckDeathConditions();
    }

    // ─── 업데이트 메서드 ─────────────────────────────────────

    private void UpdateHungerThirst(float dt)
    {
        // 스태미나 소모 중이면 배고픔/갈증 더 빠르게 감소
        float activityMult = (currentStamina < maxStamina * 0.5f) ? 1.5f : 1f;

        ChangeHunger(-hungerDecayRate * activityMult * dt);
        ChangeThirst(-thirstDecayRate * activityMult * dt);

        // 굶주림/탈수 시 체력 감소
        if (IsStarving)
            TakeDamage(0.1f * dt, DamageType.Starvation);
        if (IsThirsty)
            TakeDamage(0.15f * dt, DamageType.Dehydration);
    }

    private void UpdateTemperature(float dt)
    {
        float targetTemp = normalTemperature + (environmentTemp - 20f) * 0.3f;
        currentTemperature = Mathf.MoveTowards(currentTemperature, targetTemp, tempAdjustRate * dt);

        if (currentTemperature < HYPOTHERMIA_THRESHOLD)
            TakeDamage(0.2f * dt, DamageType.Hypothermia);
        else if (currentTemperature > HYPERTHERMIA_THRESHOLD)
            TakeDamage(0.2f * dt, DamageType.Hyperthermia);

        OnTemperatureChanged?.Invoke(currentTemperature, normalTemperature);
    }

    private void UpdateStamina(float dt)
    {
        if (_staminaRegenTimer > 0)
        {
            _staminaRegenTimer -= dt;
            return;
        }

        // 배고프거나 목마르면 스태미나 회복 감소
        float regenMult = (IsStarving || IsThirsty) ? 0.3f : 1f;
        ChangeStamina(staminaRegenRate * regenMult * dt);
    }

    private void UpdateBleeding(float dt)
    {
        if (!isBleeding) return;
        TakeDamage(bleedDamageRate * dt, DamageType.Bleeding);
    }

    private void UpdatePassiveHealing(float dt)
    {
        // 배부르고, 목 안 마르고, 체온 정상일 때만 자연 회복
        bool canRegen = !IsStarving && !IsThirsty
                        && currentTemperature >= HYPOTHERMIA_THRESHOLD
                        && currentTemperature <= HYPERTHERMIA_THRESHOLD
                        && !isBleeding;

        if (canRegen)
            Heal(healthRegenRate * dt);
    }

    private void CheckDeathConditions()
    {
        if (currentHealth <= 0)
            Die();
    }

    // ─── 공개 메서드 ─────────────────────────────────────────

    public void TakeDamage(float amount, DamageType type = DamageType.Physical)
    {
        if (!IsAlive) return;
        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void Heal(float amount)
    {
        if (!IsAlive) return;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void Eat(float nutritionAmount)
    {
        ChangeHunger(nutritionAmount);
    }

    public void Drink(float hydrationAmount)
    {
        ChangeThirst(hydrationAmount);
    }

    /// <summary>스태미나 소비 (이동, 공격 등)</summary>
    public bool ConsumeStamina(float amount)
    {
        if (currentStamina < amount) return false;
        currentStamina = Mathf.Clamp(currentStamina - amount, 0f, maxStamina);
        _staminaRegenTimer = staminaRegenDelay;
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        return true;
    }

    public void SetBleeding(bool state) => isBleeding = state;

    public void SetEnvironmentTemperature(float temp) => environmentTemp = temp;

    // ─── 내부 변경 ───────────────────────────────────────────

    private void ChangeHunger(float delta)
    {
        currentHunger = Mathf.Clamp(currentHunger + delta, 0f, maxHunger);
        OnHungerChanged?.Invoke(currentHunger, maxHunger);
    }

    private void ChangeThirst(float delta)
    {
        currentThirst = Mathf.Clamp(currentThirst + delta, 0f, maxThirst);
        OnThirstChanged?.Invoke(currentThirst, maxThirst);
    }

    private void ChangeStamina(float delta)
    {
        currentStamina = Mathf.Clamp(currentStamina + delta, 0f, maxStamina);
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    private void Die()
    {
        Debug.Log("[SurvivalStats] 플레이어 사망");
        OnPlayerDied?.Invoke();
        enabled = false;
    }
}

public enum DamageType
{
    Physical,
    Starvation,
    Dehydration,
    Bleeding,
    Hypothermia,
    Hyperthermia,
    Poison,
    Fall
}
