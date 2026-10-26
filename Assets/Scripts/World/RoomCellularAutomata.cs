using System;
using UnityEngine;

namespace GravityRoguelike.World
{
    /// <summary>Synchronous eight-neighbour starter rule, not a paper-specific implementation.</summary>
    public static class RoomCellularAutomata
    {
        public static void ValidateThreshold(int solidNeighbourThreshold)
        {
            if (solidNeighbourThreshold < 0 || solidNeighbourThreshold > 8)
                throw new ArgumentOutOfRangeException(nameof(solidNeighbourThreshold),
                    "Solid neighbour threshold must be from 0 through 8.");
        }

        /// <summary>
        /// Computes one complete next grid before writing any cells back.
        /// Reads current borders as neighbours; forces the next perimeter Solid.
        /// Rooms without an interior become entirely Solid.
        /// </summary>
        public static void ApplyStep(RoomData room, int solidNeighbourThreshold)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            ValidateThreshold(solidNeighbourThreshold);

            var next = new CellType[room.Width, room.Height];
            for (int y = 0; y < room.Height; y++)
            {
                for (int x = 0; x < room.Width; x++)
                {
                    if (x == 0 || y == 0 || x == room.Width - 1 || y == room.Height - 1)
                    {
                        next[x, y] = CellType.Solid;
                        continue;
                    }

                    int solidNeighbours = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (room.GetCell(new Vector2Int(x + dx, y + dy)) == CellType.Solid)
                                solidNeighbours++;
                        }
                    }
                    next[x, y] = solidNeighbours >= solidNeighbourThreshold ? CellType.Solid : CellType.Empty;
                }
            }

            // Separate write pass prevents earlier cells from influencing later evaluations.
            for (int y = 0; y < room.Height; y++)
                for (int x = 0; x < room.Width; x++)
                    room.SetCell(new Vector2Int(x, y), next[x, y]);
        }
    }
}
