using System;
using UnityEngine;

namespace GravityRoguelike.World
{
    /// <summary>Seeded rejection sampling of room bounds; no packing or backtracking.</summary>
    public static class RoomPlacementGenerator
    {
        /// <summary>
        /// Samples inclusive size ranges, capping maxima at the world dimensions.
        /// Gap applies once between rooms, not between rooms and the world boundary.
        /// Returns partial results when the total attempt budget is exhausted.
        /// </summary>
        public static RoomPlacementResult Generate(RectInt worldBounds, int worldSeed,
            int requestedCount, int minWidth, int maxWidth, int minHeight, int maxHeight,
            int minimumGap, int maxAttempts)
        {
            var world = new WorldData(worldBounds, worldSeed);
            // RectInt stores integer maxima; reject coordinates that would wrap around.
            if ((long)worldBounds.x + worldBounds.width > int.MaxValue ||
                (long)worldBounds.y + worldBounds.height > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(worldBounds), "World maximum coordinates must fit in an integer.");
            if (requestedCount < 0)
                throw new ArgumentOutOfRangeException(nameof(requestedCount), "Room count must be non-negative.");
            if (minimumGap < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumGap), "Gap must be non-negative.");
            if (minWidth <= 0 || maxWidth < minWidth)
                throw new ArgumentException("Room widths must be positive and minimum must not exceed maximum.", nameof(minWidth));
            if (minHeight <= 0 || maxHeight < minHeight)
                throw new ArgumentException("Room heights must be positive and minimum must not exceed maximum.", nameof(minHeight));
            if (requestedCount > 0 && maxAttempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Requested rooms require a positive attempt limit.");
            if (minWidth > worldBounds.width || minHeight > worldBounds.height)
                throw new ArgumentException("Minimum room size must fit inside the world.", nameof(worldBounds));

            maxWidth = Math.Min(maxWidth, worldBounds.width);
            maxHeight = Math.Min(maxHeight, worldBounds.height);
            var random = new System.Random(worldSeed);
            int attempts = 0;
            while (world.Rooms.Count < requestedCount && attempts < maxAttempts)
            {
                attempts++;
                int width = NextInclusive(random, minWidth, maxWidth);
                int height = NextInclusive(random, minHeight, maxHeight);
                int x = worldBounds.x + NextInclusive(random, 0, worldBounds.width - width);
                int y = worldBounds.y + NextInclusive(random, 0, worldBounds.height - height);
                var candidate = new RectInt(x, y, width, height);
                bool fits = true;
                foreach (RoomData room in world.Rooms)
                {
                    RectInt other = room.Bounds;
                    // At least one separating axis must have the full gap. Use long
                    // arithmetic so adding a large gap cannot overflow integer bounds.
                    bool separated = (long)candidate.xMax + minimumGap <= other.xMin ||
                        (long)other.xMax + minimumGap <= candidate.xMin ||
                        (long)candidate.yMax + minimumGap <= other.yMin ||
                        (long)other.yMax + minimumGap <= candidate.yMin;
                    if (!separated)
                    {
                        fits = false;
                        break;
                    }
                }

                if (fits)
                {
                    // Allocate the runtime grid only for accepted bounds.
                    world.AddRoom(new RoomData(world.Rooms.Count + 1, candidate, random.Next()));
                }
            }
            return new RoomPlacementResult(world, requestedCount, attempts);
        }

        private static int NextInclusive(System.Random random, int minimum, int maximum)
        {
            // Avoid maximum + 1 overflowing when the inclusive maximum is int.MaxValue.
            return (int)(minimum + (long)(random.NextDouble() * ((long)maximum - minimum + 1)));
        }
    }
}
