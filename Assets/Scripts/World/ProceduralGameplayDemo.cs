using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GravityRoguelike.World
{
    public class ProceduralGameplayDemo : MonoBehaviour
    {
        [SerializeField] private int seed = 123;
        [SerializeField] private RoomTilemapRenderer roomRenderer;
        [SerializeField] private Tilemap map;
        [SerializeField] private PlayerController2D player;
        [SerializeField] private Portal portal;
        [SerializeField] private DemoGoal goal;
        private string failure;

        private void Start()
        {
            var body = player.GetComponent<Rigidbody2D>();
            body.simulated = false;
            try
            {
                var world = RoomPlacementGenerator.Generate(new RectInt(0, 0, 128, 64), seed,
                    2, 40, 40, 26, 26, 3, 500).World;
                foreach (var room in world.Rooms)
                {
                    RoomNoiseGenerator.Generate(room, 0.45f);
                    RoomCellularAutomata.ApplyIterations(room, 5, 5);
                }
                roomRenderer.RenderWorld(world);
                if (world.Rooms.Count != 2 || world.Rooms[0].Id != 1 || world.Rooms[1].Id != 2)
                    throw new InvalidOperationException("Showcase rooms 1 and 2 are required.");
                if (map.gameObject.layer != LayerMask.NameToLayer("Ground"))
                    throw new InvalidOperationException("Generated tilemap must be on Ground.");
                map.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
                Physics2D.SyncTransforms();

                var gravity = GravityRotator.Instance;
                gravity.Rotate(-gravity.GravityAngle);
                body.rotation = 0f;
                var box = player.GetComponent<BoxCollider2D>();
                var a = world.Rooms[0];
                var b = world.Rooms[1];
                var spawn = ClearCenter(a, new Vector2Int(25, 2), box);
                var arrival = ClearCenter(b, new Vector2Int(12, 2), box);
                portal.transform.position = ClearCenter(a, new Vector2Int(30, 2), portal.GetComponent<BoxCollider2D>());
                goal.transform.position = ClearCenter(b, new Vector2Int(28, 2), goal.GetComponent<BoxCollider2D>());
                portal.SetSameSceneArrival(arrival);
                body.position = spawn;
                body.angularVelocity = 0f;
                body.linearVelocity = Vector2.zero;
                body.simulated = true;
                Physics2D.SyncTransforms();
            }
            catch (Exception e)
            {
                portal.gameObject.SetActive(false);
                goal.gameObject.SetActive(false);
                failure = "Demo initialization failed: " + e.Message;
                Debug.LogError(failure, this);
            }
        }

        // All four markers are upright. Spawning and teleporting both reset gravity to Down.
        private Vector2 ClearCenter(RoomData room, Vector2Int cell, BoxCollider2D box)
        {
            if (!room.ContainsLocal(cell) || room.GetCell(cell) != CellType.Empty)
                throw new InvalidOperationException($"Room {room.Id}, cell {cell} must be Empty.");
            Vector2 position = map.GetCellCenterWorld((Vector3Int)room.LocalToWorld(cell));
            Vector2 scale = box.transform.lossyScale;
            Vector2 size = Vector2.Scale(box.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
            if (Physics2D.OverlapBox(position + Vector2.Scale(box.offset, scale), size, 0f, 1 << map.gameObject.layer))
                throw new InvalidOperationException($"Room {room.Id}, cell {cell} lacks clearance for {box.name}.");
            return position;
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(12, 12, 390, 54), failure ?? (goal.Complete ? "LEVEL COMPLETE"
                : "A/D or arrows: Move     Space: Jump     Q/E: Rotate gravity\nHead right to the purple portal, then to the green goal."));
        }
    }
}
