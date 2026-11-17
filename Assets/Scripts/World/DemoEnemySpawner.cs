using System.Collections.Generic;
using UnityEngine;

namespace GravityRoguelike.World
{
    public class DemoEnemySpawner : MonoBehaviour
    {
        [SerializeField] private WalkerEnemy walker;
        [SerializeField] private BatBehaviour bat;
        [SerializeField] private RoomGrid grid;
        private readonly List<GameObject> spawned = new List<GameObject>();

        public void Clear()
        {
            foreach (var enemy in spawned)
            {
                if (enemy == null) continue;
                enemy.SetActive(false);
                Destroy(enemy);
            }
            spawned.Clear();
        }

        public void Populate(RoomData room, GravityEndpointPlacementResult leg, Transform player)
        {
            Clear();
            var cells = new List<Vector2Int>(leg.RestingCosts.Keys);
            cells.Sort((a, b) => a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var random = new System.Random(room.Seed);
            for (int i = cells.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }
            // Some routes have very few resting positions. The cleaned room also
            // has connected ledges and ceilings that enemies can occupy.
            int preferredCount = cells.Count;
            for (int y = 1; y < room.Height - 1; y++)
            for (int x = 1; x < room.Width - 1; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!leg.RestingCosts.ContainsKey(cell) && room.GetCell(cell) == CellType.Empty)
                    cells.Add(cell);
            }
            for (int i = cells.Count - 1; i > preferredCount; i--)
            {
                int j = random.Next(preferredCount, i + 1);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }
            var box = walker.GetComponent<BoxCollider2D>();
            var circle = bat.GetComponent<CircleCollider2D>();
            float cellHeight = Vector2.Distance(grid.CellToWorld(Vector2Int.zero), grid.CellToWorld(Vector2Int.up));
            // Leave one extra margin beyond the two collider contact offsets:
            // at exact tangency, rounding can classify the spawn as overlapping.
            float surfaceClearance = 3f * Physics2D.defaultContactOffset;
            bool placedWalker = false, placedBat = false;
            foreach (var cell in cells)
            {
                if (Vector2Int.Distance(cell, leg.Start.Value) < 4 || Vector2Int.Distance(cell, leg.Goal.Value) < 2)
                    continue;
                var world = room.LocalToWorld(cell);
                var center = grid.CellToWorld(world);
                int ground = 1 << grid.solid.gameObject.layer;
                if (!placedWalker && grid.IsSolid(world + Vector2Int.down))
                {
                    var hit = Physics2D.Raycast(center, Vector2.down, cellHeight, ground);
                    if (!hit || hit.normal.y < 0.9f) continue;
                    var pos = hit.point + Vector2.up * (box.size.y * 0.5f + surfaceClearance);
                    if (Physics2D.OverlapBox(pos, box.size, 0, ground)) continue;
                    var enemy = Instantiate(walker, pos - box.offset, Quaternion.identity);
                    enemy.Initialize(grid, player);
                    spawned.Add(enemy.gameObject);
                    placedWalker = true;
                }
                else if (!placedBat && grid.IsSolid(world + Vector2Int.up))
                {
                    var hit = Physics2D.Raycast(center, Vector2.up, cellHeight, ground);
                    if (!hit || hit.normal.y > -0.9f) continue;
                    var pos = hit.point + Vector2.down * (circle.radius + surfaceClearance);
                    if (Physics2D.OverlapCircle(pos, circle.radius, ground)) continue;
                    var enemy = Instantiate(bat, pos - circle.offset, Quaternion.identity);
                    var chase = enemy.GetComponent<EnemyChase>();
                    chase.grid = grid;
                    chase.player = player;
                    spawned.Add(enemy.gameObject);
                    placedBat = true;
                }
                if (placedWalker && placedBat) break;
            }
        }
    }
}
