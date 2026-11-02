using System;
using System.Linq;
using GravityRoguelike.World;
using NUnit.Framework;
using UnityEngine;

namespace GravityRoguelike.Tests
{
    public class RoomRegionCleanupTests
    {
        private static RoomData Grid(int origin, params string[] rows)
        {
            var room = new RoomData(7, new RectInt(origin, -origin, rows[0].Length, rows.Length), 93);
            for (int y = 0; y < room.Height; y++)
            for (int x = 0; x < room.Width; x++)
                room.SetCell(new Vector2Int(x, y), rows[y][x] == '.' ? CellType.Empty : CellType.Solid);
            return room;
        }

        private static string[] Rows(RoomData room)
        {
            return Enumerable.Range(0, room.Height).Select(y => new string(Enumerable.Range(0, room.Width)
                .Select(x => room.GetCell(new Vector2Int(x, y)) == CellType.Empty ? '.' : '#').ToArray())).ToArray();
        }

        [TestCase(0)]
        [TestCase(-12)]
        public void RowMajorTieBreakAndEdgeOnlyConnectivity(int origin)
        {
            string[] before = { "#######", "#..#..#", "###.###", "#######" };
            var room = Grid(origin, before);
            var regions = ConnectedRoomRegions.Detect(room);
            CollectionAssert.AreEqual(new[] { 2, 2, 1 }, regions.Select(r => r.Count));
            CollectionAssert.AreEqual(before, Rows(room));
            var result = RoomRegionCleanup.Clean(room);
            Assert.That(result.RegionsRemoved, Is.EqualTo(2));
            Assert.That(result.CellsFilled, Is.EqualTo(3));
            Assert.That(result.ReachabilityChecked, Is.False);
            CollectionAssert.AreEqual(new[] { "#######", "#..####", "#######", "#######" }, Rows(room));
            Assert.That(room.Bounds, Is.EqualTo(new RectInt(origin, -origin, 7, 4)));
            Assert.That(room.Id, Is.EqualTo(7));
            Assert.That(room.Seed, Is.EqualTo(93));
            Assert.That(RoomRegionCleanup.Clean(room).Performed, Is.False);
        }

        [Test]
        public void ProtectedSmallerRegionRemainsGravityReachable()
        {
            var room = Grid(0, "########", "#...#..#", "########");
            var start = new Vector2Int(5, 1);
            var goal = new Vector2Int(6, 1);
            var result = RoomRegionCleanup.Clean(room, start, goal);
            Assert.That(result.PositionsInSameRegion, Is.True);
            Assert.That(result.InitiallyReachable, Is.True);
            Assert.That(result.ReachabilityPreserved, Is.True);
            Assert.That(GravityReachabilitySearch.Search(room, start, GravityDirection.Down, goal).Reachable, Is.True);
            CollectionAssert.AreEqual(new[] { "########", "#####..#", "########" }, Rows(room));
        }

        [TestCase(4, 1)]
        [TestCase(-1, 1)]
        [TestCase(0, 1)]
        public void InvalidOrSeparatedEndpointsPreventAllWrites(int x, int y)
        {
            string[] before = { "######", "#..#.#", "######" };
            var room = Grid(0, before);
            var result = RoomRegionCleanup.Clean(room, Vector2Int.one, new Vector2Int(x, y));
            Assert.That(result.Succeeded || result.Performed, Is.False);
            Assert.That(result.Reason, Is.Not.Empty);
            CollectionAssert.AreEqual(before, Rows(room));
        }

        [Test]
        public void EmptyPerimeterCannotBeFilled()
        {
            string[] before = { ".####", "#####", "#...#", "#####" };
            var room = Grid(0, before);
            Assert.That(RoomRegionCleanup.Clean(room).Succeeded, Is.False);
            CollectionAssert.AreEqual(before, Rows(room));
        }

        [TestCase("#####", 0)]
        [TestCase("#...#", 1)]
        public void SolidOrOpenInteriorNeedsNoWrites(string middle, int count)
        {
            string[] before = { "#####", middle, "#####" };
            var room = Grid(0, before);
            var result = RoomRegionCleanup.Clean(room);
            Assert.That(result.RegionsDetected, Is.EqualTo(count));
            Assert.That(result.Succeeded, Is.EqualTo(count == 1));
            Assert.That(result.Performed, Is.False);
            CollectionAssert.AreEqual(before, Rows(room));
        }

        [Test]
        public void RejectsNullRoom()
        {
            Assert.Throws<ArgumentNullException>(() => RoomRegionCleanup.Clean(null));
        }
    }
}
