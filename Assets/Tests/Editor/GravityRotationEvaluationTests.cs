using System;
using System.Linq;
using GravityRoguelike.World;
using NUnit.Framework;
using UnityEngine;

namespace GravityRoguelike.Tests
{
    public class GravityRotationEvaluationTests
    {
        [TestCase(2, 2, 0, 0, 0)]
        [TestCase(2, 0, 0, 2, 8)]
        [TestCase(0, 0, 1, 1, 1)]
        [TestCase(0, 4, 2, 0, 1)]
        [TestCase(0, 4, 2, 1, 2)]
        public void MinimumCostAndWitness(int x, int y, int cost, int minimum, int maximum)
        {
            var target = new Vector2Int(x, y);
            var start = new Vector2Int(2, 2);
            PlayerState[] first = null;
            foreach (int origin in new[] { 0, 0, -17 })
            {
                var room = new RoomData(3, new RectInt(origin, origin, 5, 5), 42);
                var result = GravityReachabilitySearch.Evaluate(room, start, GravityDirection.Down, target, minimum, maximum);
                Assert.That(result.Reachable, Is.EqualTo(GravityReachabilitySearch.Search(room, start, GravityDirection.Down, target).Reachable));
                Assert.That(result.MinimumRequiredGravityRotations, Is.EqualTo(cost));
                Assert.That(result.MeetsMinimum, Is.EqualTo(cost >= minimum));
                Assert.That(result.MeetsMaximum, Is.EqualTo(cost <= maximum));
                Assert.That(result.MeetsRequirements, Is.EqualTo(cost >= minimum && cost <= maximum));
                Assert.That(result.Path.First(), Is.EqualTo(new PlayerState(start, GravityDirection.Down)));
                Assert.That(result.Path.Last().Position, Is.EqualTo(target));
                int turns = 0;
                for (int i = 1; i < result.Path.Count; i++)
                {
                    var previous = result.Path[i - 1];
                    var next = result.Path[i];
                    if (previous.Gravity != next.Gravity)
                    {
                        Assert.That(GravityMovementSimulator.SimulateGravity(room, previous), Is.EqualTo(previous));
                        bool vertical = previous.Gravity == GravityDirection.Up || previous.Gravity == GravityDirection.Down;
                        Assert.That(next.Gravity == GravityDirection.Up || next.Gravity == GravityDirection.Down, Is.Not.EqualTo(vertical));
                        turns++;
                    }
                    Assert.That(GravityMovementSimulator.SimulateGravity(room, new PlayerState(previous.Position, next.Gravity)), Is.EqualTo(next));
                }
                Assert.That(turns, Is.EqualTo(cost));
                if (first != null) CollectionAssert.AreEqual(first, result.Path);
                first = result.Path.ToArray();
                foreach (var cell in room.Bounds.allPositionsWithin)
                    Assert.That(room.GetCell(room.WorldToLocal(cell)), Is.EqualTo(CellType.Empty));
            }
        }

        [Test]
        public void ChoosesDirectTurnOverDetourAroundRoom()
        {
            var room = new RoomData(1, new RectInt(0, 0, 5, 5), 1);
            var start = new Vector2Int(2, 0);
            var detour = new PlayerState(start, GravityDirection.Down);
            foreach (var direction in new[] { GravityDirection.Right, GravityDirection.Up, GravityDirection.Left, GravityDirection.Down })
                detour = GravityMovementSimulator.SimulateGravity(room, new PlayerState(detour.Position, direction));
            Assert.That(detour.Position, Is.EqualTo(Vector2Int.zero));
            Assert.That(GravityReachabilitySearch.Evaluate(room, start, GravityDirection.Down, detour.Position, 1, 1)
                .MinimumRequiredGravityRotations, Is.EqualTo(1));
        }

        [Test]
        public void ExhaustsFourDirectionsAndEvaluatesAgainAfterRepair()
        {
            var room = new RoomData(2, new RectInt(-5, 4, 5, 3), 99);
            RoomBorderGenerator.Generate(room);
            room.SetCell(new Vector2Int(2, 1), CellType.Solid);
            var start = Vector2Int.one;
            var target = new Vector2Int(3, 1);
            var before = GravityReachabilitySearch.Evaluate(room, start, GravityDirection.Down, target, 0, 8);
            Assert.That(before.ExploredStates, Is.EqualTo(4));
            Assert.That(before.MinimumRequiredGravityRotations, Is.Null);
            Assert.That(before.Reachable || before.MeetsMinimum || before.MeetsMaximum || before.MeetsRequirements, Is.False);
            Assert.That(before.Path, Is.Empty);
            Assert.That(GravityRoomRepair.Repair(room, start, GravityDirection.Down, target, 50, 1).RepairSucceeded, Is.True);
            Assert.That(GravityReachabilitySearch.Evaluate(room, start, GravityDirection.Down, target, 1, 1).MeetsRequirements, Is.True);
            Assert.That(GravityReachabilitySearch.Evaluate(room, start, GravityDirection.Right, target, 0, 0).MeetsRequirements, Is.True);
        }

        [TestCase(-1, 8)]
        [TestCase(0, -1)]
        [TestCase(3, 2)]
        public void RejectsInvalidRange(int minimum, int maximum)
        {
            var room = new RoomData(1, new RectInt(0, 0, 1, 1), 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => GravityReachabilitySearch.Evaluate(room,
                Vector2Int.zero, GravityDirection.Down, Vector2Int.zero, minimum, maximum));
        }
    }
}
