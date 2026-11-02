using System;
using GravityRoguelike.World;
using NUnit.Framework;
using UnityEngine;

namespace GravityRoguelike.Tests
{
    public class GravityRoomRepairTests
    {
        private static readonly Vector2Int Start = new Vector2Int(1, 1);

        [Test]
        public void AlreadyReachableDoesNotAttemptRepair()
        {
            var room = Corridor(0);
            string before = Snapshot(room);
            var result = Repair(room, 50, 8);
            Assert.That(result.InitiallyReachable, Is.True);
            Assert.That(result.RepairAttempted, Is.False);
            Assert.That(result.RepairSucceeded, Is.False);
            Assert.That(result.FinallyReachable, Is.True);
            Assert.That(result.CellsModified, Is.Zero);
            Assert.That(Snapshot(room), Is.EqualTo(before));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void RepairsControlledCorridorsAndIsIdempotent(int walls)
        {
            var room = Corridor(walls);
            var result = Repair(room, 50, walls);
            Assert.That(result.InitiallyReachable, Is.False);
            Assert.That(result.RepairSucceeded, Is.True);
            Assert.That(result.CellsModified, Is.EqualTo(walls));
            Assert.That(result.Attempts, Is.EqualTo(walls));
            Assert.That(result.FinallyReachable, Is.True);
            Assert.That(GravityReachabilitySearch.Search(room, Start, GravityDirection.Down, Target(room)).Reachable, Is.True);
            foreach (Vector2Int cell in result.ModifiedCells)
            {
                Assert.That(cell.y, Is.EqualTo(1));
                Assert.That(cell.x, Is.InRange(2, 1 + walls));
                Assert.That(room.GetCell(cell), Is.EqualTo(CellType.Empty));
            }
            AssertBorders(room);
            string repaired = Snapshot(room);
            var repeat = Repair(room, 50, walls);
            Assert.That(repeat.InitiallyReachable, Is.True);
            Assert.That(repeat.Attempts, Is.Zero);
            Assert.That(repeat.CellsModified, Is.Zero);
            Assert.That(Snapshot(room), Is.EqualTo(repaired));
        }

        [TestCase(0, 8, 0)]
        [TestCase(50, 0, 0)]
        [TestCase(1, 8, 1)]
        [TestCase(50, 1, 1)]
        public void FailedBudgetsPreserveEveryCell(int attempts, int cells, int expectedAttempts)
        {
            var room = Corridor(2);
            string before = Snapshot(room);
            var result = Repair(room, attempts, cells);
            Assert.That(result.RepairSucceeded, Is.False);
            Assert.That(result.FinallyReachable, Is.False);
            Assert.That(result.Attempts, Is.EqualTo(expectedAttempts));
            Assert.That(result.RepairAttempted, Is.EqualTo(expectedAttempts > 0));
            Assert.That(result.CellsModified, Is.Zero);
            Assert.That(result.ModifiedCells, Is.Empty);
            Assert.That(Snapshot(room), Is.EqualTo(before));
            AssertBorders(room);
        }

        [Test]
        public void GeometricOpeningWithoutGravityRouteIsNotAccepted()
        {
            var room = new RoomData(7, new RectInt(0, 0, 7, 5), 42);
            RoomBorderGenerator.Generate(room);
            for (int y = 1; y < 4; y++) room.SetCell(new Vector2Int(3, y), CellType.Solid);
            var target = new Vector2Int(4, 2);
            string before = Snapshot(room);
            var opened = new RoomData(7, room.Bounds, 42);
            for (int y = 0; y < room.Height; y++)
            for (int x = 0; x < room.Width; x++) opened.SetCell(new Vector2Int(x, y), room.GetCell(new Vector2Int(x, y)));
            opened.SetCell(new Vector2Int(3, 1), CellType.Empty);
            Assert.That(GravityReachabilitySearch.Search(opened, Start, GravityDirection.Down, target).Reachable, Is.False);
            var result = GravityRoomRepair.Repair(room, Start, GravityDirection.Down, target, 50, 1);
            Assert.That(result.RepairAttempted, Is.True);
            Assert.That(result.RepairSucceeded, Is.False);
            Assert.That(Snapshot(room), Is.EqualTo(before));
            AssertBorders(room);
        }

        [Test]
        public void PlacementAndMetadataDoNotAffectDeterministicRepair()
        {
            var first = Corridor(2);
            var second = Corridor(2, new Vector2Int(100, 200));
            var a = Repair(first, 50, 8);
            var b = Repair(second, 50, 8);
            Assert.That(b.RepairSucceeded, Is.EqualTo(a.RepairSucceeded));
            Assert.That(b.Attempts, Is.EqualTo(a.Attempts));
            Assert.That(b.ModifiedCells, Is.EqualTo(a.ModifiedCells));
            Assert.That(second.Bounds, Is.EqualTo(new RectInt(100, 200, 6, 3)));
            Assert.That(second.Id, Is.EqualTo(7));
            Assert.That(second.Seed, Is.EqualTo(42));
        }

        [Test]
        public void NoCandidatesAndNoInteriorTerminateSafely()
        {
            var open = new RoomData(7, new RectInt(0, 0, 5, 5), 42);
            RoomBorderGenerator.Generate(open);
            var result = GravityRoomRepair.Repair(open, Start, GravityDirection.Down, new Vector2Int(2, 2));
            Assert.That(result.RepairSucceeded, Is.False);
            Assert.That(result.Attempts, Is.Zero);
            var tiny = new RoomData(7, new RectInt(0, 0, 2, 2), 42);
            RoomBorderGenerator.Generate(tiny);
            string before = Snapshot(tiny);
            Assert.Throws<ArgumentException>(() => GravityRoomRepair.Repair(tiny, Vector2Int.zero, GravityDirection.Down, Vector2Int.one));
            Assert.That(Snapshot(tiny), Is.EqualTo(before));
        }

        [Test]
        public void InvalidInputsNeverChangeTheGrid()
        {
            var room = Corridor(2);
            string before = Snapshot(room);
            Assert.Throws<ArgumentNullException>(() => GravityRoomRepair.Repair(null, Start, GravityDirection.Down, Start));
            Assert.Throws<ArgumentOutOfRangeException>(() => Repair(room, -1, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => Repair(room, 50, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => GravityRoomRepair.Repair(room, Start, (GravityDirection)99, Target(room)));
            Assert.Throws<ArgumentOutOfRangeException>(() => GravityRoomRepair.Repair(room, new Vector2Int(-1, 0), GravityDirection.Down, Target(room)));
            Assert.Throws<ArgumentOutOfRangeException>(() => GravityRoomRepair.Repair(room, Start, GravityDirection.Down, new Vector2Int(99, 0)));
            Assert.Throws<ArgumentException>(() => GravityRoomRepair.Repair(room, Vector2Int.zero, GravityDirection.Down, Target(room)));
            Assert.Throws<ArgumentException>(() => GravityRoomRepair.Repair(room, Start, GravityDirection.Down, Vector2Int.zero));
            Assert.That(Snapshot(room), Is.EqualTo(before));
        }

        public static RoomData Corridor(int walls, Vector2Int origin = default)
        {
            var room = new RoomData(7, new RectInt(origin.x, origin.y, walls + 4, 3), 42);
            RoomBorderGenerator.Generate(room);
            for (int x = 2; x < walls + 2; x++) room.SetCell(new Vector2Int(x, 1), CellType.Solid);
            return room;
        }

        private static Vector2Int Target(RoomData room) => new Vector2Int(room.Width - 2, 1);
        private static GravityRoomRepairResult Repair(RoomData room, int attempts, int cells)
            => GravityRoomRepair.Repair(room, Start, GravityDirection.Down, Target(room), attempts, cells);

        private static string Snapshot(RoomData room)
        {
            var text = new System.Text.StringBuilder();
            for (int y = 0; y < room.Height; y++)
            for (int x = 0; x < room.Width; x++) text.Append((int)room.GetCell(new Vector2Int(x, y)));
            return text.ToString();
        }

        private static void AssertBorders(RoomData room)
        {
            for (int y = 0; y < room.Height; y++)
            for (int x = 0; x < room.Width; x++)
                if (x == 0 || y == 0 || x == room.Width - 1 || y == room.Height - 1)
                    Assert.That(room.GetCell(new Vector2Int(x, y)), Is.EqualTo(CellType.Solid));
        }
    }
}
