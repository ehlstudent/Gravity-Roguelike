namespace GravityRoguelike.World
{
    public sealed class RoomPlacementResult
    {
        public WorldData World { get; }
        public int RequestedCount { get; }
        public int PlacedCount { get; }
        public int AttemptsUsed { get; }
        /// <summary>True when the attempt budget stopped placement before all rooms were placed.</summary>
        public bool FinishedEarly => PlacedCount < RequestedCount;

        internal RoomPlacementResult(WorldData world, int requestedCount, int attemptsUsed)
        {
            World = world;
            RequestedCount = requestedCount;
            PlacedCount = world.Rooms.Count;
            AttemptsUsed = attemptsUsed;
        }
    }
}
