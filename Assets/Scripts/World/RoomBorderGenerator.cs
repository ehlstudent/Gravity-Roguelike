using System;
using UnityEngine;

namespace GravityRoguelike.World
{
    public static class RoomBorderGenerator
    {
        /// <summary>
        /// Replaces cell contents with a one-cell Solid perimeter and Empty interior.
        /// Rooms narrower or shorter than three cells become entirely Solid.
        /// Coordinates are local; bounds, ID and seed are unchanged.
        /// </summary>
        public static void Generate(RoomData room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));

            for (int y = 0; y < room.Height; y++)
            {
                for (int x = 0; x < room.Width; x++)
                {
                    bool border = x == 0 || y == 0 || x == room.Width - 1 || y == room.Height - 1;
                    room.SetCell(new Vector2Int(x, y), border ? CellType.Solid : CellType.Empty);
                }
            }
        }
    }
}
