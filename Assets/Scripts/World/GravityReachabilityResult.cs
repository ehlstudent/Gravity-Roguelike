using System.Collections.Generic;

namespace GravityRoguelike.World
{
    public sealed class GravityReachabilityResult
    {
        public bool Reachable { get; }
        public int ExploredStates { get; }
        public IReadOnlyList<PlayerState> Path { get; }

        internal GravityReachabilityResult(bool reachable, int exploredStates, List<PlayerState> path)
        {
            Reachable = reachable;
            ExploredStates = exploredStates;
            Path = path.AsReadOnly();
        }
    }
}
