using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// ─── WorldItem (3D 부유/회전) ─────────────────────────────
public class WorldItem : MonoBehaviour, IInteractable
{
    [SerializeField] private TextMeshPro amountLabel;
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobFrequency = 2f;
    [SerializeField] private float rotateSpeed  = 60f;

    private ItemData _item;
    private int      _amount;
    private float    _startY;
    public string InteractPrompt => $"{_item?.itemNameKR} 줍기";

    private void Start() => _startY = transform.position.y;

    private void Update()
    {
        // 3D Y축 부유
        Vector3 p = transform.position;
        p.y = _startY + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        transform.position = p;
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
    }

    public void Initialize(ItemData item, int amount)
    {
        _item = item; _amount = amount;
        if (amountLabel) amountLabel.text = amount > 1 ? $"x{amount}" : "";
    }

    public void Interact(PlayerController player)
    {
        if (InventorySystem.Instance?.AddItem(_item, _amount) == true) Destroy(gameObject);
        else UIManager.Instance?.ShowNotification("인벤토리가 꽉 찼습니다");
    }
}

// ─── LootTable ───────────────────────────────────────────
[CreateAssetMenu(fileName = "NewLootTable", menuName = "LastShore/LootTable")]
public class LootTable : UnityEngine.ScriptableObject
{
    [System.Serializable]
    public struct LootEntry
    {
        public ItemData item;
        [Range(0f,1f)] public float dropChance;
        public int minAmount, maxAmount;
    }
    public List<LootEntry> entries = new();

    public void DropLoot(Vector3 position)
    {
        foreach (var e in entries)
        {
            if (UnityEngine.Random.value > e.dropChance) continue;
            int amount = UnityEngine.Random.Range(e.minAmount, e.maxAmount + 1);
            if (e.item?.prefab == null) continue;
            // 3D XZ 퍼뜨리기
            Vector2 off = UnityEngine.Random.insideUnitCircle;
            var go = UnityEngine.Object.Instantiate(e.item.prefab,
                position + new Vector3(off.x, 0.5f, off.y), Quaternion.identity);
            go.GetComponent<WorldItem>()?.Initialize(e.item, amount);
        }
    }
}

// ─── CameraController (3D XZ 추적, Y/회전 고정) ──────────
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [SerializeField] private Transform target;
    [SerializeField] private float     smoothSpeed = 5f;

    private float _fixedY;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        _fixedY  = transform.position.y;
    }

    private void LateUpdate()
    {
        if (target == null) return;
        // XZ만 추적, Y 완전 고정
        Vector3 desired = new Vector3(target.position.x, _fixedY, target.position.z);
        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }

    public void SetTarget(Transform t) => target = t;

    public void Shake(float duration, float magnitude) => StartCoroutine(ShakeCoroutine(duration, magnitude));

    private IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        Vector3 orig = transform.localPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // XZ만 흔들기, Y 고정
            transform.localPosition = new Vector3(
                orig.x + UnityEngine.Random.Range(-1f,1f) * magnitude,
                orig.y,
                orig.z + UnityEngine.Random.Range(-1f,1f) * magnitude);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = orig;
    }
}

// ─── SkillSystem ─────────────────────────────────────────
public class SkillSystem : MonoBehaviour
{
    public static SkillSystem Instance { get; private set; }

    [System.Serializable]
    public class SkillData { public SkillType type; public int level = 1; public float currentExp; public float ExpToNext => 100f * Mathf.Pow(1.2f, level); }

    private Dictionary<SkillType, SkillData> _skills = new();
    public static event Action<SkillType, int> OnSkillLevelUp;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        foreach (SkillType t in Enum.GetValues(typeof(SkillType)))
            if (t != SkillType.None) _skills[t] = new SkillData { type = t };
    }

    public void AddExp(SkillType skill, float amount)
    {
        if (skill == SkillType.None || !_skills.TryGetValue(skill, out var d)) return;
        d.currentExp += amount;
        while (d.currentExp >= d.ExpToNext)
        { d.currentExp -= d.ExpToNext; d.level++; OnSkillLevelUp?.Invoke(skill, d.level); UIManager.Instance?.ShowNotification($"{skill} Lv.{d.level}!"); }
    }

    public int   GetLevel(SkillType s) => _skills.TryGetValue(s, out var d) ? d.level : 1;
    public float GetExp(SkillType s)   => _skills.TryGetValue(s, out var d) ? d.currentExp : 0;
}

// ─── Projectile (3D OnTriggerEnter) ─────────────────────
public class Projectile : MonoBehaviour
{
    private float _damage, _speed, _maxRange;
    private Vector3 _direction;
    private GameObject _owner;
    private float _traveled;

    [SerializeField] private GameObject hitEffect;
    [SerializeField] private LayerMask  hitLayer;

    public void Init(Vector3 direction, float damage, float speed, float range, GameObject owner)
    { _direction = direction.normalized; _damage = damage; _speed = speed; _maxRange = range; _owner = owner; }

    private void Update()
    {
        float dist = _speed * Time.deltaTime;
        _traveled += dist;
        transform.Translate(Vector3.forward * dist, Space.Self);
        if (_traveled >= _maxRange) Destroy(gameObject);
    }

    // 3D OnTriggerEnter
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == _owner) return;
        if (other.TryGetComponent(out IDamageable t)) { t.TakeDamage(_damage, transform.position); Spawn(); Destroy(gameObject); }
        else if (((1 << other.gameObject.layer) & hitLayer) != 0) { Spawn(); Destroy(gameObject); }
    }

    private void Spawn() { if (hitEffect) Instantiate(hitEffect, transform.position, Quaternion.identity); }
}

// ─── Billboard (World Space Canvas가 카메라를 향함) ──────
public class Billboard : MonoBehaviour
{
    private Camera _cam;
    private void Start() => _cam = Camera.main;
    private void LateUpdate()
    {
        if (_cam == null) return;
        transform.LookAt(transform.position + _cam.transform.rotation * Vector3.forward,
                         _cam.transform.rotation * Vector3.up);
    }
}
