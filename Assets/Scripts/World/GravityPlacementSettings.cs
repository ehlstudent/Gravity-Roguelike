using UnityEngine;

namespace GravityRoguelike.World
{
    public abstract class GravityPlacementSettings : MonoBehaviour
    {
        [Header("Gravity rotation requirements")]
        [Tooltip("The cheapest route must require at least this many gravity rotations.")]
        [Min(0), SerializeField] private int minRequiredGravityRotations = 2;
        [Tooltip("The cheapest route may require at most this many gravity rotations.")]
        [Min(0), SerializeField] private int maxRequiredGravityRotations = 8;

        public int MinRequiredGravityRotations => minRequiredGravityRotations;
        public int MaxRequiredGravityRotations => maxRequiredGravityRotations;

        [Header("Gravity-aware start and goal placement")]
        [Tooltip("Minimum Manhattan distance between the local start and goal cells.")]
        [Min(0), SerializeField] private int minStartGoalDistance = 8;
        [Tooltip("Maximum number of candidate pairs checked by gravity search per placement action.")]
        [Min(0), SerializeField] private int maxPlacementEvaluations = 200;

        public int MinStartGoalDistance => minStartGoalDistance;
        public int MaxPlacementEvaluations => maxPlacementEvaluations;
    }
}
