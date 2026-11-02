using System;
using UnityEngine;

namespace GravityRoguelike.World.Development
{
    /// <summary>Explicit development pipeline; settings live on WorldBoundsPreview.</summary>
    public class RoomWorldPreview : MonoBehaviour
    {
        [SerializeField] private WorldBoundsPreview boundsPreview;
        [SerializeField] private RoomTilemapRenderer roomRenderer;

        /// <summary>
        /// Current data, retained in memory until regeneration or domain reload.
        /// Either CA action evolves it; Generate and Render World resets it to seeded noise.
        /// </summary>
        public WorldData GeneratedWorld { get; private set; }

        private GravityEndpointPlacementResult automaticPair;
        private RoomData placementRoom;
        private long placementRevision;
        private (int min, int max, int distance, int budget) placementSettings;
        private (int min, int max, int distance, int budget) CurrentPlacementSettings =>
            (boundsPreview.MinRequiredGravityRotations, boundsPreview.MaxRequiredGravityRotations,
                boundsPreview.MinStartGoalDistance, boundsPreview.MaxPlacementEvaluations);

        [ContextMenu("Find Valid Start and Goal")]
        public void FindValidStartAndGoal()
        {
            if (GeneratedWorld == null || boundsPreview == null)
            {
                Debug.LogWarning("Assign bounds preview settings and generate a world first using Generate and Render World.", this);
                return;
            }
            foreach (var room in GeneratedWorld.Rooms)
            {
                if (room.Id != boundsPreview.ReachabilityRoomId) continue;
                var settings = CurrentPlacementSettings;
                automaticPair = GravityEndpointPlacement.Find(room, boundsPreview.ReachabilityGravity,
                    settings.min, settings.max, settings.distance, settings.budget);
                if (!automaticPair.Succeeded)
                {
                    Debug.LogWarning($"Room {room.Id}: placement failed after {automaticPair.EvaluatedPairs} evaluations. {automaticPair.FailureReason}", this);
                    return;
                }
                placementRoom = room;
                placementRevision = room.CellRevision;
                placementSettings = settings;
                Debug.Log($"Room {room.Id}: start {automaticPair.Start}, goal {automaticPair.Goal}, gravity {automaticPair.InitialGravity}, "
                    + $"distance {automaticPair.Distance}, minimum rotations {automaticPair.MinimumRotations}, evaluated {automaticPair.EvaluatedPairs} pairs.", this);
                return;
            }
            Debug.LogWarning($"Placement: room ID {boundsPreview.ReachabilityRoomId} is not in the current world.", this);
        }

        [ContextMenu("Use Manual Start and Goal")]
        public void UseManualStartAndGoal()
        {
            automaticPair = null;
            placementRoom = null;
        }

        private (Vector2Int start, Vector2Int goal) GetEndpoints(RoomData room)
        {
            if (automaticPair == null) return (boundsPreview.ReachabilityStart, boundsPreview.ReachabilityTarget);
            if (!automaticPair.Succeeded || placementRoom != room || placementRevision != room.CellRevision
                || placementSettings != CurrentPlacementSettings || automaticPair.InitialGravity != boundsPreview.ReachabilityGravity)
                throw new InvalidOperationException("Run Find Valid Start and Goal again or choose Use Manual Start and Goal.");
            return (automaticPair.Start.Value, automaticPair.Goal.Value);
        }

        [ContextMenu("Check Gravity Reachability")]
        public void CheckGravityReachability()
        {
            AnalyseSelectedRoom(false);
        }

        [ContextMenu("Evaluate Gravity Requirements")]
        public void EvaluateGravityRequirements()
        {
            AnalyseSelectedRoom(true);
        }

        private void AnalyseSelectedRoom(bool evaluateRotations)
        {
            if (GeneratedWorld == null)
            {
                Debug.LogWarning("Generate a world first using Generate and Render World before checking gravity reachability.", this);
                return;
            }
            if (boundsPreview == null)
            {
                Debug.LogError("Gravity reachability requires bounds preview settings.", this);
                return;
            }

            RoomData selectedRoom = null;
            foreach (RoomData room in GeneratedWorld.Rooms)
                if (room.Id == boundsPreview.ReachabilityRoomId) selectedRoom = room;
            if (selectedRoom == null)
            {
                Debug.LogWarning($"Gravity reachability: room ID {boundsPreview.ReachabilityRoomId} is not in the current generated world.", this);
                return;
            }

            try
            {
                var endpoints = GetEndpoints(selectedRoom);
                if (evaluateRotations)
                {
                    int minimum = boundsPreview.MinRequiredGravityRotations;
                    int maximum = boundsPreview.MaxRequiredGravityRotations;
                    var evaluation = GravityReachabilitySearch.Evaluate(selectedRoom, endpoints.start,
                        boundsPreview.ReachabilityGravity, endpoints.goal, minimum, maximum);
                    string rotations = evaluation.MinimumRequiredGravityRotations?.ToString() ?? "N/A";
                    Debug.Log($"Gravity Rotation Evaluation | Room: {selectedRoom.Id} | Start: {endpoints.start}"
                        + $" | Goal: {endpoints.goal} | Initial Gravity: {boundsPreview.ReachabilityGravity}"
                        + $" | Reachable: {evaluation.Reachable} | Minimum required rotations: {rotations}"
                        + $" | Required range: {minimum}–{maximum} | Meets requirements: {evaluation.MeetsRequirements}"
                        + $" | Explored states: {evaluation.ExploredStates}", this);
                    return;
                }
                GravityReachabilityResult result = GravityReachabilitySearch.Search(selectedRoom,
                    endpoints.start, boundsPreview.ReachabilityGravity, endpoints.goal);
                Debug.Log($"Gravity Reachability Check | Room: {selectedRoom.Id} | Start: {endpoints.start}"
                    + $" | Goal: {endpoints.goal} | Initial Gravity: {boundsPreview.ReachabilityGravity}"
                    + $" | Reachable: {result.Reachable} | Explored States: {result.ExploredStates}", this);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                Debug.LogWarning($"Gravity reachability check failed: {exception.Message}", this);
            }
        }

        [ContextMenu("Clean Selected Room Regions")]
        public void CleanSelectedRoomRegions()
        {
            if (GeneratedWorld == null)
            {
                Debug.LogWarning("Generate a world first using Generate and Render World before cleaning regions.", this);
                return;
            }
            if (boundsPreview == null || roomRenderer == null)
            {
                Debug.LogWarning("Region cleanup requires bounds preview and renderer references.", this);
                return;
            }

            try
            {
                roomRenderer.ValidateReferences();
                foreach (RoomData room in GeneratedWorld.Rooms)
                {
                    if (room.Id != boundsPreview.ReachabilityRoomId) continue;
                    var endpoints = boundsPreview.ProtectRegionEndpoints ? GetEndpoints(room) : default;
                    var result = boundsPreview.ProtectRegionEndpoints
                        ? RoomRegionCleanup.Clean(room, endpoints.start,
                            endpoints.goal, boundsPreview.ReachabilityGravity)
                        : RoomRegionCleanup.Clean(room);
                    if (result.Performed)
                    {
                        roomRenderer.RenderWorld(GeneratedWorld);
#if UNITY_EDITOR
                        if (!Application.isPlaying && gameObject.scene.IsValid())
                            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                        UnityEditor.SceneView.RepaintAll();
#endif
                    }
                    string gravity = result.ReachabilityChecked
                        ? $"reachable before {result.InitiallyReachable}, candidate {result.FinallyReachable}"
                        : "not checked";
                    string message = $"Room {room.Id} region cleanup | Detected {result.RegionsDetected}, "
                        + $"removed {result.RegionsRemoved}, filled {result.CellsFilled} cells | Gravity: {gravity}. {result.Reason}";
                    if (result.Succeeded) Debug.Log(message, this);
                    else Debug.LogWarning(message, this);
                    return;
                }
                Debug.LogWarning($"Region cleanup: room ID {boundsPreview.ReachabilityRoomId} is not in the generated world.", this);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                Debug.LogWarning($"Region cleanup failed: {exception.Message}", this);
            }
        }

        [ContextMenu("Repair Selected Room")]
        public void RepairSelectedRoom()
        {
            if (GeneratedWorld == null)
            {
                Debug.LogWarning("Generate a world first using Generate and Render World before repairing a room.", this);
                return;
            }
            if (boundsPreview == null || roomRenderer == null)
            {
                Debug.LogWarning("Room repair requires bounds preview and room renderer references. World data was not changed.", this);
                return;
            }

            try
            {
                roomRenderer.ValidateReferences();
                RoomData selectedRoom = null;
                foreach (RoomData room in GeneratedWorld.Rooms)
                    if (room.Id == boundsPreview.ReachabilityRoomId) selectedRoom = room;
                if (selectedRoom == null)
                    throw new ArgumentException($"Room ID {boundsPreview.ReachabilityRoomId} is not in the current generated world.");

                var endpoints = GetEndpoints(selectedRoom);
                GravityRoomRepairResult result = GravityRoomRepair.Repair(selectedRoom,
                    endpoints.start, boundsPreview.ReachabilityGravity, endpoints.goal,
                    boundsPreview.MaxRepairAttempts, boundsPreview.MaxCarvedCells);
                if (result.RepairSucceeded)
                {
                    roomRenderer.RenderWorld(GeneratedWorld);
#if UNITY_EDITOR
                    if (!Application.isPlaying && gameObject.scene.IsValid())
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                    UnityEditor.SceneView.RepaintAll();
#endif
                }
                Debug.Log($"Gravity-Aware Repair | Room: {selectedRoom.Id} | Initially Reachable: {result.InitiallyReachable}"
                    + $" | Repair Attempted: {result.RepairAttempted} | Repair Successful: {result.RepairSucceeded}"
                    + $" | Attempts: {result.Attempts} | Cells Modified: {result.CellsModified}"
                    + $" | Final Reachability: {result.FinallyReachable} | Reason: {result.Reason}", this);
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"Room repair failed: {exception.Message}", this);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning($"Room repair failed: {exception.Message}", this);
            }
        }

        [ContextMenu("Apply One CA Step")]
        public void ApplyOneCAStep()
        {
            ApplyCASteps(1);
        }

        [ContextMenu("Apply Configured CA Iterations")]
        public void ApplyConfiguredCAIterations()
        {
            // The shared preflight handles missing configuration before any cells are changed.
            ApplyCASteps(boundsPreview == null ? 0 : boundsPreview.CAIterations);
        }

        private void ApplyCASteps(int iterationCount)
        {
            if (GeneratedWorld == null)
            {
                Debug.LogWarning("Generate a world first using Generate and Render World before applying a CA step.", this);
                return;
            }
            if (boundsPreview == null || roomRenderer == null)
            {
                Debug.LogError("CA step requires bounds preview and room renderer references. World data was not changed.", this);
                return;
            }

            try
            {
                // Preflight everything needed by the entire operation before modifying any room.
                int threshold = boundsPreview.SolidNeighbourThreshold;
                RoomCellularAutomata.ValidateThreshold(threshold);
                RoomCellularAutomata.ValidateIterationCount(iterationCount);
                roomRenderer.ValidateReferences();

                foreach (RoomData room in GeneratedWorld.Rooms)
                    RoomCellularAutomata.ApplyIterations(room, threshold, iterationCount);
                roomRenderer.RenderWorld(GeneratedWorld);
                Debug.Log($"Applied {iterationCount} CA step(s) to {GeneratedWorld.Rooms.Count} rooms (threshold {threshold}).", this);
#if UNITY_EDITOR
                if (!Application.isPlaying && gameObject.scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                UnityEditor.SceneView.RepaintAll();
#endif
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"CA step failed: {exception.Message}", this);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError($"CA step failed: {exception.Message}", this);
            }
        }

        [ContextMenu("Generate and Render World")]
        public void GenerateAndRenderWorld()
        {
            if (boundsPreview == null || roomRenderer == null)
            {
                Debug.LogError("RoomWorldPreview requires bounds preview and room renderer references.", this);
                return;
            }

            try
            {
                RoomPlacementResult result = boundsPreview.CreatePlacement();
                foreach (RoomData room in result.World.Rooms)
                    // Noise initialization reuses border generation before filling the interior.
                    RoomNoiseGenerator.Generate(room, boundsPreview.InitialSolidProbability);

                roomRenderer.RenderWorld(result.World);
                GeneratedWorld = result.World;
                boundsPreview.ShowPlacement(result);
#if UNITY_EDITOR
                if (!Application.isPlaying && gameObject.scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"World generation failed: {exception.Message}", this);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError($"World rendering failed: {exception.Message}", this);
            }
        }
    }
}
