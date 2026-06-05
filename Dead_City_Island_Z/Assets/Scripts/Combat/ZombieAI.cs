using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>좀비 AI (3D NavMeshAgent 기반)</summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAI : MonoBehaviour, IDamageable
{
    [Header("스탯")]
    [SerializeField] protected float maxHealth    = 50f;
    [SerializeField] private float attackDamage   = 8f;
    [SerializeField] private float attackRange    = 1.2f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float patrolSpeed    = 1.2f;
    [SerializeField] private float chaseSpeed     = 2.8f;
    [SerializeField] private float detectRange    = 6f;
    [SerializeField] private float loseRange      = 12f;
    [SerializeField] private float patrolRadius   = 5f;
    [SerializeField] private float patrolWaitTime = 2f;
    [SerializeField] private LootTable lootTable;

    protected Rigidbody    _rb;
    protected NavMeshAgent _agent;
    protected Animator     _animator;
    protected float        _currentHealth;
    private ZombieState    _state = ZombieState.Patrol;
    protected Transform    _target;
    private Vector3        _patrolTarget;
    private float          _patrolWaitTimer;
    private float          _attackTimer;
    protected bool         _isDead;

    public static event Action<ZombieAI> OnZombieDied;
    public bool IsAlive => !_isDead;

    private static readonly int AnimSpeed  = Animator.StringToHash("Speed");
    private static readonly int AnimDead   = Animator.StringToHash("Dead");
    private static readonly int AnimAttack = Animator.StringToHash("Attack");

    protected virtual void Awake()
    {
        _rb            = GetComponent<Rigidbody>();
        _agent         = GetComponent<NavMeshAgent>();
        _animator      = GetComponent<Animator>();
        _currentHealth = maxHealth;
        // NavMeshAgent가 이동 제어 — Rigidbody는 킨마틱
        _rb.isKinematic = true;
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    protected virtual void Start()
    {
        SetNewPatrolTarget();
        StartCoroutine(StateMachine());
    }

    private IEnumerator StateMachine()
    {
        while (!_isDead)
        {
            switch (_state)
            {
                case ZombieState.Patrol:  yield return PatrolLoop();  break;
                case ZombieState.Chase:   yield return ChaseLoop();   break;
                case ZombieState.Attack:  yield return AttackLoop();  break;
                default: yield return null; break;
            }
        }
    }

    private IEnumerator PatrolLoop()
    {
        _agent.speed = patrolSpeed;
        while (_state == ZombieState.Patrol && !_isDead)
        {
            if (TryDetectPlayer()) yield break;
            if (Vector3.Distance(transform.position, _patrolTarget) < 0.8f)
            {
                _agent.ResetPath();
                _patrolWaitTimer = patrolWaitTime;
                while (_patrolWaitTimer > 0 && _state == ZombieState.Patrol)
                { _patrolWaitTimer -= Time.deltaTime; if (TryDetectPlayer()) yield break; yield return null; }
                SetNewPatrolTarget();
            }
            else _agent.SetDestination(_patrolTarget);
            _animator?.SetFloat(AnimSpeed, _agent.velocity.magnitude);
            yield return null;
        }
    }

    private IEnumerator ChaseLoop()
    {
        _agent.speed = chaseSpeed;
        while (_state == ZombieState.Chase && !_isDead)
        {
            if (_target == null) { _state = ZombieState.Patrol; yield break; }
            float dist = Vector3.Distance(transform.position, _target.position);
            if (dist > loseRange)    { _state = ZombieState.Patrol; _target = null; yield break; }
            if (dist <= attackRange) { _state = ZombieState.Attack; yield break; }
            _agent.SetDestination(_target.position);
            _animator?.SetFloat(AnimSpeed, _agent.velocity.magnitude);
            yield return null;
        }
    }

    private IEnumerator AttackLoop()
    {
        _agent.ResetPath();
        while (_state == ZombieState.Attack && !_isDead)
        {
            if (_target == null) { _state = ZombieState.Patrol; yield break; }
            float dist = Vector3.Distance(transform.position, _target.position);
            if (dist > attackRange * 1.3f) { _state = ZombieState.Chase; yield break; }

            Vector3 dir = _target.position - transform.position; dir.y = 0;
            if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);

            if (_attackTimer <= 0)
            {
                _animator?.SetTrigger(AnimAttack);
                yield return new WaitForSeconds(0.3f);
                if (_target != null && _target.TryGetComponent(out SurvivalStats s))
                    s.TakeDamage(attackDamage, DamageType.Physical);
                _attackTimer = attackCooldown;
            }
            _attackTimer -= Time.deltaTime;
            _animator?.SetFloat(AnimSpeed, 0f);
            yield return null;
        }
    }

    protected bool TryDetectPlayer()
    {
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc == null) return false;
        if (Vector3.Distance(transform.position, pc.transform.position) > detectRange) return false;
        _target = pc.transform;
        _state  = ZombieState.Chase;
        return true;
    }

    public virtual void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (_isDead) return;
        _currentHealth -= damage;
        if (_state == ZombieState.Patrol) TryDetectPlayer();
        StartCoroutine(Knockback((transform.position - hitPoint).normalized * 2.5f));
        if (_currentHealth <= 0) Die();
    }

    private IEnumerator Knockback(Vector3 force)
    {
        _agent.enabled  = false;
        _rb.isKinematic = false;
        force.y = 0.2f;
        _rb.AddForce(force, ForceMode.Impulse);
        yield return new WaitForSeconds(0.25f);
        _rb.linearVelocity = Vector3.zero;
        _rb.isKinematic = true;
        _agent.enabled  = true;
    }

    protected virtual void Die()
    {
        _isDead = true;
        _agent.enabled = false;
        _rb.isKinematic = false;
        _animator?.SetBool(AnimDead, true);
        GetComponent<Collider>().enabled = false;
        lootTable?.DropLoot(transform.position);
        OnZombieDied?.Invoke(this);
        DungeonManager.Instance?.NotifyEnemyKilled();
        Destroy(gameObject, 3f);
    }

    private void SetNewPatrolTarget()
    {
        Vector3 rand = transform.position + new Vector3(
            UnityEngine.Random.Range(-patrolRadius, patrolRadius), 0,
            UnityEngine.Random.Range(-patrolRadius, patrolRadius));
        if (NavMesh.SamplePosition(rand, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
            _patrolTarget = hit.position;
    }
}

public enum ZombieState { Patrol, Chase, Attack, Stunned, Dead }
