using System;
using GravityRoguelike.World;
using NUnit.Framework;
using UnityEngine;

namespace GravityRoguelike.Tests
{
    public class GravityMovementSimulatorTests
    {
        [TestCase(GravityDirection.Down, 0, -1)]
        [TestCase(GravityDirection.Up, 0, 1)]
        [TestCase(GravityDirection.Left, -1, 0)]
        [TestCase(GravityDirection.Right, 1, 0)]
        public void FallsUntilFirstObstacleWithoutChangingInputs(GravityDirection gravity, int dx, int dy)
        {
            var room = new RoomData(1, new RectInt(100, 200, 9, 9), 42);
            var start = new Vector2Int(4, 4);
            var step = new Vector2Int(dx, dy);
            room.SetCell(start + step * 3, CellType.Solid);
            var before = Snapshot(room);
            var initial = new PlayerState(start, gravity);
            var expected = new PlayerState(start + step * 2, gravity);
            Assert.That(GravityMovementSimulator.SimulateGravity(room, initial), Is.EqualTo(expected));
            Assert.That(GravityMovementSimulator.SimulateGravity(room, initial), Is.EqualTo(expected));
            Assert.That(GravityMovementSimulator.SimulateGravity(room, expected), Is.EqualTo(expected));
            Assert.That(initial, Is.EqualTo(new PlayerState(start, gravity)));
            Assert.That(Snapshot(room), Is.EqualTo(before));
            room.SetCell(start + step, CellType.Solid);
            Assert.That(GravityMovementSimulator.SimulateGravity(room, initial), Is.EqualTo(initial));
        }

        [TestCase(1, 1)]
        [TestCase(1, 7)]
        [TestCase(8, 1)]
        [TestCase(9, 7)]
        public void OpenRoomsStopAtBoundaries(int width, int height)
        {
            var room = new RoomData(1, new RectInt(0, 0, width, height), 42);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                AssertLanding(room, x, y, GravityDirection.Down, x, 0);
                AssertLanding(room, x, y, GravityDirection.Up, x, height - 1);
                AssertLanding(room, x, y, GravityDirection.Left, 0, y);
                AssertLanding(room, x, y, GravityDirection.Right, width - 1, y);
            }
        }

        [Test]
        public void RotationUsesTheNewDirectionFromTheSettledPosition()
        {
            var room = new RoomData(1, new RectInt(0, 0, 7, 7), 42);
            RoomBorderGenerator.Generate(room);
            var down = GravityMovementSimulator.SimulateGravity(room,
                new PlayerState(new Vector2Int(3, 4), GravityDirection.Down));
            var left = GravityMovementSimulator.SimulateGravity(room,
                new PlayerState(down.Position, GravityDirection.Left));
            Assert.That(down.Position, Is.EqualTo(new Vector2Int(3, 1)));
            Assert.That(left, Is.EqualTo(new PlayerState(new Vector2Int(1, 1), GravityDirection.Left)));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(3, 0)]
        [TestCase(0, 3)]
        [TestCase(int.MinValue, int.MaxValue)]
        public void RejectsOutsideStartingPositions(int x, int y)
        {
            var room = new RoomData(1, new RectInt(0, 0, 3, 3), 42);
            var state = new PlayerState(new Vector2Int(x, y), GravityDirection.Down);
            var before = Snapshot(room);
            Assert.Throws<ArgumentOutOfRangeException>(() => GravityMovementSimulator.SimulateGravity(room, state));
            Assert.That(Snapshot(room), Is.EqualTo(before));
        }

        [Test]
        public void RejectsNullRoomsSolidStartsAndInvalidGravity()
        {
            var state = new PlayerState(Vector2Int.zero, GravityDirection.Down);
            Assert.Throws<ArgumentNullException>(() => GravityMovementSimulator.SimulateGravity(null, state));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerState(Vector2Int.zero, (GravityDirection)99));
            var room = new RoomData(1, new RectInt(0, 0, 3, 3), 42);
            for (int y = 0; y < room.Height; y++)
            for (int x = 0; x < room.Width; x++) room.SetCell(new Vector2Int(x, y), CellType.Solid);
            var before = Snapshot(room);
            Assert.Throws<ArgumentException>(() => GravityMovementSimulator.SimulateGravity(room, state));
            Assert.That(Snapshot(room), Is.EqualTo(before));
        }

        [Test]
        public void IdenticalLocalGridsIgnoreWorldPlacement()
        {
            var first = new RoomData(1, new RectInt(100, 200, 9, 7), 42);
            var second = new RoomData(2, new RectInt(800, 400, 9, 7), 99);
            RoomBorderGenerator.Generate(first);
            RoomBorderGenerator.Generate(second);
            first.SetCell(new Vector2Int(4, 2), CellType.Solid);
            second.SetCell(new Vector2Int(4, 2), CellType.Solid);
            var before = Snapshot(first);
            foreach (GravityDirection gravity in Enum.GetValues(typeof(GravityDirection)))
            {
                var state = new PlayerState(new Vector2Int(4, 3), gravity);
                Assert.That(GravityMovementSimulator.SimulateGravity(first, state),
                    Is.EqualTo(GravityMovementSimulator.SimulateGravity(second, state)));
            }
            Assert.That(Snapshot(first), Is.EqualTo(before));
            Assert.That(Snapshot(second), Is.EqualTo(before));
            Assert.That(first.Bounds, Is.EqualTo(new RectInt(100, 200, 9, 7)));
            Assert.That(first.Id, Is.EqualTo(1));
            Assert.That(first.Seed, Is.EqualTo(42));
        }

        private static void AssertLanding(RoomData room, int x, int y, GravityDirection gravity, int endX, int endY)
        {
            var result = GravityMovementSimulator.SimulateGravity(room, new PlayerState(new Vector2Int(x, y), gravity));
            Assert.That(result, Is.EqualTo(new PlayerState(new Vector2Int(endX, endY), gravity)));
        }

        private static CellType[] Snapshot(RoomData room)
        {
            var cells = new CellType[room.Width * room.Height];
            for (int y = 0; y < room.Height; y++)
            for (int x = 0; x < room.Width; x++) cells[y * room.Width + x] = room.GetCell(new Vector2Int(x, y));
            return cells;
        }
    }
}
