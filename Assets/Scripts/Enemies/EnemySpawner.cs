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

    private RoomGrid roomGrid;
    private Transform player;
    private System.Random rng;

    // Finds the scene references once, collects the usable cells, then spawns every entry.
    private void Start()
    {
        rng = useFixedSeed ? new System.Random(seed) : new System.Random();

        roomGrid = FindFirstObjectByType<RoomGrid>();
        player = GameObject.FindWithTag("Player").transform;

        Vector2Int playerCell = roomGrid.WorldToCell(player.position);
        List<Vector2Int> cells = roomGrid.GetReachableCells(playerCell);

        foreach (EnemySpawnEntry entry in enemies)
            for (int i = 0; i < entry.count; i++)
                SpawnRandom(entry.prefab, cells);
    }

    // Picks a random cell that suits this prefab, spawns it there and removes the cell from the pool.
    private void SpawnRandom(Enemy prefab, List<Vector2Int> cells)
    {
        var candidates = new List<Vector2Int>();
        foreach (Vector2Int c in cells)
        {
            bool farEnough = Vector2.Distance(roomGrid.CellToWorld(c), player.position) >= minDistanceFromPlayer;
            if (farEnough && prefab.CanSpawnAt(roomGrid, c)) candidates.Add(c);
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"No valid spawn cell for {prefab.name}");
            return;
        }

        Vector2Int chosen = candidates[rng.Next(candidates.Count)];
        cells.Remove(chosen);   // so two enemies never share a cell

        Enemy enemy = Instantiate(prefab, roomGrid.CellToWorld(chosen), Quaternion.identity);
        enemy.Init(roomGrid, player);
    }

    // Keeps counts from going negative when typed in the Inspector.
    private void OnValidate()
    {
        if (enemies == null) return;
        foreach (EnemySpawnEntry entry in enemies)
            entry.count = Mathf.Max(0, entry.count);
    }
}