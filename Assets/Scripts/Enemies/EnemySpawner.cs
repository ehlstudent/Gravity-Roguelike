using System.Collections.Generic;
using UnityEngine;

// Spawns random enemies at random valid cells at scene start and injects their references.
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private Enemy[] enemyPrefabs;          // pool to pick from
    [SerializeField] private int enemyCount = 5;
    [SerializeField] private float minDistanceFromPlayer = 5f;   // no enemies spawning on top of the player

    private RoomGrid roomGrid;
    private Transform player;

    // Finds the scene references once, collects the usable cells, then spawns.
    private void Start()
    {
        roomGrid = FindFirstObjectByType<RoomGrid>();
        player = GameObject.FindWithTag("Player").transform;

        Vector2Int playerCell = roomGrid.WorldToCell(player.position);
        List<Vector2Int> cells = roomGrid.GetReachableCells(playerCell);

        for (int i = 0; i < enemyCount; i++)
        {
            Enemy prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            SpawnRandom(prefab, cells);
        }
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

        Vector2Int chosen = candidates[Random.Range(0, candidates.Count)];
        cells.Remove(chosen);   // so two enemies never share a cell

        Enemy enemy = Instantiate(prefab, roomGrid.CellToWorld(chosen), Quaternion.identity);
        enemy.Init(roomGrid, player);
    }
}