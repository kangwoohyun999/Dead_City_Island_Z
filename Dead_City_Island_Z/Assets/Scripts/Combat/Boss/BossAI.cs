using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>보스 기본 클래스 (3D NavMeshAgent)</summary>
public abstract class BossAI : MonoBehaviour, IDamageable
{
    [Header("스탯")]
    [SerializeField] protected float  maxHealth      = 1000f;
    [SerializeField] protected float  moveSpeed      = 2.5f;
    [SerializeField] protected string bossNameKR     = "보스";
    [SerializeField] protected float  phase2Threshold= 0.65f;
    [SerializeField] protected float  phase3Threshold= 0.30f;
    [SerializeField] protected LootTable lootTable;

    protected Rigidbody    _rb;
    protected NavMeshAgent _agent;
    protected Animator     _animator;
    protected Renderer     _renderer;
    protected float        _currentHealth;
    protected int          _currentPhase = 1;
    protected bool         _isDead;
    protected bool         _isTransitioningPhase;
    protected Transform    _player;

    public static event Action<BossAI>       OnBossSpawned;
    public static event Action<BossAI, int>  OnPhaseChanged;
    public static event Action<BossAI>       OnBossDefeated;
    public static event Action<float, float> OnBossHealthChanged;

    public bool   IsAlive      => !_isDead;
    public float  HealthRatio  => _currentHealth / maxHealth;
    public int    CurrentPhase => _currentPhase;
    public float  MaxHealth    => maxHealth;
    public string BossNameKR   => bossNameKR;

    protected static readonly int AnimPhase  = Animator.StringToHash("Phase");
    protected static readonly int AnimAttack = Animator.StringToHash("Attack");
    protected static readonly int AnimHurt   = Animator.StringToHash("Hurt");
    protected static readonly int AnimDead   = Animator.StringToHash("Dead");
    protected static readonly int AnimMoving = Animator.StringToHash("Moving");

    protected virtual void Awake()
    {
        _rb            = GetComponent<Rigidbody>();
        _agent         = GetComponent<NavMeshAgent>();
        _animator      = GetComponent<Animator>();
        _renderer      = GetComponentInChildren<Renderer>();
        _currentHealth = maxHealth;
        if (_rb) { _rb.isKinematic = true; _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ; }
    }

    protected virtual void Start()
    {
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc) _player = pc.transform;
        OnBossSpawned?.Invoke(this);
        BossHUDManager.Instance?.ShowBossHUD(this);
        StartCoroutine(BossLoop());
    }

    private IEnumerator BossLoop()
    {
        yield return OnBossStart();
        while (!_isDead)
        {
            if (_isTransitioningPhase) { yield return null; continue; }
            yield return _currentPhase switch { 1 => Phase1Loop(), 2 => Phase2Loop(), 3 => Phase3Loop(), _ => Phase1Loop() };
        }
    }

    protected abstract IEnumerator OnBossStart();
    protected abstract IEnumerator Phase1Loop();
    protected abstract IEnumerator Phase2Loop();
    protected abstract IEnumerator Phase3Loop();

    protected virtual IEnumerator TransitionToPhase(int newPhase)
    {
        _isTransitioningPhase = true;
        UIManager.Instance?.ShowNotification($"⚠️ {bossNameKR} — {newPhase}페이즈!");
        CameraController.Instance?.Shake(0.6f, 0.2f);
        yield return FlashEffect(Color.white, 0.15f, 5);
        _currentPhase = newPhase;
        _isTransitioningPhase = false;
        _animator?.SetInteger(AnimPhase, newPhase);
        OnPhaseChanged?.Invoke(this, newPhase);
        BossHUDManager.Instance?.OnPhaseChanged(newPhase);
    }

    public virtual void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (_isDead || _isTransitioningPhase) return;
        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        OnBossHealthChanged?.Invoke(_currentHealth, maxHealth);
        BossHUDManager.Instance?.UpdateHealth(_currentHealth, maxHealth);
        _animator?.SetTrigger(AnimHurt);
        StartCoroutine(HitFlash());
        if (_currentPhase == 1 && HealthRatio <= phase2Threshold) StartCoroutine(TransitionToPhase(2));
        else if (_currentPhase == 2 && HealthRatio <= phase3Threshold) StartCoroutine(TransitionToPhase(3));
        if (_currentHealth <= 0f) StartCoroutine(DieRoutine());
    }

    protected virtual IEnumerator DieRoutine()
    {
        _isDead = true;
        StopMovement();
        if (_agent) _agent.enabled = false;
        _animator?.SetBool(AnimDead, true);
        CameraController.Instance?.Shake(1.0f, 0.3f);
        yield return new WaitForSeconds(1.2f);
        lootTable?.DropLoot(transform.position);
        DungeonManager.Instance?.NotifyEnemyKilled();
        OnBossDefeated?.Invoke(this);
        BossHUDManager.Instance?.HideBossHUD();
        yield return new WaitForSeconds(1.5f);
        Destroy(gameObject);
    }

    // 3D 이동 유틸
    protected void MoveTowardPlayer(float speedMult = 1f)
    {
        if (_agent == null || _player == null) return;
        _agent.isStopped = false;
        _agent.speed     = moveSpeed * speedMult;
        _agent.SetDestination(_player.position);
        _animator?.SetBool(AnimMoving, true);
        FacePlayer();
    }

    protected void StopMovement()
    {
        if (_agent != null) { _agent.isStopped = true; _agent.velocity = Vector3.zero; }
        _animator?.SetBool(AnimMoving, false);
    }

    protected void FacePlayer()
    {
        if (_player == null) return;
        Vector3 dir = _player.position - transform.position; dir.y = 0;
        if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);
    }

    protected float DistanceToPlayer()
    {
        if (_player == null) return float.MaxValue;
        Vector3 d = _player.position - transform.position; d.y = 0;
        return d.magnitude;
    }

    protected Vector3 DirectionToPlayer()
    {
        if (_player == null) return Vector3.forward;
        Vector3 d = _player.position - transform.position; d.y = 0;
        return d.normalized;
    }

    // 3D 범위 데미지
    protected void DealDamageInRadius(float radius, float damage)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (var h in hits)
            if (h.TryGetComponent(out SurvivalStats s))
            { s.TakeDamage(damage, DamageType.Physical); CameraController.Instance?.Shake(0.3f, 0.12f); }
    }

    // 3D 발사체
    protected void FireProjectile(GameObject prefab, Vector3 dir, float speed, float damage)
    {
        if (prefab == null) return;
        dir.y = 0;
        var go = Instantiate(prefab, transform.position + Vector3.up, Quaternion.LookRotation(dir));
        go.GetComponent<Projectile>()?.Init(dir.normalized, damage, speed, 15f, gameObject);
    }

    protected void FireRadialProjectiles(GameObject prefab, int count, float speed, float damage)
    {
        float step = 360f / count;
        for (int i = 0; i < count; i++)
        {
            float a = i * step * Mathf.Deg2Rad;
            FireProjectile(prefab, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), speed, damage);
        }
    }

    protected IEnumerator FlashEffect(Color color, float interval, int times)
    {
        for (int i = 0; i < times; i++)
        {
            if (_renderer) _renderer.material.color = color;
            yield return new WaitForSeconds(interval);
            if (_renderer) _renderer.material.color = Color.white;
            yield return new WaitForSeconds(interval);
        }
    }

    private IEnumerator HitFlash()
    {
        if (_renderer) _renderer.material.color = new Color(1f, 0.4f, 0.4f);
        yield return new WaitForSeconds(0.08f);
        if (_renderer) _renderer.material.color = Color.white;
    }
}
