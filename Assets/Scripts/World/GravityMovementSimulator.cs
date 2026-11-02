using System;
using UnityEngine;

namespace GravityRoguelike.World
{
    public static class GravityMovementSimulator
    {
        public static PlayerState SimulateGravity(RoomData room, PlayerState initialState)
        {
            if (room == null)
                throw new ArgumentNullException(nameof(room));
            if (!room.ContainsLocal(initialState.Position))
                throw new ArgumentOutOfRangeException(nameof(initialState), "The starting position must be inside the room.");
            if (room.GetCell(initialState.Position) != CellType.Empty)
                throw new ArgumentException("The starting cell must be Empty.", nameof(initialState));

            Vector2Int step = GetGravityStep(initialState.Gravity);
            Vector2Int position = initialState.Position;
            Vector2Int next = position + step;
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
                case GravityDirection.Down: return Vector2Int.down;
                case GravityDirection.Up: return Vector2Int.up;
                case GravityDirection.Left: return Vector2Int.left;
                case GravityDirection.Right: return Vector2Int.right;
                default: throw new ArgumentOutOfRangeException(nameof(gravity), gravity, "Unknown gravity direction.");
            }
        }
    }
}
