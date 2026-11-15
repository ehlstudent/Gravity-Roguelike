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
            return Search(room, start, initialGravity, target, false);
        }

        public static GravityRotationEvaluation Evaluate(RoomData room, Vector2Int start,
            GravityDirection initialGravity, Vector2Int target, int minimum, int maximum)
        {
            if (minimum < 0) throw new ArgumentOutOfRangeException(nameof(minimum));
            if (maximum < minimum)
                throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum must be at least the minimum.");

            var route = Search(room, start, initialGravity, target, true);
            return new GravityRotationEvaluation(route, minimum, maximum);
        }

        internal sealed class StartRoutes
        {
            internal readonly Dictionary<Vector2Int, int> RestingCosts = new Dictionary<Vector2Int, int>();
            internal readonly Dictionary<Vector2Int, (PlayerState state, int explored)> FirstArrival =
                new Dictionary<Vector2Int, (PlayerState, int)>();
            internal readonly Dictionary<PlayerState, PlayerState> Parents = new Dictionary<PlayerState, PlayerState>();
        }

        internal static StartRoutes Explore(RoomData room, Vector2Int start, GravityDirection gravity)
        {
            var routes = new StartRoutes();
            Search(room, start, gravity, null, true, routes);
            return routes;
        }

        internal static List<PlayerState> BuildPath(PlayerState current, Dictionary<PlayerState, PlayerState> parents)
        {
            var path = new List<PlayerState> { current };
            while (parents.TryGetValue(current, out PlayerState parent))
            {
                path.Add(parent);
                current = parent;
            }
            path.Reverse();
            return path;
        }

        private static GravityReachabilityResult Search(RoomData room, Vector2Int start,
            GravityDirection initialGravity, Vector2Int? target, bool minimizeRotations, StartRoutes routes = null)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            ValidatePosition(room, start, nameof(start));
            if (target.HasValue) ValidatePosition(room, target.Value, nameof(target));
            var initial = new PlayerState(start, initialGravity);
            var queue = new LinkedList<(PlayerState state, int cost)>();
            var best = new Dictionary<PlayerState, int> { [initial] = 0 };
            var parents = routes?.Parents ?? new Dictionary<PlayerState, PlayerState>();
            queue.AddLast((initial, 0));

            void Relax(PlayerState next, PlayerState previous, int cost, int edgeCost)
            {
                int candidate = cost + edgeCost;
                if (best.TryGetValue(next, out int known) && known <= candidate) return;
                best[next] = candidate;
                parents[next] = previous;
                // Zero-cost falls precede pending rotations. Unit-cost search remains FIFO.
                if (edgeCost == 0) queue.AddFirst((next, candidate));
                else queue.AddLast((next, candidate));
            }

            int explored = 0;
            var reachedStates = routes == null ? new List<PlayerState>() : null;

            while (queue.Count > 0)
            {
                var (current, cost) = queue.First.Value;
                queue.RemoveFirst();
                if (cost != best[current]) continue;
                explored++;
                reachedStates?.Add(current);
                if (target.HasValue && current.Position == target.Value)
                    return new GravityReachabilityResult(true, explored, BuildPath(current, parents), reachedStates);

                PlayerState settled = GravityMovementSimulator.SimulateGravity(room, current);
                if (routes != null && settled == current && !routes.RestingCosts.ContainsKey(current.Position))
                {
                    routes.RestingCosts.Add(current.Position, cost);
                    routes.FirstArrival.Add(current.Position, (current, explored));
                }
                if (settled != current)
                {
                    Relax(settled, current, cost, minimizeRotations ? 0 : 1);
                    continue;
                }

                int directionIndex = Array.IndexOf(RotationOrder, current.Gravity);
                for (int rotation = 1; rotation >= -1; rotation -= 2)
                {
                    GravityDirection direction = RotationOrder[(directionIndex + rotation + 4) % 4];
                    var rotated = new PlayerState(current.Position, direction);
                    PlayerState successor = GravityMovementSimulator.SimulateGravity(room, rotated);
                    Relax(successor, current, cost, 1);
                }
            }

            if (routes != null) return null;
            return new GravityReachabilityResult(false, explored, new List<PlayerState>(), reachedStates);
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
