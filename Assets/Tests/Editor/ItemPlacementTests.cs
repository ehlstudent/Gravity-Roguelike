using System.Collections.Generic;
using GravityRoguelike.Items;
using GravityRoguelike.World;
using NUnit.Framework;
using UnityEngine;

namespace GravityRoguelike.Tests
{
    public class ItemPlacementTests
    {
        private static readonly Vector2Int Start = new Vector2Int(2, 1);

        [Test]
        public void SameSeedGivesSameItems()
        {
            var room = Room();
            var first = ItemPlacement.Place(room, Start, GravityDirection.Down, null, 5, 0, 2, new System.Random(7));
            var second = ItemPlacement.Place(room, Start, GravityDirection.Down, null, 5, 0, 2, new System.Random(7));
            Assert.That(first.Count, Is.GreaterThan(0));
            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].Cell, Is.EqualTo(first[i].Cell));
                Assert.That(second[i].Type, Is.EqualTo(first[i].Type));
            }
        }

        [Test]
        public void ItemsLieOnReachableRestingCells()
        {
            var room = Room();
            var resting = ItemPlacement.FindRestingCells(room, Start, GravityDirection.Down);
            for (int seed = 0; seed < 50; seed++)
            {
                foreach (PlacedItem item in ItemPlacement.Place(room, Start, GravityDirection.Down, null, 4, 0, 2,
                             new System.Random(seed)))
                {
                    Assert.That(room.GetCell(item.Cell), Is.EqualTo(CellType.Empty));
                    Assert.That(resting.ContainsKey(item.Cell), Is.True, $"Seed {seed}: {item.Cell} unreachable");
                    Assert.That(item.Rotations, Is.EqualTo(resting[item.Cell]));
                }
            }
        }

        [Test]
        public void SealedPocketIsNeverUsed()
        {
            var room = Room();
            var pocket = new Vector2Int(9, 6); // Enclosed by Solid cells in Room().
            for (int seed = 0; seed < 50; seed++)
                foreach (PlacedItem item in ItemPlacement.Place(room, Start, GravityDirection.Down, null, 6, 0, 1,
                             new System.Random(seed)))
                    Assert.That(item.Cell, Is.Not.EqualTo(pocket));
        }

        [Test]
        public void KeepsDistanceToEndpointsAndOtherItems()
        {
            var room = Room();
            var goal = new Vector2Int(10, 8);
            for (int seed = 0; seed < 50; seed++)
            {
                var items = ItemPlacement.Place(room, Start, GravityDirection.Down,
                    new[] { Start, goal }, 4, 2, 3, new System.Random(seed));
                for (int i = 0; i < items.Count; i++)
                {
                    Assert.That(Manhattan(items[i].Cell, Start), Is.GreaterThan(2));
                    Assert.That(Manhattan(items[i].Cell, goal), Is.GreaterThan(2));
                    for (int j = i + 1; j < items.Count; j++)
                        Assert.That(Manhattan(items[i].Cell, items[j].Cell), Is.GreaterThanOrEqualTo(3));
                }
            }
        }

        [Test]
        public void LootTableFollowsRarityWeights()
        {
            var counts = new Dictionary<ItemType, int>();
            var rng = new System.Random(1);
            for (int i = 0; i < 12000; i++)
            {
                ItemType type = ItemLootTable.Roll(rng);
                counts[type] = counts.TryGetValue(type, out int c) ? c + 1 : 1;
            }
            // Weights 60 / 25 / 25 / 10 of 120.
            Assert.That(counts[ItemType.Heart], Is.InRange(5600, 6400));
            Assert.That(counts[ItemType.DamageShard], Is.InRange(2200, 2800));
            Assert.That(counts[ItemType.SpeedBoots], Is.InRange(2200, 2800));
            Assert.That(counts[ItemType.MaxHealthCrystal], Is.InRange(800, 1200));
        }

        /// <summary>12x10 room: solid border, a ledge, and a sealed 1-cell pocket at (9, 6).</summary>
        private static RoomData Room()
        {
            var room = new RoomData(1, new RectInt(0, 0, 12, 10), 1);
            for (int x = 0; x < 12; x++)
            {
                room.SetCell(new Vector2Int(x, 0), CellType.Solid);
                room.SetCell(new Vector2Int(x, 9), CellType.Solid);
            }
            for (int y = 0; y < 10; y++)
            {
                room.SetCell(new Vector2Int(0, y), CellType.Solid);
                room.SetCell(new Vector2Int(11, y), CellType.Solid);
            }
            for (int x = 4; x <= 7; x++) room.SetCell(new Vector2Int(x, 4), CellType.Solid);
            foreach (var cell in new[] { new Vector2Int(8, 6), new Vector2Int(10, 6), new Vector2Int(9, 5), new Vector2Int(9, 7) })
                room.SetCell(cell, CellType.Solid);
            return room;
        }

        private static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
