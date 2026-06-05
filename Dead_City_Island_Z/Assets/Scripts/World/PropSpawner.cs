using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>환경 오브젝트 배치 — NavMesh.SamplePosition 기반 3D 배치</summary>
public class PropSpawner : MonoBehaviour
{
    [SerializeField] private List<PropEntry> islandProps = new();
    [SerializeField] private List<PropEntry> cityProps   = new();
    [SerializeField] private int   attemptsPerChunk    = 20;
    [SerializeField] private float minPropSpacing      = 2f;
    [SerializeField] private LayerMask obstacleLayer;

    private Dictionary<Vector2Int, List<GameObject>> _chunkProps = new();
    private System.Random _rng;

    private void OnEnable()  => WorldGenerator.OnChunkLoaded += OnChunkLoaded;
    private void OnDisable() => WorldGenerator.OnChunkLoaded -= OnChunkLoaded;
    private void Start()     { _rng = new System.Random(WorldGenerator.Instance?.Seed ?? 0); }

    private void OnChunkLoaded(Vector2Int coord)
    {
        if (_chunkProps.ContainsKey(coord)) return;
        var wg    = WorldGenerator.Instance; if (wg == null) return;
        var props = wg.CurrentWorldType == WorldType.Island ? islandProps : cityProps;
        var spawned = new List<GameObject>(); var positions = new List<Vector3>();
        int size = 16; float wx = coord.x * size, wz = coord.y * size;

        foreach (var entry in props)
        {
            int count = _rng.Next(entry.minCount, entry.maxCount + 1);
            for (int i = 0; i < count; i++)
            {
                for (int att = 0; att < attemptsPerChunk; att++)
                {
                    Vector3 candidate = new Vector3(wx + (float)_rng.NextDouble()*size, 0f, wz + (float)_rng.NextDouble()*size);
                    // NavMesh 위 유효 위치 탐색 (3D)
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;
                    candidate = hit.position;
                    bool close = false; foreach (var ep in positions) if (Vector3.Distance(candidate, ep) < minPropSpacing) { close = true; break; }
                    if (close) continue;
                    if (Physics.CheckSphere(candidate + Vector3.up * 0.5f, 0.4f, obstacleLayer)) continue;
                    if (entry.prefab == null) break;
                    // Y축 랜덤 회전 (탑다운에서 의미있는 회전)
                    float yRot = entry.randomRotation ? (float)_rng.NextDouble() * 360f : 0f;
                    var go = Instantiate(entry.prefab, candidate, Quaternion.Euler(0f, yRot, 0f), transform);
                    spawned.Add(go); positions.Add(candidate); break;
                }
            }
        }
        _chunkProps[coord] = spawned;
    }

    public void UnloadChunk(Vector2Int coord)
    {
        if (!_chunkProps.TryGetValue(coord, out var list)) return;
        foreach (var go in list) if (go) Destroy(go);
        _chunkProps.Remove(coord);
    }
}
