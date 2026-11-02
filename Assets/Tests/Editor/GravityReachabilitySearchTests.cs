using System;
using System.Collections.Generic;
using GravityRoguelike.World;
using NUnit.Framework;
using UnityEngine;

namespace GravityRoguelike.Tests
{
    public class GravityReachabilitySearchTests
    {
        [Test]
        public void StartEqualsTargetSucceedsBeforeFalling()
        {
            var result = GravityReachabilitySearch.Search(Room(), new Vector2Int(2, 2),
                GravityDirection.Down, new Vector2Int(2, 2));
            Assert.That(result.Reachable, Is.True);
            Assert.That(result.ExploredStates, Is.EqualTo(1));
            Assert.That(result.Path.Count, Is.EqualTo(1));
        }

        [TestCase(GravityDirection.Down, 2, 0)]
        [TestCase(GravityDirection.Up, 2, 4)]
        [TestCase(GravityDirection.Left, 0, 2)]
        [TestCase(GravityDirection.Right, 4, 2)]
        public void InitialFallUsesProvidedGravity(GravityDirection gravity, int x, int y)
        {
            var room = Room();
            var result = GravityReachabilitySearch.Search(room, new Vector2Int(2, 2), gravity, new Vector2Int(x, y));
            Assert.That(result.Reachable, Is.True);
            Assert.That(result.Path.Count, Is.EqualTo(2));
            AssertValidPath(room, result);
        }

        [Test]
        public void InitialGravityChangesWhichTargetsAreReachable()
        {
            var room = Room();
            Assert.That(GravityReachabilitySearch.Search(room, new Vector2Int(2, 2), GravityDirection.Down,
                new Vector2Int(2, 0)).Reachable, Is.True);
            Assert.That(GravityReachabilitySearch.Search(room, new Vector2Int(2, 2), GravityDirection.Up,
                new Vector2Int(2, 0)).Reachable, Is.False);
        }

        [Test]
        public void FindsPathRequiringQuarterTurnAndPreservesRoom()
        {
            var room = Room();
            room.SetCell(new Vector2Int(2, 1), CellType.Solid);
            var cells = Snapshot(room);
            var result = GravityReachabilitySearch.Search(room, new Vector2Int(2, 3), GravityDirection.Down, Vector2Int.zero);
            Assert.That(result.Reachable, Is.True);
            Assert.That(result.Path.Count, Is.GreaterThan(2));
            Assert.That(result.Path[result.Path.Count - 1].Position, Is.EqualTo(Vector2Int.zero));
            AssertValidPath(room, result);
            Assert.That(Snapshot(room), Is.EqualTo(cells));
            Assert.That(room.Bounds, Is.EqualTo(new RectInt(0, 0, 5, 5)));
            Assert.That(room.Id, Is.EqualTo(7));
            Assert.That(room.Seed, Is.EqualTo(42));
        }

        [Test]
        public void ConnectedMidFallTargetIsNotAStoppingPoint()
        {
            var result = GravityReachabilitySearch.Search(Room(), new Vector2Int(2, 3),
                GravityDirection.Down, new Vector2Int(2, 2));
            Assert.That(result.Reachable, Is.False);
            Assert.That(result.Path, Is.Empty);
            Assert.That(result.ExploredStates, Is.InRange(1, 100));
        }

        [Test]
        public void BlockedCellExploresFourGravityStatesOnceEach()
        {
            var room = Room();
            for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++) room.SetCell(new Vector2Int(x, y), CellType.Solid);
            room.SetCell(new Vector2Int(1, 1), CellType.Empty);
            room.SetCell(new Vector2Int(3, 3), CellType.Empty);
            var result = GravityReachabilitySearch.Search(room, new Vector2Int(1, 1), GravityDirection.Down, new Vector2Int(3, 3));
            Assert.That(result.Reachable, Is.False);
            Assert.That(result.ExploredStates, Is.EqualTo(4));
            Assert.That(result.Path, Is.Empty);
        }

        [TestCase(1, 1, 0, 0)]
        [TestCase(1, 5, 0, 4)]
        [TestCase(5, 1, 4, 0)]
        public void HandlesSmallAndNarrowRooms(int width, int height, int x, int y)
        {
            var room = new RoomData(7, new RectInt(0, 0, width, height), 42);
            var result = GravityReachabilitySearch.Search(room, Vector2Int.zero, GravityDirection.Down, new Vector2Int(x, y));
            Assert.That(result.Reachable, Is.True);
            AssertValidPath(room, result);
        }

        [Test]
        public void ResultsAreDeterministicAndIndependentOfPlacement()
        {
            var first = Room();
            var second = new RoomData(9, new RectInt(100, 200, 5, 5), 66);
            var a = GravityReachabilitySearch.Search(first, new Vector2Int(2, 2), GravityDirection.Down, Vector2Int.zero);
            var b = GravityReachabilitySearch.Search(second, new Vector2Int(2, 2), GravityDirection.Down, Vector2Int.zero);
            var repeat = GravityReachabilitySearch.Search(first, new Vector2Int(2, 2), GravityDirection.Down, Vector2Int.zero);
            Assert.That(b.Reachable, Is.EqualTo(a.Reachable));
            Assert.That(b.ExploredStates, Is.EqualTo(a.ExploredStates));
            Assert.That(b.Path, Is.EqualTo(a.Path));
            Assert.That(repeat.Path, Is.EqualTo(a.Path));
            Assert.That(repeat.ExploredStates, Is.EqualTo(a.ExploredStates));
        }

        [Test]
        public void InvalidInputsFailBeforeSearchingWithoutMutation()
        {
            var room = Room();
            var zero = Vector2Int.zero;
            var outside = new Vector2Int(5, 0);
            Assert.Throws<ArgumentNullException>(() => GravityReachabilitySearch.Search(null, zero, GravityDirection.Down, zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => GravityReachabilitySearch.Search(room, outside, GravityDirection.Down, zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => GravityReachabilitySearch.Search(room, zero, GravityDirection.Down, outside));
            Assert.Throws<ArgumentOutOfRangeException>(() => GravityReachabilitySearch.Search(room, zero, (GravityDirection)99, zero));
            room.SetCell(zero, CellType.Solid);
            var before = Snapshot(room);
            Assert.Throws<ArgumentException>(() => GravityReachabilitySearch.Search(room, zero, GravityDirection.Down, Vector2Int.one));
            Assert.Throws<ArgumentException>(() => GravityReachabilitySearch.Search(room, Vector2Int.one, GravityDirection.Down, zero));
            Assert.That(Snapshot(room), Is.EqualTo(before));
            var tiny = new RoomData(1, new RectInt(0, 0, 1, 1), 0);
            RoomBorderGenerator.Generate(tiny);
            Assert.Throws<ArgumentException>(() => GravityReachabilitySearch.Search(tiny, zero, GravityDirection.Down, zero));
        }

        private static RoomData Room() => new RoomData(7, new RectInt(0, 0, 5, 5), 42);

        private static CellType[] Snapshot(RoomData room)
        {
            var cells = new CellType[room.Width * room.Height];
            for (int y = 0; y < room.Height; y++)
            for (int x = 0; x < room.Width; x++) cells[y * room.Width + x] = room.GetCell(new Vector2Int(x, y));
            return cells;
        }

        private static void AssertValidPath(RoomData room, GravityReachabilityResult result)
        {
            var unique = new HashSet<PlayerState>();
            for (int i = 0; i < result.Path.Count; i++)
            {
                var state = result.Path[i];
                Assert.That(unique.Add(state), Is.True);
                Assert.That(room.ContainsLocal(state.Position), Is.True);
                Assert.That(room.GetCell(state.Position), Is.EqualTo(CellType.Empty));
                if (i == 0) continue;
                var previous = result.Path[i - 1];
                if (state.Gravity != previous.Gravity)
                {
                    Assert.That(GravityMovementSimulator.SimulateGravity(room, previous), Is.EqualTo(previous));
                    bool previousVertical = previous.Gravity == GravityDirection.Down || previous.Gravity == GravityDirection.Up;
                    bool nextVertical = state.Gravity == GravityDirection.Down || state.Gravity == GravityDirection.Up;
                    Assert.That(nextVertical, Is.Not.EqualTo(previousVertical));
                }
                Assert.That(state, Is.EqualTo(GravityMovementSimulator.SimulateGravity(room,
                    new PlayerState(previous.Position, state.Gravity))));
            }
        }
    }
}
