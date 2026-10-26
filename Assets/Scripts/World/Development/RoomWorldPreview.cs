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
        /// Apply One CA Step evolves it; Generate and Render World resets it to seeded noise.
        /// </summary>
        public WorldData GeneratedWorld { get; private set; }

        [ContextMenu("Apply One CA Step")]
        public void ApplyOneCAStep()
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
                roomRenderer.ValidateReferences();

                foreach (RoomData room in GeneratedWorld.Rooms)
                    RoomCellularAutomata.ApplyStep(room, threshold);
                roomRenderer.RenderWorld(GeneratedWorld);
                Debug.Log($"Applied one CA step to {GeneratedWorld.Rooms.Count} rooms (threshold {threshold}).", this);
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
