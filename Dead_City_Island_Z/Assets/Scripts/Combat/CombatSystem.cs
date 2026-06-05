using System;
using System.Collections;
using UnityEngine;

/// <summary>근접 전투 시스템 (3D Physics.OverlapSphere)</summary>
public class CombatSystem : MonoBehaviour
{
    [Header("공격")]
    [SerializeField] private float baseAttackDamage = 10f;
    [SerializeField] private float baseAttackSpeed  = 1f;
    [SerializeField] private float baseAttackRange  = 1.2f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private Transform attackPoint;

    private float _weaponDamage, _weaponSpeed, _weaponRange;
    private float _attackCooldown;
    private bool  _isAttacking;
    private Animator _animator;

    public static event Action OnAttack;
    public static event Action<float> OnDamageDealt;

    private static readonly int AnimAttack = Animator.StringToHash("Attack");

    private float TotalDamage   => baseAttackDamage + _weaponDamage;
    private float TotalSpeed    => baseAttackSpeed   + _weaponSpeed;
    private float TotalRange    => baseAttackRange   + _weaponRange;
    private float AttackInterval=> 1f / Mathf.Max(0.1f, TotalSpeed);

    private void Awake() => _animator = GetComponent<Animator>();

    private void Update() { if (_attackCooldown > 0) _attackCooldown -= Time.deltaTime; }

    public void TryAttack()
    {
        if (_attackCooldown > 0 || _isAttacking) return;
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        _isAttacking    = true;
        _attackCooldown = AttackInterval;
        _animator?.SetTrigger(AnimAttack);
        OnAttack?.Invoke();
        yield return new WaitForSeconds(0.15f);

        // 3D OverlapSphere
        Vector3 pos = attackPoint != null
            ? attackPoint.position
            : transform.position + transform.forward * TotalRange * 0.5f;

        Collider[] hits = Physics.OverlapSphere(pos, TotalRange, enemyLayer);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out IDamageable target))
            {
                float dmg = TotalDamage * (UnityEngine.Random.value < 0.2f ? 1.5f : 1f);
                target.TakeDamage(dmg, transform.position);
                OnDamageDealt?.Invoke(dmg);
                CameraController.Instance?.Shake(0.1f, 0.05f);
            }
        }
        yield return new WaitForSeconds(0.1f);
        _isAttacking = false;
    }

    public void UpdateWeaponStats(ItemData weapon)
    {
        _weaponDamage = weapon == null ? 0 : weapon.attackDamage;
        _weaponSpeed  = weapon == null ? 0 : weapon.attackSpeed - 1f;
        _weaponRange  = weapon == null ? 0 : weapon.attackRange - 1.2f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, baseAttackRange);
    }
#endif
}
