using System;
using UnityEngine;

namespace GravityRoguelike.World
{
    /// <summary>Deterministic, read-only gravity movement on a room's local cell grid.</summary>
    public static class GravityMovementSimulator
    {
        /// <summary>
        /// Moves through consecutive Empty cells along gravity, stopping before a
        /// non-empty cell or the grid boundary. Returns the same gravity direction.
        /// A supported state stays unchanged. No room cells or metadata are written.
        /// </summary>
        /// <exception cref="ArgumentNullException">The room is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The initial position or gravity is invalid.</exception>
        /// <exception cref="ArgumentException">The initial cell is not Empty.</exception>
        public static PlayerState SimulateGravity(RoomData room, PlayerState initialState)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (!room.ContainsLocal(initialState.Position))
                throw new ArgumentOutOfRangeException(nameof(initialState), initialState,
                    "Initial position must be inside the room's local grid.");
            if (room.GetCell(initialState.Position) != CellType.Empty)
                throw new ArgumentException("Initial position must be an Empty cell.", nameof(initialState));

            Vector2Int step = GetGravityStep(initialState.Gravity);
            Vector2Int position = initialState.Position;
            Vector2Int next = position + step;
            // Bounds are checked first, including for rooms with open perimeter cells.
            // Each step is monotonic along one axis, so a finite room always terminates.
            while (room.ContainsLocal(next) && room.GetCell(next) == CellType.Empty)
            {
                position = next;
                next = position + step;
            }

            return new PlayerState(position, initialState.Gravity);
        }

        private static Vector2Int GetGravityStep(GravityDirection gravity)
        {
            switch (gravity)
            {
                case GravityDirection.Down: return new Vector2Int(0, -1);
                case GravityDirection.Up: return new Vector2Int(0, 1);
                case GravityDirection.Left: return new Vector2Int(-1, 0);
                case GravityDirection.Right: return new Vector2Int(1, 0);
                default:
                    throw new ArgumentOutOfRangeException(nameof(gravity), gravity,
                        "Gravity must be Down, Up, Left, or Right.");
            }
        }
    }
}
