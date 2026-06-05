using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>월드 생성 — Tilemap 없이 3D 오브젝트를 XZ 평면에 배치</summary>
public class WorldGenerator : MonoBehaviour
{
    public static WorldGenerator Instance { get; private set; }

    [SerializeField] private WorldType worldType = WorldType.Island;
    [SerializeField] private int seed = 0;
    [SerializeField] private bool randomSeed = true;
    [SerializeField] private int chunkSize = 16, viewDistance = 3, maxCachedChunks = 25;
    [SerializeField] private int mapWidth = 20, mapHeight = 20;

    // 3D 지형 프리팹 (Tilemap 대신)
    [Header("섬 지형 프리팹")]
    [SerializeField] private GameObject waterPlanePrefab, sandPlanePrefab, grassPlanePrefab, rockPlanePrefab;
    [Header("도시 지형 프리팹")]
    [SerializeField] private GameObject concretePlanePrefab, asphaltPlanePrefab, rubblePlanePrefab;

    private Dictionary<Vector2Int, ChunkData>  _loadedChunks    = new();
    private HashSet<Vector2Int>                _generatedChunks = new();
    private Dictionary<Vector2Int, GameObject> _chunkRoots      = new();
    private Queue<Vector2Int>                  _loadQueue       = new();
    private Transform _player;
    private Vector2Int _lastChunk = new(-999,-999);
    private System.Random _rng;
    private float[,] _noiseMap;

    public static event Action<Vector2Int> OnChunkLoaded;
    public static event Action             OnWorldGenerated;

    public WorldType CurrentWorldType => worldType;
    public int       Seed             => seed;

    private void Awake() { if (Instance != null && Instance != this) { Destroy(this); return; } Instance = this; if (randomSeed) seed = UnityEngine.Random.Range(0,999999); _rng = new System.Random(seed); }

    private void Start() { var pc = FindFirstObjectByType<PlayerController>(); if (pc) _player = pc.transform; GenerateNoise(); StartCoroutine(InitLoad()); }

    private void Update()
    {
        if (_player == null) return;
        var chunk = ToChunk(_player.position);
        if (chunk != _lastChunk) { _lastChunk = chunk; UpdateChunks(chunk); }
        if (_loadQueue.Count > 0) { GenerateChunk(_loadQueue.Dequeue()); GenerateChunk(_loadQueue.Count > 0 ? _loadQueue.Dequeue() : chunk); }
    }

    private void GenerateNoise()
    {
        int tw = mapWidth * chunkSize, th = mapHeight * chunkSize;
        _noiseMap = new float[tw, th];
        float scale = 0.04f, ox = (float)_rng.NextDouble()*1000f, oz = (float)_rng.NextDouble()*1000f;
        for (int x = 0; x < tw; x++) for (int z = 0; z < th; z++)
        {
            float n = 0, amp = 1, freq = 1, max = 0;
            for (int o = 0; o < 4; o++) { n += Mathf.PerlinNoise((x*scale+ox)*freq, (z*scale+oz)*freq)*amp; max += amp; amp *= 0.5f; freq *= 2f; }
            n /= max;
            if (worldType == WorldType.Island) { float cx=(float)x/tw-0.5f, cz=(float)z/th-0.5f; n -= Mathf.Sqrt(cx*cx+cz*cz)*2f*0.6f; }
            _noiseMap[x,z] = Mathf.Clamp01(n);
        }
    }

    private IEnumerator InitLoad()
    {
        for (int x = -viewDistance; x <= viewDistance; x++) for (int z = -viewDistance; z <= viewDistance; z++) { GenerateChunk(new Vector2Int(x,z)); yield return null; }
        OnWorldGenerated?.Invoke();
    }

    private void UpdateChunks(Vector2Int center)
    {
        var toUnload = new List<Vector2Int>();
        foreach (var c in _loadedChunks.Keys) if (Mathf.Max(Mathf.Abs(c.x-center.x), Mathf.Abs(c.y-center.y)) > viewDistance+1) toUnload.Add(c);
        foreach (var c in toUnload) UnloadChunk(c);
        for (int x = -viewDistance; x <= viewDistance; x++) for (int z = -viewDistance; z <= viewDistance; z++)
        { var c = new Vector2Int(center.x+x, center.y+z); if (InBounds(c) && !_generatedChunks.Contains(c)) _loadQueue.Enqueue(c); }
    }

    private void GenerateChunk(Vector2Int coord)
    {
        if (_generatedChunks.Contains(coord) || !InBounds(coord)) return;
        _generatedChunks.Add(coord);
        var root = new GameObject($"Chunk_{coord.x}_{coord.y}"); root.transform.SetParent(transform);
        _chunkRoots[coord] = root;
        var chunk = new ChunkData(coord); _loadedChunks[coord] = chunk;
        int wx = coord.x * chunkSize, wz = coord.y * chunkSize;
        for (int lx = 0; lx < chunkSize; lx++) for (int lz = 0; lz < chunkSize; lz++)
        {
            int gx = Mathf.Clamp(wx+lx+mapWidth*chunkSize/2, 0, mapWidth*chunkSize-1);
            int gz = Mathf.Clamp(wz+lz+mapHeight*chunkSize/2, 0, mapHeight*chunkSize-1);
            float noise = _noiseMap[gx, gz];
            // 3D 오브젝트를 XZ 평면에 배치 (Y=0)
            Vector3 pos = new Vector3(wx+lx+0.5f, 0f, wz+lz+0.5f);
            var prefab = GetPrefab(GetTileType(noise));
            if (prefab != null) { var go = Instantiate(prefab, pos, Quaternion.identity, root.transform); go.isStatic = true; }
        }
        OnChunkLoaded?.Invoke(coord);
    }

    private void UnloadChunk(Vector2Int c)
    {
        _loadedChunks.Remove(c);
        if (_chunkRoots.TryGetValue(c, out var root)) { Destroy(root); _chunkRoots.Remove(c); }
    }

    private TileType GetTileType(float n)
    {
        if (worldType == WorldType.Island) { if (n<0.20f) return TileType.Water; if (n<0.32f) return TileType.Sand; if (n<0.70f) return TileType.Grass; return TileType.Rock; }
        else { if (n<0.25f) return TileType.Road; if (n<0.65f) return TileType.Concrete; return TileType.Wall; }
    }

    private GameObject GetPrefab(TileType t) => worldType == WorldType.Island
        ? t switch { TileType.Water=>waterPlanePrefab, TileType.Sand=>sandPlanePrefab, TileType.Grass=>grassPlanePrefab, TileType.Rock=>rockPlanePrefab, _=>grassPlanePrefab }
        : t switch { TileType.Road=>asphaltPlanePrefab, TileType.Concrete=>concretePlanePrefab, _=>rubblePlanePrefab };

    // 3D XZ → 청크 좌표 (Z 사용)
    public Vector2Int ToChunk(Vector3 pos) => new Vector2Int(Mathf.FloorToInt(pos.x/chunkSize), Mathf.FloorToInt(pos.z/chunkSize));
    private bool InBounds(Vector2Int c) => c.x>=-mapWidth/2&&c.x<mapWidth/2&&c.y>=-mapHeight/2&&c.y<mapHeight/2;
    public TileType GetTileAt(Vector3 pos) { var c = ToChunk(pos); if (_loadedChunks.TryGetValue(c, out var d)) { var k = new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.z)); if (d.tileTypes.TryGetValue(k, out var t)) return t; } return TileType.Unknown; }
}
