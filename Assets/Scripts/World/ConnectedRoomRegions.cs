using System;
using System.Collections.Generic;
using UnityEngine;

namespace GravityRoguelike.World
{
    public static class ConnectedRoomRegions
    {
        private static readonly Vector2Int[] Neighbours =
            { Vector2Int.down, Vector2Int.left, Vector2Int.up, Vector2Int.right };

        /// <summary>Room-local components in row-major discovery order. Only shared edges connect cells.</summary>
        public static IReadOnlyList<IReadOnlyList<Vector2Int>> Detect(RoomData room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            var regions = new List<IReadOnlyList<Vector2Int>>();
            var visited = new bool[room.Width, room.Height];

            bool Discover(Vector2Int cell)
            {
                if (!room.ContainsLocal(cell) || visited[cell.x, cell.y]) return false;
                visited[cell.x, cell.y] = true;
                return room.GetCell(cell) == CellType.Empty;
            }

            for (int y = 0; y < room.Height; y++)
            for (int x = 0; x < room.Width; x++)
            {
                var start = new Vector2Int(x, y);
                if (!Discover(start)) continue;
                var cells = new List<Vector2Int> { start };
                // Process the growing list as a FIFO queue and retain it as the region snapshot.
                for (int i = 0; i < cells.Count; i++)
                foreach (var offset in Neighbours)
                {
                    var next = cells[i] + offset;
                    if (Discover(next)) cells.Add(next);
                }
                regions.Add(cells.AsReadOnly());
            }
            return regions.AsReadOnly();
        }
    }
}
