using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>특수 좀비 3종 (3D — Runner/Bloater/Brute)</summary>
public class SpecialZombieAI : ZombieAI
{
    [Header("타입")]
    [SerializeField] private SpecialZombieType specialType = SpecialZombieType.Runner;

    [Header("Runner")]
    [SerializeField] private float runnerChaseSpeed  = 5.5f;
    [SerializeField] private float runnerLeapForce   = 8f;
    [SerializeField] private float runnerLeapCooldown= 3f;
    private float _leapTimer;

    [Header("Bloater")]
    [SerializeField] private float bloatExplosionRadius = 3f;
    [SerializeField] private float bloatExplosionDamage = 40f;
    [SerializeField] private float bloatTriggerRange    = 1.5f;
    [SerializeField] private GameObject explosionEffect;
    private bool _exploded;

    [Header("Brute")]
    [SerializeField] private float bruteKnockbackForce = 6f;
    [SerializeField] private float bruteSlamCooldown   = 4f;
    [SerializeField] private float bruteSlamRadius     = 2.5f;
    [SerializeField] private float bruteSlamDamage     = 50f;
    private float _slamTimer;

    protected new void Awake()
    {
        base.Awake();
        switch (specialType)
        {
            case SpecialZombieType.Runner:  transform.localScale *= 0.85f; break;
            case SpecialZombieType.Bloater: transform.localScale *= 1.3f; break;
            case SpecialZombieType.Brute:   transform.localScale *= 1.8f; maxHealth *= 3f; break;
        }
    }

    private void Update()
    {
        if (!IsAlive) return;
        switch (specialType)
        {
            case SpecialZombieType.Runner:  UpdateRunner();  break;
            case SpecialZombieType.Bloater: UpdateBloater(); break;
            case SpecialZombieType.Brute:   UpdateBrute();   break;
        }
    }

    private void UpdateRunner()
    {
        if (_agent) _agent.speed = runnerChaseSpeed;
        _leapTimer -= Time.deltaTime;
        if (_leapTimer > 0 || _target == null) return;
        float dist = Vector3.Distance(transform.position, _target.position);
        if (dist < 5f && dist > 1.5f) { StartCoroutine(LeapAttack()); _leapTimer = runnerLeapCooldown; }
    }

    private IEnumerator LeapAttack()
    {
        _agent.enabled  = false;
        _rb.isKinematic = false;
        Vector3 dir = (_target.position - transform.position).normalized;
        dir.y = 0.4f;
        _rb.AddForce(dir.normalized * runnerLeapForce, ForceMode.Impulse);
        yield return new WaitForSeconds(0.35f);
        _rb.linearVelocity = Vector3.zero;
        _rb.isKinematic    = true;
        _agent.enabled     = true;
    }

    private void UpdateBloater()
    {
        if (_exploded || _target == null) return;
        if (Vector3.Distance(transform.position, _target.position) <= bloatTriggerRange)
            StartCoroutine(Explode());
    }

    private IEnumerator Explode()
    {
        _exploded = true;
        var rend = GetComponentInChildren<Renderer>();
        for (int i = 0; i < 4; i++)
        {
            if (rend) rend.material.color = Color.red;
            yield return new WaitForSeconds(0.15f);
            if (rend) rend.material.color = Color.white;
            yield return new WaitForSeconds(0.15f);
        }
        if (explosionEffect) Instantiate(explosionEffect, transform.position, Quaternion.identity);
        // 3D OverlapSphere
        Collider[] hits = Physics.OverlapSphere(transform.position, bloatExplosionRadius);
        foreach (var h in hits)
            if (h.TryGetComponent(out SurvivalStats s)) s.TakeDamage(bloatExplosionDamage, DamageType.Physical);
        TakeDamage(9999f, transform.position);
    }

    private void UpdateBrute()
    {
        _slamTimer -= Time.deltaTime;
        if (_slamTimer > 0 || _target == null) return;
        if (Vector3.Distance(transform.position, _target.position) > bruteSlamRadius) return;
        StartCoroutine(SlamAttack());
        _slamTimer = bruteSlamCooldown;
    }

    private IEnumerator SlamAttack()
    {
        yield return new WaitForSeconds(0.5f);
        CameraController.Instance?.Shake(0.4f, 0.15f);
        Collider[] hits = Physics.OverlapSphere(transform.position, bruteSlamRadius);
        foreach (var h in hits)
        {
            if (h.TryGetComponent(out SurvivalStats s)) s.TakeDamage(bruteSlamDamage, DamageType.Physical);
            if (h.TryGetComponent(out Rigidbody rb))
            {
                Vector3 dir = (h.transform.position - transform.position).normalized;
                dir.y = 0.3f;
                bool wasKin = rb.isKinematic;
                rb.isKinematic = false;
                rb.AddForce(dir.normalized * bruteKnockbackForce, ForceMode.Impulse);
            }
        }
    }
}
public enum SpecialZombieType { Runner, Bloater, Brute }
