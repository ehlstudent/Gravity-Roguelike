using System.Collections.Generic;
using UnityEngine;

namespace GravityRoguelike.World
{
    public sealed class GravityRoomRepairResult
    {
        public bool InitiallyReachable { get; }
        public bool RepairAttempted => Attempts > 0;
        public bool RepairSucceeded { get; }
        public int Attempts { get; }
        public int CellsModified => ModifiedCells.Count;
        public bool FinallyReachable => InitiallyReachable || RepairSucceeded;
        public IReadOnlyList<Vector2Int> ModifiedCells { get; }
        public string Reason { get; }

        internal GravityRoomRepairResult(bool initiallyReachable, bool repairSucceeded,
            int attempts, List<Vector2Int> modifiedCells, string reason)
        {
            InitiallyReachable = initiallyReachable;
            RepairSucceeded = repairSucceeded;
            Attempts = attempts;
            ModifiedCells = modifiedCells.AsReadOnly();
            Reason = reason;
        }
    }
}
