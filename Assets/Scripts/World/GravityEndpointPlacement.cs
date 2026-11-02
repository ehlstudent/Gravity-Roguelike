using System;
using System.Collections.Generic;
using UnityEngine;

namespace GravityRoguelike.World
{
    public sealed class GravityEndpointPlacementResult
    {
        public bool Succeeded => Start.HasValue;
        public Vector2Int? Start { get; internal set; }
        public Vector2Int? Goal { get; internal set; }
        public GravityDirection InitialGravity { get; internal set; }
        public int? Distance { get; internal set; }
        public int? MinimumRotations { get; internal set; }
        public int EvaluatedPairs { get; internal set; }
        public IReadOnlyList<PlayerState> Path { get; internal set; }
        public string FailureReason { get; internal set; }
    }

    public static class GravityEndpointPlacement
    {
        public static GravityEndpointPlacementResult Find(RoomData room, GravityDirection gravity,
            int minRotations, int maxRotations, int minDistance, int maxEvaluations,
            Func<GravityRotationEvaluation, bool> acceptRoute = null, System.Random order = null)
        {
            var result = new GravityEndpointPlacementResult { InitialGravity = gravity };
            GravityEndpointPlacementResult Fail(string reason)
            {
                result.FailureReason = reason;
                return result;
            }

            if (room == null) return Fail("A room is required.");
            if (!Enum.IsDefined(typeof(GravityDirection), gravity)) return Fail("Unknown initial gravity.");
            if (minRotations < 0 || maxRotations < minRotations) return Fail("Invalid required rotation range.");
            if (minDistance < 0 || maxEvaluations < 0) return Fail("Distance and evaluation limit must be non-negative.");

            // Issue 10 accepts midair Empty starts and settles them under the initial gravity.
            var cells = new List<Vector2Int>();
            for (int y = 0; y < room.Height; y++)
            for (int x = 0; x < room.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (room.GetCell(cell) == CellType.Empty) cells.Add(cell);
            }
            if (cells.Count < 2) return Fail("At least two distinct Empty cells are required.");

            if (order != null)
            {
                for (int i = cells.Count - 1; i > 0; i--)
                {
                    int j = order.Next(i + 1);
                    (cells[i], cells[j]) = (cells[j], cells[i]);
                }
            }

            // Stream ordered pairs. Row-major start, then goal, breaks equal-distance ties.
            foreach (var start in cells)
            foreach (var goal in cells)
            {
                int distance = Math.Abs(start.x - goal.x) + Math.Abs(start.y - goal.y);
                if (distance == 0 || distance < minDistance) continue;
                if (result.EvaluatedPairs == maxEvaluations)
                    return result.Succeeded ? result : Fail("No qualifying pair found within the evaluation budget.");
                result.EvaluatedPairs++;
                var route = GravityReachabilitySearch.Evaluate(room, start, gravity, goal, minRotations, maxRotations);
                if (!route.MeetsRequirements || (result.Succeeded && distance <= result.Distance.Value)) continue;
                if (acceptRoute != null && !acceptRoute(route)) continue;
                result.Start = start;
                result.Goal = goal;
                result.Distance = distance;
                result.MinimumRotations = route.MinimumRequiredGravityRotations;
                result.Path = route.Path;
            }
            if (result.Succeeded) return result;
            return Fail(result.EvaluatedPairs == 0 ? "No distinct pair meets the minimum distance."
                : "All distance-qualified pairs were evaluated; none met the gravity requirements.");
        }
    }
}
