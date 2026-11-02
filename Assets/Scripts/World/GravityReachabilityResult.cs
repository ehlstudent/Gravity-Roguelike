using System.Collections.Generic;

namespace GravityRoguelike.World
{
    public sealed class GravityReachabilityResult
    {
        public bool Reachable { get; }
        public int ExploredStates { get; }
        public IReadOnlyList<PlayerState> Path { get; }
        public IReadOnlyList<PlayerState> ReachedStates { get; }

        internal GravityReachabilityResult(bool reachable, int exploredStates, List<PlayerState> path,
            List<PlayerState> reachedStates)
        {
            Reachable = reachable;
            ExploredStates = exploredStates;
            Path = path.AsReadOnly();
            ReachedStates = reachedStates.AsReadOnly();
        }
    }
}
