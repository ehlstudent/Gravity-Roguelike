using System;
using System.Collections.Generic;
using UnityEngine;

namespace GravityRoguelike.World
{
    /// <summary>A rectangular integer grid boundary containing non-overlapping rooms.</summary>
    public class WorldData
    {
        private readonly List<RoomData> rooms = new List<RoomData>();

        public RectInt Bounds { get; }
        public int Seed { get; }
        public IReadOnlyList<RoomData> Rooms { get; }

        public WorldData(RectInt bounds, int seed)
        {
            if (bounds.width <= 0 || bounds.height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bounds), bounds,
                    "World width and height must be positive.");
            }

            Bounds = bounds;
            Seed = seed;
            Rooms = rooms.AsReadOnly();
        }

        /// <summary>
        /// Adds a room as placed by the caller. Rejects null, duplicate IDs,
        /// out-of-world bounds and overlapping rooms. Touching edges are allowed.
        /// </summary>
        public void AddRoom(RoomData room)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            RectInt bounds = room.Bounds;
            // Maxima are exclusive, so equality at the world's outer edge is valid.
            if (bounds.xMin < Bounds.xMin || bounds.yMin < Bounds.yMin ||
                bounds.xMax > Bounds.xMax || bounds.yMax > Bounds.yMax)
            {
                throw new ArgumentException("Room bounds must be entirely inside the world bounds.", nameof(room));
            }

            foreach (RoomData existing in rooms)
            {
                if (existing.Id == room.Id)
                {
                    throw new ArgumentException($"Room ID {room.Id} already exists in this world.", nameof(room));
                }

                if (bounds.Overlaps(existing.Bounds))
                {
                    throw new ArgumentException($"Room {room.Id} overlaps room {existing.Id}.", nameof(room));
                }
            }

            rooms.Add(room);
        }
    }
}
