using System.Collections.Generic;
using System.Text;
using GravityRoguelike.World;
using NUnit.Framework;
#if UNITY_EDITOR
using GravityRoguelike.World.Development;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Tilemaps;
#endif
using UnityEngine;

namespace GravityRoguelike.Tests
{
    public class WorldRegressionTests
    {
        [Test]
        public void PlayerStatesDistinguishGravityAndRetainValueSemantics()
        {
            var states = new HashSet<PlayerState>();
            var values = new Dictionary<PlayerState, GravityDirection>();
            foreach (GravityDirection gravity in System.Enum.GetValues(typeof(GravityDirection)))
            {
                var state = new PlayerState(new Vector2Int(3, 5), gravity);
                var duplicate = new PlayerState(new Vector2Int(3, 5), gravity);
                Assert.That(state, Is.EqualTo(duplicate));
                Assert.That(state.GetHashCode(), Is.EqualTo(duplicate.GetHashCode()));
                Assert.That(states.Add(state), Is.True);
                Assert.That(states.Add(duplicate), Is.False);
                values.Add(state, gravity);
                Assert.That(values[duplicate], Is.EqualTo(gravity));
                var position = state.Position;
                position.x = 100;
                Assert.That(state.Position, Is.EqualTo(new Vector2Int(3, 5)));
            }
            Assert.That(states.Count, Is.EqualTo(4));
        }

        [Test]
        public void SeededPlacementNoiseAndCaRemainDeterministic()
        {
            var first = GenerateWorld();
            var second = GenerateWorld();
            Assert.That(first.Rooms.Count, Is.GreaterThan(0));
            Assert.That(Snapshot(first), Is.EqualTo(Snapshot(second)));
            string initial = Snapshot(first);
            foreach (var room in first.Rooms)
                RoomCellularAutomata.ApplyIterations(room, 5, 0);
            Assert.That(Snapshot(first), Is.EqualTo(initial));
            for (int i = 0; i < first.Rooms.Count; i++)
            {
                RoomCellularAutomata.ApplyIterations(first.Rooms[i], 5, 3);
                for (int step = 0; step < 3; step++)
                    RoomCellularAutomata.ApplyStep(second.Rooms[i], 5);
            }
            Assert.That(Snapshot(first), Is.EqualTo(Snapshot(second)));
        }

#if UNITY_EDITOR
        [Test]
        public void DevelopmentSceneGeneratesSmoothsRendersAndResets()
        {
            Vector2 gravityBefore = Physics2D.gravity;
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/TilemapPreview.unity");
            try
            {
                RoomWorldPreview preview = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var candidate = root.GetComponentInChildren<RoomWorldPreview>(true);
                    if (candidate != null) preview = candidate;
                }
                Assert.That(preview, Is.Not.Null);
                var settings = new SerializedObject(preview);
                var config = (WorldBoundsPreview)settings.FindProperty("boundsPreview").objectReferenceValue;
                var renderer = (RoomTilemapRenderer)settings.FindProperty("roomRenderer").objectReferenceValue;
                Assert.That(config, Is.Not.Null);
                Assert.That(renderer, Is.Not.Null);
                renderer.ValidateReferences();
                var tilemap = (Tilemap)new SerializedObject(renderer).FindProperty("targetTilemap").objectReferenceValue;
                preview.GenerateAndRenderWorld();
                var world = preview.GeneratedWorld;
                Assert.That(world, Is.Not.Null);
                Assert.That(world.Rooms.Count, Is.GreaterThan(0));
                string initial = Snapshot(world);
                var expected = config.CreatePlacement().World;
                foreach (var room in expected.Rooms)
                {
                    RoomNoiseGenerator.Generate(room, config.InitialSolidProbability);
                    RoomCellularAutomata.ApplyIterations(room, config.SolidNeighbourThreshold, 1 + config.CAIterations);
                }
                preview.ApplyOneCAStep();
                preview.ApplyConfiguredCAIterations();
                Assert.That(preview.GeneratedWorld, Is.SameAs(world));
                Assert.That(Snapshot(world), Is.EqualTo(Snapshot(expected)));
                foreach (var room in world.Rooms)
                for (int y = 0; y < room.Height; y++)
                for (int x = 0; x < room.Width; x++)
                {
                    var local = new Vector2Int(x, y);
                    var global = room.LocalToWorld(local);
                    Assert.That(tilemap.HasTile(new Vector3Int(global.x, global.y, 0)),
                        Is.EqualTo(room.GetCell(local) == CellType.Solid));
                }
                preview.GenerateAndRenderWorld();
                Assert.That(Snapshot(preview.GeneratedWorld), Is.EqualTo(initial));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            Assert.That(Physics2D.gravity, Is.EqualTo(gravityBefore));
        }
#endif

        private static WorldData GenerateWorld()
        {
            var world = RoomPlacementGenerator.Generate(new RectInt(0, 0, 60, 36), 123,
                8, 8, 18, 6, 14, 2, 500).World;
            foreach (var room in world.Rooms) RoomNoiseGenerator.Generate(room, 0.45);
            return world;
        }

        private static string Snapshot(WorldData world)
        {
            var text = new StringBuilder();
            text.Append(world.Seed).Append(world.Bounds);
            foreach (var room in world.Rooms)
            {
                text.Append(room.Id).Append(room.Bounds).Append(room.Seed);
                for (int y = 0; y < room.Height; y++)
                for (int x = 0; x < room.Width; x++)
                    text.Append((int)room.GetCell(new Vector2Int(x, y)));
            }
            return text.ToString();
        }
    }
}
