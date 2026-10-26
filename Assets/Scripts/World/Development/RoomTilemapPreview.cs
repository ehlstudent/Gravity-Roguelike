using System;
using UnityEngine;

namespace GravityRoguelike.World.Development
{
    /// <summary>Explicit development patterns, independent of the placement/generation pipeline.</summary>
    public class RoomTilemapPreview : MonoBehaviour
    {
        [SerializeField] private RoomTilemapRenderer roomRenderer;

        [ContextMenu("Render Two-Room Test")]
        public void RenderTwoRoomTest()
        {
            Render(CreateTestWorld());
        }

        [ContextMenu("Render Smaller Test (One Tile)")]
        public void RenderSmallerTest()
        {
            var world = new WorldData(new RectInt(-8, -5, 18, 12), 42);
            var room = new RoomData(3, new RectInt(2, 1, 2, 2), 103);
            room.SetCell(Vector2Int.zero, CellType.Solid);
            world.AddRoom(room);
            Render(world);
        }

        [ContextMenu("Render Empty Test (No Tiles)")]
        public void RenderEmptyTest()
        {
            var world = new WorldData(new RectInt(-8, -5, 18, 12), 42);
            world.AddRoom(new RoomData(1, new RectInt(-6, -3, 4, 4), 101));
            Render(world);
        }

        /// <summary>Seven solid cells; all other cells stay Empty, including room edges.</summary>
        public static WorldData CreateTestWorld()
        {
            var world = new WorldData(new RectInt(-8, -5, 18, 12), 42);
            var left = new RoomData(1, new RectInt(-6, -3, 4, 4), 101);
            left.SetCell(new Vector2Int(0, 0), CellType.Solid);
            left.SetCell(new Vector2Int(1, 0), CellType.Solid);
            left.SetCell(new Vector2Int(2, 0), CellType.Solid);
            left.SetCell(new Vector2Int(3, 3), CellType.Solid);
            var right = new RoomData(2, new RectInt(2, 1, 5, 4), 102);
            right.SetCell(new Vector2Int(0, 0), CellType.Solid);
            right.SetCell(new Vector2Int(2, 1), CellType.Solid);
            right.SetCell(new Vector2Int(4, 3), CellType.Solid);
            world.AddRoom(left);
            world.AddRoom(right);
            return world;
        }

        private void Render(WorldData world)
        {
            if (roomRenderer == null)
            {
                Debug.LogError("RoomTilemapPreview requires a RoomTilemapRenderer reference.", this);
                return;
            }
            try
            {
                roomRenderer.RenderWorld(world);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                UnityEditor.SceneView.RepaintAll();
#endif
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message, this);
            }
        }
    }
}
