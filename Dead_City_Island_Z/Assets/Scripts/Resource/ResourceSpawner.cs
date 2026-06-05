using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 구역/바이옴별 자원 노드를 씬에 자동 배치
/// </summary>
public class ResourceSpawner : MonoBehaviour
{
    [Header("스폰 설정")]
    [SerializeField] private List<SpawnEntry> spawnEntries = new();
    [SerializeField] private float spawnRadius  = 50f;
    [SerializeField] private int   spawnAttempts= 30;      // 최대 시도 횟수
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float minSpacingBetweenNodes = 2f;

    private List<Vector2> _spawnedPositions = new();

    private void Start()
    {
        SpawnAll();
    }

    [ContextMenu("Respawn All")]
    public void SpawnAll()
    {
        _spawnedPositions.Clear();

        foreach (var entry in spawnEntries)
            SpawnEntry(entry);
    }

    private void SpawnEntry(SpawnEntry entry)
    {
        if (entry.prefab == null) return;

        int count = Random.Range(entry.minCount, entry.maxCount + 1);
        int spawned = 0;

        for (int attempt = 0; attempt < spawnAttempts && spawned < count; attempt++)
        {
            Vector2 randomPos = (Vector2)transform.position
                + Random.insideUnitCircle * spawnRadius;

            if (!IsValidSpawnPosition(randomPos)) continue;

            var go = Instantiate(entry.prefab,
                new Vector3(randomPos.x, randomPos.y, 0),
                Quaternion.identity, transform);

            _spawnedPositions.Add(randomPos);
            spawned++;
        }
    }

    private bool IsValidSpawnPosition(Vector2 pos)
    {
        // 장애물 체크
        if (Physics2D.OverlapCircle(pos, 0.4f, obstacleLayer)) return false;

        // 다른 노드와 거리 체크
        foreach (var existing in _spawnedPositions)
            if (Vector2.Distance(pos, existing) < minSpacingBetweenNodes)
                return false;

        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, spawnRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
#endif
}

[System.Serializable]
public class SpawnEntry
{
    public string     label;
    public GameObject prefab;
    public int        minCount = 3;
    public int        maxCount = 8;
    [Range(0f, 1f)]
    public float      spawnChance = 1f;
}
