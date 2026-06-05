using System;
using System.Collections;
using UnityEngine;

/// <summary>원거리 전투 — 3D Ground Raycast 조준, 3D 발사체</summary>
public class RangedCombatSystem : MonoBehaviour
{
    public static event Action<int, int> OnAmmoChanged;
    public static event Action OnFired, OnReloading, OnReloaded, OnDryFire;

    [SerializeField] private GameObject bulletPrefab, arrowPrefab;
    [SerializeField] private Transform  muzzlePoint;
    [SerializeField] private GameObject aimReticle;
    [SerializeField] private LayerMask  groundLayer;
    [SerializeField] private Camera     mainCamera;

    private WeaponStats _stats;
    private int _currentAmmo;
    private bool _isReloading, _isAiming;
    private float _fireTimer;
    private ItemData _weapon;
    private AmmoType _ammoType;
    private SurvivalStats _survivalStats;
    private Vector3 _aimWorldPos;

    private void Awake() { _survivalStats = GetComponent<SurvivalStats>(); mainCamera ??= Camera.main; }

    private void Update()
    {
        if (_weapon == null || !_weapon.IsWeapon) return;
        if (GameManager.Instance?.CurrentState != GameState.Playing) return;
        if (PlayerController.IsPointerOverUI()) return;
        UpdateAim3D();
        if (_fireTimer > 0) _fireTimer -= Time.deltaTime;
        if (Input.GetMouseButton(0))     TryFire();
        if (Input.GetMouseButtonDown(1)) _isAiming = !_isAiming;
        if (Input.GetKeyDown(KeyCode.R)) StartReload();
    }

    private void UpdateAim3D()
    {
        // 3D Ground Raycast로 마우스 위치 결정
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayer))
        {
            _aimWorldPos = hit.point;
            if (aimReticle) { aimReticle.SetActive(_isAiming); aimReticle.transform.position = hit.point + Vector3.up * 0.02f; }
        }
        if (!_isAiming && aimReticle) aimReticle.SetActive(false);
    }

    public void TryFire()
    {
        if (_isReloading || _fireTimer > 0 || _stats == null) return;
        if (_currentAmmo <= 0) { OnDryFire?.Invoke(); StartReload(); return; }
        _currentAmmo--;
        _fireTimer = 1f / _stats.fireRate;
        OnAmmoChanged?.Invoke(_currentAmmo, _stats.magazineSize);
        OnFired?.Invoke();

        // 3D 발사 방향 — 마우스 위치 기준
        Vector3 muzzlePos = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 1.2f;
        Vector3 fireDir   = _aimWorldPos - muzzlePos; fireDir.y = 0;
        if (fireDir.sqrMagnitude < 0.01f) fireDir = transform.forward;
        fireDir = fireDir.normalized;

        if (_stats.pelletCount > 1) for (int i = 0; i < _stats.pelletCount; i++) SpawnProjectile(AddSpread(fireDir, _stats.spread * 2f), muzzlePos);
        else SpawnProjectile(AddSpread(fireDir, _stats.spread), muzzlePos);

        _survivalStats?.ConsumeStamina(2f);
        WeaponDurabilitySystem.Instance?.DecreaseDurability(_weapon, 0.5f);
    }

    private void SpawnProjectile(Vector3 dir, Vector3 spawnPos)
    {
        var prefab = _ammoType == AmmoType.Arrow ? arrowPrefab : bulletPrefab;
        if (prefab == null) return;
        var go = Instantiate(prefab, spawnPos, Quaternion.LookRotation(dir));
        go.GetComponent<Projectile>()?.Init(dir, _stats.damage, _stats.projectileSpeed, _weapon.attackRange, gameObject);
    }

    public void StartReload()
    {
        if (_isReloading || _stats == null || _currentAmmo >= _stats.magazineSize) return;
        var ammo = GetAmmoItem();
        if (ammo == null || !InventorySystem.Instance.HasItem(ammo, 1)) { UIManager.Instance?.ShowNotification("탄약이 없습니다"); return; }
        StartCoroutine(ReloadRoutine(ammo));
    }

    private IEnumerator ReloadRoutine(ItemData ammo)
    {
        _isReloading = true; OnReloading?.Invoke();
        UIManager.Instance?.ShowNotification($"장전 중... ({_stats.reloadTime:F1}초)");
        yield return new WaitForSeconds(_stats.reloadTime);
        int toLoad = Mathf.Min(_stats.magazineSize - _currentAmmo, InventorySystem.Instance.GetItemCount(ammo));
        InventorySystem.Instance.RemoveItem(ammo, toLoad);
        _currentAmmo += toLoad;
        OnAmmoChanged?.Invoke(_currentAmmo, _stats.magazineSize); OnReloaded?.Invoke(); _isReloading = false;
    }

    public void SetWeapon(ItemData weapon)
    {
        _weapon = weapon;
        _stats  = weapon == null ? null : WeaponStatsDatabase.Get(weapon.name);
        if (_stats == null) return;
        _ammoType    = _stats.ammoType;
        _currentAmmo = _stats.magazineSize;
        OnAmmoChanged?.Invoke(_currentAmmo, _stats.magazineSize);
    }

    private Vector3 AddSpread(Vector3 dir, float spread)
        => Quaternion.AngleAxis(UnityEngine.Random.Range(-spread, spread), Vector3.up) * dir;

    private ItemData GetAmmoItem()
    {
        string n = _ammoType switch { AmmoType.Pistol=>"PistolAmmo", AmmoType.Shotgun=>"ShotgunShell", AmmoType.Rifle=>"RifleAmmo", AmmoType.Arrow=>"Arrow", _=>"" };
        return string.IsNullOrEmpty(n) ? null : ItemDatabase.Instance?.Get(n);
    }
}
