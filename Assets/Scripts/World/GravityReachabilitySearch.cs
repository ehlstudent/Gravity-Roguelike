using System;
using System.Collections.Generic;
using UnityEngine;

namespace GravityRoguelike.World
{
    public static class GravityReachabilitySearch
    {
        private static readonly GravityDirection[] RotationOrder =
        {
            GravityDirection.Down, GravityDirection.Left, GravityDirection.Up, GravityDirection.Right
        };

        public static GravityReachabilityResult Search(RoomData room, Vector2Int start,
            GravityDirection initialGravity, Vector2Int target)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            ValidatePosition(room, start, nameof(start));
            ValidatePosition(room, target, nameof(target));
            var initial = new PlayerState(start, initialGravity);
            var queue = new Queue<PlayerState>();
            var visited = new HashSet<PlayerState> { initial };
            var parents = new Dictionary<PlayerState, PlayerState>();
            queue.Enqueue(initial);
            int explored = 0;
            var reachedStates = new List<PlayerState>();

            while (queue.Count > 0)
            {
                PlayerState current = queue.Dequeue();
                explored++;
                reachedStates.Add(current);
                if (current.Position == target)
                {
                    var path = new List<PlayerState> { current };
                    while (parents.TryGetValue(current, out PlayerState parent))
                    {
                        path.Add(parent);
                        current = parent;
                    }
                    path.Reverse();
                    return new GravityReachabilityResult(true, explored, path, reachedStates);
                }

                PlayerState settled = GravityMovementSimulator.SimulateGravity(room, current);
                if (settled != current)
                {
                    Enqueue(settled, current, queue, visited, parents);
                    continue;
                }

                int directionIndex = Array.IndexOf(RotationOrder, current.Gravity);
                for (int rotation = 1; rotation >= -1; rotation -= 2)
                {
                    GravityDirection direction = RotationOrder[(directionIndex + rotation + 4) % 4];
                    var rotated = new PlayerState(current.Position, direction);
                    PlayerState successor = GravityMovementSimulator.SimulateGravity(room, rotated);
                    Enqueue(successor, current, queue, visited, parents);
                }
            }

            return new GravityReachabilityResult(false, explored, new List<PlayerState>(), reachedStates);
        }

        private static void Enqueue(PlayerState next, PlayerState previous, Queue<PlayerState> queue,
            HashSet<PlayerState> visited, Dictionary<PlayerState, PlayerState> parents)
        {
            if (!visited.Add(next)) return;
            parents.Add(next, previous);
            queue.Enqueue(next);
        }

        private static void ValidatePosition(RoomData room, Vector2Int position, string parameterName)
        {
            if (!room.ContainsLocal(position))
                throw new ArgumentOutOfRangeException(parameterName, position, "Position must be inside the room in local coordinates.");
            if (room.GetCell(position) != CellType.Empty)
                throw new ArgumentException("Position must be an Empty cell.", parameterName);
        }
    }
}
