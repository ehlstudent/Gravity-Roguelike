using System;
using UnityEngine;

namespace GravityRoguelike.World
{
    /// <summary>
    /// A room's cells use local [x, y] coordinates starting at (0, 0).
    /// Bounds.position is its integer world-grid origin, not a Transform position.
    /// Bounds.xMax and Bounds.yMax are exclusive.
    /// </summary>
    public class RoomData
    {
        private readonly CellType[,] cells;

        public int Id { get; }
        public RectInt Bounds { get; }
        public int Seed { get; }
        public int Width => Bounds.width;
        public int Height => Bounds.height;

        /// <summary>Creates an empty grid. The caller supplies the room's unique ID.</summary>
        public RoomData(int id, RectInt bounds, int seed)
        {
            if (bounds.width <= 0 || bounds.height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bounds), bounds,
                    "Room width and height must be positive.");
            }

            Id = id;
            Bounds = bounds;
            Seed = seed;
            // Arrays initialize to zero, which is CellType.Empty.
            cells = new CellType[Width, Height];
        }

        public bool ContainsLocal(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
        }

        /// <summary>Reads a local cell; throws if it is outside the grid.</summary>
        public CellType GetCell(Vector2Int localCell)
        {
            ValidateLocalCell(localCell);
            return cells[localCell.x, localCell.y];
        }

        /// <summary>Writes a local cell; throws if it is outside the grid.</summary>
        public void SetCell(Vector2Int localCell, CellType value)
        {
            ValidateLocalCell(localCell);
            cells[localCell.x, localCell.y] = value;
        }

        /// <summary>Translates to world-grid coordinates without clamping or bounds checks.</summary>
        public Vector2Int LocalToWorld(Vector2Int localCell)
        {
            return Bounds.position + localCell;
        }

        /// <summary>Translates to local coordinates without clamping or bounds checks.</summary>
        public Vector2Int WorldToLocal(Vector2Int worldCell)
        {
            return worldCell - Bounds.position;
        }

        private void ValidateLocalCell(Vector2Int localCell)
        {
            if (!ContainsLocal(localCell))
            {
                throw new ArgumentOutOfRangeException(nameof(localCell), localCell,
                    $"Local cell must satisfy 0 <= x < {Width} and 0 <= y < {Height}.");
            }
        }
    }
}
