using System;
using System.Collections.Generic;
using GravityRoguelike.World;
using UnityEngine;

namespace GravityRoguelike.Items
{
    public readonly struct PlacedItem
    {
        public Vector2Int Cell { get; }
        public ItemType Type { get; }
        /// <summary>Fewest gravity rotations needed to reach the cell from the room's start.</summary>
        public int Rotations { get; }

        public PlacedItem(Vector2Int cell, ItemType type, int rotations)
        {
            Cell = cell;
            Type = type;
            Rotations = rotations;
        }
    }

    /// <summary>
    /// Places items only on cells where the player can come to rest, using the same
    /// fall-and-rotate rules as GravityReachabilitySearch. Rare items prefer cells that
    /// need more gravity rotations, so exploring is rewarded.
    /// </summary>
    public static class ItemPlacement
    {
        /// <summary>Every resting cell reachable from start, with its minimum rotation count.</summary>
        public static Dictionary<Vector2Int, int> FindRestingCells(RoomData room, Vector2Int start,
            GravityDirection gravity)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (!room.ContainsLocal(start) || room.GetCell(start) != CellType.Empty)
                throw new ArgumentException("Start must be an Empty cell inside the room.", nameof(start));

            return GravityReachabilitySearch.Explore(room, start, gravity).RestingCosts;
        }

        /// <param name="excluded">Cells to keep clear, e.g. start and goal.</param>
        /// <param name="excludeRadius">Manhattan radius kept free around excluded cells.</param>
        /// <param name="minSpacing">Minimum Manhattan distance between two items.</param>
        public static List<PlacedItem> Place(RoomData room, Vector2Int start, GravityDirection gravity,
            IEnumerable<Vector2Int> excluded, int count, int excludeRadius, int minSpacing, System.Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (count < 0 || excludeRadius < 0 || minSpacing < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Counts and distances must be non-negative.");

            return Place(FindRestingCells(room, start, gravity), excluded, count, excludeRadius, minSpacing, rng);
        }

        internal static List<PlacedItem> Place(IReadOnlyDictionary<Vector2Int, int> restingCells,
            IEnumerable<Vector2Int> excluded, int count, int excludeRadius, int minSpacing, System.Random rng,
            Queue<ItemType> showcaseItems = null)
        {
            var blocked = new List<Vector2Int>(excluded ?? Array.Empty<Vector2Int>());
            var candidates = new List<KeyValuePair<Vector2Int, int>>();
            foreach (var entry in restingCells)
            {
                bool free = true;
                foreach (Vector2Int cell in blocked)
                    if (Manhattan(cell, entry.Key) <= excludeRadius) { free = false; break; }
                if (free) candidates.Add(entry);
            }

            // Fixed order before shuffling keeps results identical for the same seed.
            candidates.Sort((left, right) => left.Key.y != right.Key.y
                ? left.Key.y.CompareTo(right.Key.y) : left.Key.x.CompareTo(right.Key.x));
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            int maxRotations = 0;
            foreach (var entry in candidates) maxRotations = Math.Max(maxRotations, entry.Value);
            int rareThreshold = (maxRotations + 1) / 2;

            var placed = new List<PlacedItem>();
            for (int i = 0; i < count; i++)
            {
                bool showcasing = showcaseItems != null && showcaseItems.Count > 0;
                ItemType type = showcasing ? showcaseItems.Peek() : ItemLootTable.Roll(rng);
                bool rare = ItemLootTable.GetRarity(type) == ItemRarity.Rare;
                if (!TryPick(candidates, placed, minSpacing, rare ? rareThreshold : 0, out var pick) &&
                    !(rare && TryPick(candidates, placed, minSpacing, 0, out pick)))
                    break; // Room is full.

                placed.Add(new PlacedItem(pick.Key, type, pick.Value));
                if (showcasing) showcaseItems.Dequeue();
            }
            return placed;
        }

        private static bool TryPick(List<KeyValuePair<Vector2Int, int>> candidates, List<PlacedItem> placed,
            int minSpacing, int minRotations, out KeyValuePair<Vector2Int, int> pick)
        {
            foreach (var candidate in candidates)
            {
                if (candidate.Value < minRotations) continue;
                bool spaced = true;
                foreach (PlacedItem item in placed)
                    if (Manhattan(item.Cell, candidate.Key) < Math.Max(1, minSpacing)) { spaced = false; break; }
                if (!spaced) continue;
                pick = candidate;
                return true;
            }
            pick = default;
            return false;
        }

        private static int Manhattan(Vector2Int a, Vector2Int b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
    }
}
