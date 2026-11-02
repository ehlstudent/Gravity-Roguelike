using System;
using System.Collections.Generic;
using UnityEngine;

namespace GravityRoguelike.World
{
    public static class GravityRoomRepair
    {
        private static readonly Vector2Int[] Neighbours =
        {
            Vector2Int.down, Vector2Int.left, Vector2Int.up, Vector2Int.right
        };

        public static GravityRoomRepairResult Repair(RoomData room, Vector2Int start,
            GravityDirection initialGravity, Vector2Int target, int maxRepairAttempts = 50, int maxCarvedCells = 8)
        {
            if (maxRepairAttempts < 0) throw new ArgumentOutOfRangeException(nameof(maxRepairAttempts));
            if (maxCarvedCells < 0) throw new ArgumentOutOfRangeException(nameof(maxCarvedCells));
            GravityReachabilityResult initial = GravityReachabilitySearch.Search(room, start, initialGravity, target);
            if (initial.Reachable)
                return new GravityRoomRepairResult(true, false, 0, new List<Vector2Int>(), "Already reachable.");
            if (maxRepairAttempts == 0 || maxCarvedCells == 0)
                return new GravityRoomRepairResult(false, false, 0, new List<Vector2Int>(), "Repair budget is zero.");

            var pending = new Queue<List<Vector2Int>>();
            var scheduled = new HashSet<string>();
            Schedule(room, initial, new List<Vector2Int>(), target, maxRepairAttempts, maxCarvedCells, pending, scheduled);
            int attempts = 0;
            while (pending.Count > 0 && attempts < maxRepairAttempts)
            {
                List<Vector2Int> carved = pending.Dequeue();
                RoomData working = room.Copy();
                foreach (Vector2Int cell in carved) working.SetCell(cell, CellType.Empty);
                attempts++;
                GravityReachabilityResult check = GravityReachabilitySearch.Search(working, start, initialGravity, target);
                if (check.Reachable)
                {
                    foreach (Vector2Int cell in carved) room.SetCell(cell, CellType.Empty);
                    return new GravityRoomRepairResult(false, true, attempts, carved, "Verified by gravity reachability.");
                }
                Schedule(working, check, carved, target, maxRepairAttempts, maxCarvedCells, pending, scheduled);
            }

            string reason = attempts == maxRepairAttempts
                ? "Repair attempt limit reached."
                : "No valid repair found among frontier candidates within the carving limit.";
            return new GravityRoomRepairResult(false, false, attempts, new List<Vector2Int>(), reason);
        }

        private static void Schedule(RoomData room, GravityReachabilityResult reachability,
            List<Vector2Int> carved, Vector2Int target, int maxAttempts, int maxCells,
            Queue<List<Vector2Int>> pending, HashSet<string> scheduled)
        {
            if (carved.Count >= maxCells || scheduled.Count >= maxAttempts) return;
            var unique = new HashSet<Vector2Int>();
            foreach (PlayerState state in reachability.ReachedStates)
            foreach (Vector2Int step in Neighbours)
            {
                Vector2Int cell = state.Position + step;
                if (cell.x > 0 && cell.x < room.Width - 1 && cell.y > 0 && cell.y < room.Height - 1
                    && room.GetCell(cell) == CellType.Solid) unique.Add(cell);
            }
            var candidates = new List<Vector2Int>(unique);
            candidates.Sort((a, b) =>
            {
                long distanceA = Math.Abs((long)a.x - target.x) + Math.Abs((long)a.y - target.y);
                long distanceB = Math.Abs((long)b.x - target.x) + Math.Abs((long)b.y - target.y);
                int comparison = distanceA.CompareTo(distanceB);
                return comparison != 0 ? comparison : CompareCells(a, b);
            });
            foreach (Vector2Int cell in candidates)
            {
                if (scheduled.Count >= maxAttempts) break;
                var next = new List<Vector2Int>(carved) { cell };
                next.Sort(CompareCells);
                var keys = new string[next.Count];
                for (int i = 0; i < next.Count; i++) keys[i] = next[i].x + "," + next[i].y;
                if (scheduled.Add(string.Join(";", keys))) pending.Enqueue(next);
            }
        }

        private static int CompareCells(Vector2Int a, Vector2Int b)
        {
            int comparison = a.y.CompareTo(b.y);
            return comparison != 0 ? comparison : a.x.CompareTo(b.x);
        }
    }
}
