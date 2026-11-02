using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GravityRoguelike.World
{
    public sealed class RoomRegionCleanupResult
    {
        public int RegionsDetected { get; internal set; }
        public int RegionsRemoved { get; internal set; }
        public int CellsFilled { get; internal set; }
        public bool Performed => CellsFilled > 0;
        public bool Succeeded { get; internal set; }
        public string Reason { get; internal set; }
        public bool? PositionsInSameRegion { get; internal set; }
        public bool? InitiallyReachable { get; internal set; }
        public bool? FinallyReachable { get; internal set; }
        public bool ReachabilityChecked => FinallyReachable.HasValue;
        public bool? ReachabilityPreserved => ReachabilityChecked
            ? InitiallyReachable == FinallyReachable : (bool?)null;
    }

    public static class RoomRegionCleanup
    {
        public static RoomRegionCleanupResult Clean(RoomData room, Vector2Int? start = null,
            Vector2Int? target = null, GravityDirection gravity = GravityDirection.Down)
        {
            var regions = ConnectedRoomRegions.Detect(room);
            var result = new RoomRegionCleanupResult { RegionsDetected = regions.Count };
            RoomRegionCleanupResult Reject(string reason)
            {
                result.Reason = reason;
                return result;
            }

            IReadOnlyList<Vector2Int> main = null;
            if (start.HasValue || target.HasValue)
            {
                if (!start.HasValue || !target.HasValue)
                    return Reject("Provide both protected positions.");
                if (!room.ContainsLocal(start.Value) || !room.ContainsLocal(target.Value))
                    return Reject("Protected positions must be inside the room.");
                main = regions.FirstOrDefault(region => region.Contains(start.Value));
                var goal = regions.FirstOrDefault(region => region.Contains(target.Value));
                if (main == null || goal == null)
                    return Reject("Protected positions must be Empty.");
                result.PositionsInSameRegion = main == goal;
                if (main != goal)
                    return Reject("Protected positions occupy different regions. Both regions were kept.");
            }
            else
            {
                // Strict comparison keeps the first discovered region on ties.
                foreach (var region in regions)
                    if (main == null || region.Count > main.Count) main = region;
            }

            if (main == null) return Reject("Room has no Empty region.");
            var removed = new List<Vector2Int>();
            foreach (var region in regions)
            {
                if (region == main) continue;
                foreach (var cell in region)
                {
                    if (cell.x == 0 || cell.y == 0 || cell.x == room.Width - 1 || cell.y == room.Height - 1)
                        return Reject("Removing this region would change the room perimeter.");
                    removed.Add(cell);
                }
            }

            if (start.HasValue)
            {
                bool Reachable(RoomData grid) =>
                    GravityReachabilitySearch.Search(grid, start.Value, gravity, target.Value).Reachable;
                result.InitiallyReachable = Reachable(room);
                result.FinallyReachable = result.InitiallyReachable;
                if (removed.Count > 0)
                {
                    var candidate = room.Copy();
                    foreach (var cell in removed) candidate.SetCell(cell, CellType.Solid);
                    result.FinallyReachable = Reachable(candidate);
                    if (result.InitiallyReachable == true && result.FinallyReachable == false)
                        return Reject("Proposed cleanup would break gravity reachability.");
                }
            }

            foreach (var cell in removed) room.SetCell(cell, CellType.Solid);
            result.CellsFilled = removed.Count;
            result.RegionsRemoved = regions.Count - 1;
            result.Succeeded = true;
            result.Reason = result.Performed ? "Disconnected pockets filled." : "Already geometrically connected.";
            return result;
        }
    }
}
