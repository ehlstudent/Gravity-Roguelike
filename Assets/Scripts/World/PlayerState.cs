using System;
using UnityEngine;

namespace GravityRoguelike.World
{
    /// <summary>
    /// Immutable logical state for future room-local reachability searches.
    /// Position uses RoomData's local cell coordinates, without world offsets.
    /// Both position and gravity participate in equality and hashing.
    /// Room ownership and continuous physics are handled outside this type.
    /// </summary>
    public readonly struct PlayerState : IEquatable<PlayerState>
    {
        public Vector2Int Position { get; }
        public GravityDirection Gravity { get; }

        public PlayerState(Vector2Int position, GravityDirection gravity)
        {
            if (gravity != GravityDirection.Down && gravity != GravityDirection.Up &&
                gravity != GravityDirection.Left && gravity != GravityDirection.Right)
                throw new ArgumentOutOfRangeException(nameof(gravity), gravity,
                    "Gravity must be Down, Up, Left, or Right.");

            Position = position;
            Gravity = gravity;
        }

        public bool Equals(PlayerState other)
        {
            return Position.Equals(other.Position) && Gravity == other.Gravity;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Position.GetHashCode() * 397) ^ (int)Gravity;
            }
        }

        public static bool operator ==(PlayerState left, PlayerState right) => left.Equals(right);
        public static bool operator !=(PlayerState left, PlayerState right) => !left.Equals(right);

        public override string ToString() => $"({Position.x}, {Position.y}, {Gravity})";
    }
}
