using System.Collections.Generic;

namespace GravityRoguelike.World
{
    public sealed class GravityRotationEvaluation
    {
        public bool Reachable => MinimumRequiredGravityRotations.HasValue;
        public int? MinimumRequiredGravityRotations { get; }
        public bool MeetsMinimum { get; }
        public bool MeetsMaximum { get; }
        public bool MeetsRequirements => MeetsMinimum && MeetsMaximum;
        public int ExploredStates { get; }
        public IReadOnlyList<PlayerState> Path { get; }

        internal GravityRotationEvaluation(GravityReachabilityResult route, int minimum, int maximum)
        {
            Path = route.Path;
            ExploredStates = route.ExploredStates;
            if (!route.Reachable) return;

            int rotations = 0;
            for (int i = 1; i < Path.Count; i++)
                if (Path[i].Gravity != Path[i - 1].Gravity) rotations++;
            MinimumRequiredGravityRotations = rotations;
            MeetsMinimum = rotations >= minimum;
            MeetsMaximum = rotations <= maximum;
        }
    }
}
