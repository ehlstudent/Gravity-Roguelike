using System.Collections.Generic;
using UnityEngine;

// One line in the Inspector list: which enemy, and how many of it.
[System.Serializable]
public class EnemySpawnEntry
{
    public Enemy prefab;
    public int count = 1;
}

// Spawns the configured enemies at random valid cells at scene start and injects their references.
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private EnemySpawnEntry[] enemies;          // e.g. Walker x2, Bat x3
    [SerializeField] private float minDistanceFromPlayer = 5f;   // no enemies spawning on top of the player
    [SerializeField] private bool useFixedSeed = true;           // off = a different layout every run
    [SerializeField] private int seed = 1234;                    // same seed = same enemy layout
    [Tooltip("Disable when the procedural demo calls SpawnInRoom after generation.")]
    [SerializeField] private bool spawnOnStart = true;

    private RoomGrid roomGrid;
    private Transform player;
    private System.Random rng;
    private readonly List<Enemy> spawned = new List<Enemy>();

    // Finds the scene references once, collects the usable cells, then spawns every entry.
    private void Start()
    {
        if (!spawnOnStart) return;
        rng = useFixedSeed ? new System.Random(seed) : new System.Random();

        roomGrid = FindFirstObjectByType<RoomGrid>();
        player = GameObject.FindWithTag("Player").transform;

        Vector2Int playerCell = roomGrid.WorldToCell(player.position);
        List<Vector2Int> cells = roomGrid.GetReachableCells(playerCell);

        foreach (EnemySpawnEntry entry in enemies)
            for (int i = 0; i < entry.count; i++)
                if (!SpawnRandom(entry.prefab, cells)) break;
    }

    /// <summary>Uses the existing spawn rules after a generated room and its arrival are known.</summary>
    public int SpawnInRoom(RoomGrid grid, Transform target, RectInt bounds, Vector2 entry,
        Vector2 exit, int roomSeed)
    {
        roomGrid = grid;
        player = target;
        rng = new System.Random(roomSeed);
        List<Vector2Int> cells = grid.GetReachableCells(grid.WorldToCell(entry));
        cells.RemoveAll(cell => !bounds.Contains(cell)
            || Vector2.Distance(grid.CellToWorld(cell), entry) < minDistanceFromPlayer
            || Vector2.Distance(grid.CellToWorld(cell), exit) < minDistanceFromPlayer);
        int count = 0;
        foreach (EnemySpawnEntry configuration in enemies)
            for (int i = 0; i < configuration.count; i++)
            {
                if (!SpawnRandom(configuration.prefab, cells)) break;
                count++;
            }
        return count;
    }

    /// <summary>Clears the previous run before its collision map is replaced.</summary>
    public void Clear()
    {
        foreach (Enemy enemy in spawned)
        {
            if (enemy == null) continue;
            enemy.gameObject.SetActive(false);
            Destroy(enemy.gameObject);
        }
        spawned.Clear();
    }

    // Picks a random cell that suits this prefab, spawns it there and removes the cell from the pool.
    private bool SpawnRandom(Enemy prefab, List<Vector2Int> cells)
    {
        var candidates = new List<Vector2Int>();
        foreach (Vector2Int c in cells)
        {
            bool farEnough = Vector2.Distance(roomGrid.CellToWorld(c), player.position) >= minDistanceFromPlayer;
            if (farEnough && prefab.CanSpawnAt(roomGrid, c) && BodyFits(prefab, c)) candidates.Add(c);
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"No valid spawn cell for {prefab.name}");
            return false;
        }

        Vector2Int chosen = candidates[rng.Next(candidates.Count)];
        cells.Remove(chosen);   // so two enemies never share a cell

        Enemy enemy = Instantiate(prefab, roomGrid.CellToWorld(chosen), Quaternion.identity);
        enemy.Init(roomGrid, player);
        spawned.Add(enemy);
        return true;
    }

    private bool BodyFits(Enemy prefab, Vector2Int cell)
    {
        BoxCollider2D box = prefab.GetComponent<BoxCollider2D>();
        if (box == null) return true;
        Vector2 scale = prefab.transform.lossyScale;
        Vector2 center = roomGrid.CellToWorld(cell) + Vector2.Scale(box.offset, scale);
        Vector2 size = Vector2.Scale(box.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
        return Physics2D.OverlapBox(center, size, 0, 1 << roomGrid.solid.gameObject.layer) == null;
    }

    // Keeps counts from going negative when typed in the Inspector.
    private void OnValidate()
    {
        if (enemies == null) return;
        foreach (EnemySpawnEntry entry in enemies)
            entry.count = Mathf.Max(0, entry.count);
    }
}
