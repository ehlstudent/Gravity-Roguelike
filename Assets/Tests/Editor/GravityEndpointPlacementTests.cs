using System.Linq;
using GravityRoguelike.World;
using NUnit.Framework;
using UnityEngine;

namespace GravityRoguelike.Tests
{
    public class GravityEndpointPlacementTests
    {
        private static RoomData Grid(int origin, params string[] rows)
        {
            var room = new RoomData(3, new RectInt(origin, -origin, rows[0].Length, rows.Length), 19);
            for (int y = 0; y < room.Height; y++)
            for (int x = 0; x < room.Width; x++)
                room.SetCell(new Vector2Int(x, y), rows[y][x] == '.' ? CellType.Empty : CellType.Solid);
            return room;
        }

        [TestCase(0)]
        [TestCase(-9)]
        public void GreatestSeparationWithRowMajorTieBreak(int origin)
        {
            var room = Grid(origin, "...", "...", "...");
            for (int i = 0; i < 2; i++)
            {
                var pair = GravityEndpointPlacement.Find(room, GravityDirection.Down, 2, 2, 4, 200);
                Assert.That(pair.Start, Is.EqualTo(Vector2Int.zero));
                Assert.That(pair.Goal, Is.EqualTo(new Vector2Int(2, 2)));
                Assert.That(pair.Distance, Is.EqualTo(4));
                Assert.That(pair.MinimumRotations, Is.EqualTo(2));
                Assert.That(pair.EvaluatedPairs, Is.EqualTo(4));
                Assert.That(GravityReachabilitySearch.Search(room, pair.Start.Value, pair.InitialGravity, pair.Goal.Value).Reachable, Is.True);
            }
            Assert.That(room.Bounds, Is.EqualTo(new RectInt(origin, -origin, 3, 3)));
            Assert.That(room.Id, Is.EqualTo(3));
            Assert.That(room.Seed, Is.EqualTo(19));
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                Assert.That(room.GetCell(new Vector2Int(x, y)), Is.EqualTo(CellType.Empty));
        }

        [TestCase(GravityDirection.Right, 0, 2)]
        [TestCase(GravityDirection.Left, 2, 0)]
        public void MidairStartCanFallToGoalWithoutTurning(GravityDirection gravity, int start, int goal)
        {
            var room = Grid(0, "...");
            var result = GravityEndpointPlacement.Find(room, gravity, 0, 0, 2, 2);
            Assert.That(result.Start, Is.EqualTo(new Vector2Int(start, 0)));
            Assert.That(result.Goal, Is.EqualTo(new Vector2Int(goal, 0)));
            Assert.That(GravityMovementSimulator.SimulateGravity(room, new PlayerState(result.Start.Value, gravity)).Position,
                Is.EqualTo(result.Goal.Value));
        }

        [TestCase("###", 0, 0)]
        [TestCase("#.#", 0, 0)]
        [TestCase(".#.", 2, 2)]
        [TestCase("...", 8, 0)]
        public void ImpossibleRoomsRemainUnchanged(string row, int distance, int evaluations)
        {
            var room = Grid(0, row);
            var result = GravityEndpointPlacement.Find(room, GravityDirection.Down, 0, 8, distance, 200);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Goal, Is.Null);
            Assert.That(result.EvaluatedPairs, Is.EqualTo(evaluations));
            Assert.That(result.FailureReason, Is.Not.Empty);
            Assert.That(new string(Enumerable.Range(0, row.Length).Select(x =>
                room.GetCell(new Vector2Int(x, 0)) == CellType.Empty ? '.' : '#').ToArray()), Is.EqualTo(row));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void BudgetFailureDoesNotRuleOutLaterPair(int budget)
        {
            var room = Grid(0, "...");
            // First pair is a zero-turn fall. The opposite trip needs two turns.
            var result = GravityEndpointPlacement.Find(room, GravityDirection.Right, 2, 2, 2, budget);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.EvaluatedPairs, Is.EqualTo(budget));
            StringAssert.Contains("budget", result.FailureReason);
            Assert.That(GravityEndpointPlacement.Find(room, GravityDirection.Right, 2, 2, 2, 2).Succeeded, Is.True);
        }

        [TestCase(-1, 2, 0, 1)]
        [TestCase(2, 1, 0, 1)]
        [TestCase(0, 1, -1, 1)]
        [TestCase(0, 1, 0, -1)]
        public void InvalidSettingsRunNoSearch(int min, int max, int distance, int budget)
        {
            var result = GravityEndpointPlacement.Find(Grid(0, ".."), GravityDirection.Down, min, max, distance, budget);
            Assert.That(result.FailureReason, Is.Not.Empty);
            Assert.That(result.EvaluatedPairs, Is.Zero);
        }
    }
}
