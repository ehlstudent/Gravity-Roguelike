using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GravityRoguelike.World
{
    /// <summary>Renders cell data only. Assign a dedicated generated-world Tilemap.</summary>
    public class RoomTilemapRenderer : MonoBehaviour
    {
        [Tooltip("Dedicated generated output only: rendering and Clear remove every tile on this Tilemap.")]
        [SerializeField] private Tilemap targetTilemap;
        [SerializeField] private TileBase solidTile;

        /// <summary>
        /// Replaces the complete output without modifying the supplied data.
        /// Throws before clearing if the world or required references are missing.
        /// Grid/Tilemap transforms should be identity for one unit per world-grid cell.
        /// </summary>
        public void RenderWorld(WorldData world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            ValidateReferences();

            targetTilemap.ClearAllTiles();
            foreach (RoomData room in world.Rooms)
            {
                for (int y = 0; y < room.Height; y++)
                {
                    for (int x = 0; x < room.Width; x++)
                    {
                        var localCell = new Vector2Int(x, y);
                        if (room.GetCell(localCell) != CellType.Solid) continue;
                        Vector2Int worldCell = room.LocalToWorld(localCell);
                        targetTilemap.SetTile(new Vector3Int(worldCell.x, worldCell.y, 0), solidTile);
                    }
                }
            }
        }

        /// <summary>Clears dedicated output after checking both configured references.</summary>
        [ContextMenu("Clear Generated Tiles")]
        public void Clear()
        {
            ValidateReferences();
            targetTilemap.ClearAllTiles();
        }

        /// <summary>Checks required references without changing output, for preflight before data edits.</summary>
        public void ValidateReferences()
        {
            if (targetTilemap == null)
                throw new InvalidOperationException("RoomTilemapRenderer requires a dedicated target Tilemap. Existing output was not cleared.");
            if (solidTile == null)
                throw new InvalidOperationException("RoomTilemapRenderer requires a solid TileBase asset. Existing output was not cleared.");
        }
    }
}
