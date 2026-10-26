using System;
using UnityEngine;

namespace GravityRoguelike.World.Development
{
    /// <summary>Explicit development pipeline; placement settings live only on WorldBoundsPreview.</summary>
    public class RoomWorldPreview : MonoBehaviour
    {
        [SerializeField] private WorldBoundsPreview boundsPreview;
        [SerializeField] private RoomTilemapRenderer roomRenderer;

        /// <summary>
        /// Last successfully rendered data, retained in memory until regeneration or domain reload.
        /// Inspector edits do not change this snapshot; invoke Generate and Render World again.
        /// </summary>
        public WorldData GeneratedWorld { get; private set; }

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
                    RoomBorderGenerator.Generate(room);

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
