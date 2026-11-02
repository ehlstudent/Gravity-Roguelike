using GravityRoguelike.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GravityRoguelike.Tests
{
    public class ShowcaseRouteClearanceTests
    {
        private GameObject root;
        private Tilemap map;
        private Tile tile;
        private BoxCollider2D player, marker;
        private RoomData room;
        private ShowcaseRouteClearance clearance;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Test grid", typeof(Grid));
            root.transform.position = new Vector3(-4000, -4000);
            var tiles = new GameObject("Walls", typeof(Tilemap), typeof(TilemapCollider2D));
            tiles.transform.SetParent(root.transform, false);
            tiles.layer = LayerMask.NameToLayer("Ground");
            map = tiles.GetComponent<Tilemap>();
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.colliderType = Tile.ColliderType.Grid;
            player = root.AddComponent<BoxCollider2D>();
            player.size = new Vector2(1.5f, 1.62f);
            marker = root.AddComponent<BoxCollider2D>();
            marker.size = new Vector2(1, 2);
            room = new RoomData(1, new RectInt(0, 0, 8, 8), 1);
            clearance = new ShowcaseRouteClearance(map);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(tile);
        }

        private void Wall(int x, int y)
        {
            map.SetTile(new Vector3Int(x, y, 0), tile);
            map.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
            Physics2D.SyncTransforms();
        }

        private static PlayerState State(int x, int y, GravityDirection direction) =>
            new PlayerState(new Vector2Int(x, y), direction);

        [Test]
        public void RejectsInterveningPinchEvenWithClearEndpoints()
        {
            var path = new[] { State(1, 3, GravityDirection.Right), State(6, 3, GravityDirection.Right) };
            Wall(3, 5);
            Wall(3, 1);
            Assert.That(clearance.Accept(room, path, player, marker), Is.True);
            Wall(3, 4);
            Wall(3, 2);
            Assert.That(clearance.Accept(room, path, player, marker), Is.False);
        }

        [Test]
        public void RejectsBlockedArrivalAndTriggerFootprint()
        {
            var path = new[] { State(2, 4, GravityDirection.Down), State(2, 1, GravityDirection.Down) };
            Wall(3, 4);
            Assert.That(clearance.Accept(room, path, player, marker), Is.False);
            var clear = new[] { State(5, 4, GravityDirection.Down), State(5, 1, GravityDirection.Down) };
            Assert.That(clearance.Accept(room, clear, player, marker), Is.True);
            marker.size = new Vector2(4, 2);
            Wall(7, 1);
            Assert.That(clearance.Accept(room, clear, player, marker), Is.False);
        }

        [Test]
        public void RejectsTurnWithInsufficientCornerClearance()
        {
            var path = new[] { State(2, 5, GravityDirection.Down), State(2, 3, GravityDirection.Down),
                State(6, 3, GravityDirection.Right) };
            Assert.That(clearance.Accept(room, path, player, marker), Is.True);
            // Both axis-aligned poses fit at the pivot; the swept corner does not.
            player.size = new Vector2(.9f, .9f);
            Wall(2, 2);
            Assert.That(clearance.Accept(room, path, player, marker), Is.False);
        }
    }
}
