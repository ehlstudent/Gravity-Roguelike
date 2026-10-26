using System;
using UnityEngine;

namespace GravityRoguelike.World
{
    public static class RoomNoiseGenerator
    {
        /// <summary>
        /// Resets the solid perimeter, then replaces every interior cell using room.Seed.
        /// Traversal is ascending y (bottom to top), then ascending x (left to right).
        /// Each interior cell consumes one random sample, including at probabilities 0 and 1.
        /// World position and previous contents do not affect the pattern.
        /// </summary>
        public static void Generate(RoomData room, double solidProbability)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (double.IsNaN(solidProbability) || solidProbability < 0 || solidProbability > 1)
                throw new ArgumentOutOfRangeException(nameof(solidProbability), solidProbability,
                    "Solid-cell probability must be between 0 and 1, inclusive, and cannot be NaN.");

            // Validate before touching cells. Narrow rooms remain entirely solid.
            RoomBorderGenerator.Generate(room);
            var random = new System.Random(room.Seed);
            for (int y = 1; y < room.Height - 1; y++)
            {
                for (int x = 1; x < room.Width - 1; x++)
                {
                    room.SetCell(new Vector2Int(x, y), random.NextDouble() < solidProbability
                        ? CellType.Solid : CellType.Empty);
                }
            }
        }
    }
}
