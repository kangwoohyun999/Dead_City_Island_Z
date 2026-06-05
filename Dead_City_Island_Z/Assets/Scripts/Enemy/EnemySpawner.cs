using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>적 스폰 — NavMesh.SamplePosition 기반 3D 스폰</summary>
public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner Instance { get; private set; }

    [SerializeField] private List<EnemySpawnConfig> enemyConfigs = new();
    [SerializeField] private float spawnInterval     = 15f;
    [SerializeField] private float spawnRadiusMin    = 8f;
    [SerializeField] private float spawnRadiusMax    = 14f;
    [SerializeField] private int   maxEnemiesPerZone = 20;
    [SerializeField] private float daySpawnMult      = 0.5f;
    [SerializeField] private float nightSpawnMult    = 2.0f;
    [SerializeField] private int   poolSizePerType   = 15;

    private Dictionary<string, Queue<GameObject>> _pool = new();
    private List<GameObject> _activeEnemies = new();
    private Transform _playerTf;
    private DangerLevel _danger = DangerLevel.Safe;
    private bool _isDay = true;
    private Coroutine _spawnCo;

    public static event Action<int> OnEnemyCountChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        InitPool();
    }

    private void OnEnable()
    {
        MapSystem.OnZoneEntered            += OnZoneEntered;
        MapSystem.OnZoneExited             += OnZoneExited;
        GameManager.OnDayNightCycleChanged += b => { _isDay = b; };
        ZombieAI.OnZombieDied              += z => { _activeEnemies.Remove(z.gameObject); OnEnemyCountChanged?.Invoke(_activeEnemies.Count); };
    }

    private void OnDisable()
    {
        MapSystem.OnZoneEntered            -= OnZoneEntered;
        MapSystem.OnZoneExited             -= OnZoneExited;
    }

    private void Start() { var pc = FindFirstObjectByType<PlayerController>(); if (pc) _playerTf = pc.transform; }

    private void InitPool()
    {
        foreach (var c in enemyConfigs)
        {
            if (c.prefab == null) continue;
            _pool[c.enemyID] = new Queue<GameObject>();
            for (int i = 0; i < poolSizePerType; i++) { var go = Instantiate(c.prefab, transform); go.SetActive(false); _pool[c.enemyID].Enqueue(go); }
        }
    }

    private void OnZoneEntered(ZoneData zone)
    {
        _danger = zone.dangerLevel;
        if (zone.IsSafeZone) { if (_spawnCo != null) StopCoroutine(_spawnCo); DespawnAll(); }
        else { if (_spawnCo != null) StopCoroutine(_spawnCo); _spawnCo = StartCoroutine(SpawnLoop()); }
    }

    private void OnZoneExited(ZoneData zone) { if (!zone.IsSafeZone && _spawnCo != null) { StopCoroutine(_spawnCo); _spawnCo = null; } }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            float mult = _isDay ? daySpawnMult : nightSpawnMult;
            yield return new WaitForSeconds(spawnInterval / mult);
            if (_activeEnemies.Count < maxEnemiesPerZone && _playerTf != null) SpawnWave();
        }
    }

    private void SpawnWave()
    {
        var eligible = enemyConfigs.FindAll(c => c.minDanger <= _danger && c.maxDanger >= _danger);
        if (eligible.Count == 0) return;
        int count = _danger switch { DangerLevel.Low=>UnityEngine.Random.Range(1,3), DangerLevel.Medium=>UnityEngine.Random.Range(2,5), DangerLevel.High=>UnityEngine.Random.Range(4,8), DangerLevel.Extreme=>UnityEngine.Random.Range(6,12), _=>0 };
        for (int i = 0; i < count; i++)
        {
            var cfg = WeightedRandom(eligible);
            if (cfg == null) continue;
            var pos = GetNavMeshPos();
            if (!pos.HasValue) continue;
            var go = _pool.TryGetValue(cfg.enemyID, out var q) && q.Count > 0 ? q.Dequeue() : Instantiate(cfg.prefab);
            go.SetActive(true);
            go.transform.position = pos.Value;
            go.transform.rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
            if (go.TryGetComponent(out NavMeshAgent agent)) { agent.enabled = false; agent.enabled = true; agent.Warp(pos.Value); }
            _activeEnemies.Add(go);
            OnEnemyCountChanged?.Invoke(_activeEnemies.Count);
        }
    }

    // NavMesh 위 유효 스폰 위치
    private Vector3? GetNavMeshPos()
    {
        if (_playerTf == null) return null;
        for (int i = 0; i < 10; i++)
        {
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = UnityEngine.Random.Range(spawnRadiusMin, spawnRadiusMax);
            Vector3 candidate = _playerTf.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) return hit.position;
        }
        return null;
    }

    public void DespawnAll() { foreach (var go in _activeEnemies) if (go) go.SetActive(false); _activeEnemies.Clear(); OnEnemyCountChanged?.Invoke(0); }

    private EnemySpawnConfig WeightedRandom(List<EnemySpawnConfig> list)
    {
        float total = 0; foreach (var c in list) total += c.weight;
        float roll = UnityEngine.Random.Range(0f, total);
        foreach (var c in list) { roll -= c.weight; if (roll <= 0) return c; }
        return list[list.Count - 1];
    }
}
