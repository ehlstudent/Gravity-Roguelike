using System;
using System.Collections;
using System.Collections.Generic;
using GravityRoguelike.Items;
using GravityRoguelike.World.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace GravityRoguelike.World
{
    public class ProceduralGameplayDemo : GravityPlacementSettings
    {
        [Tooltip("Repeat the entered seed on launch and on R. Leave off for a fresh seed each run.")]
        [SerializeField] private bool manualSeed;
        [SerializeField] private int seed = 123;
        [Min(0), SerializeField] private int maxWorldGenerationAttempts = 20;
        [Min(0), SerializeField] private int maxRoomPairEvaluations = 30;
        [SerializeField] private RoomTilemapRenderer roomRenderer;
        [SerializeField] private Tilemap map;
        [SerializeField] private PlayerController2D player;
        [SerializeField] private Portal portal;
        [SerializeField] private DemoGoal goal;
        [Tooltip("Optional. Spawns random items in each playable room.")]
        [SerializeField] private ItemSpawner itemSpawner;
        [Tooltip("Visual-only room template. Reads generated tiles without changing their collisions.")]
        [SerializeField] private CosmicTilemapVisuals roomVisuals;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private RoomGrid enemyGrid;

        private bool generating;
        private string status;
        private ShowcaseRouteClearance clearance;
        private Portal middlePortal;
        private readonly SpriteRenderer[] arrivalMarkers = new SpriteRenderer[2];
        private readonly string[] roomTurns = new string[3];
        private bool playing;
        private RoomData[] routeRooms;

        private int CurrentRoomIndex => middlePortal.Used ? 2 : portal.Used ? 1 : 0;

        /// <summary>The existing one-way portal route determines the current generated room.</summary>
        public RoomData CurrentRoom => playing && routeRooms != null ? routeRooms[CurrentRoomIndex] : null;

        /// <summary>Converts the room's grid bounds to a world-space centre, including Grid scale.</summary>
        public bool TryGetCurrentRoomWorldPosition(out Vector2 position)
        {
            RoomData room = CurrentRoom;
            position = Vector2.zero;
            if (room == null) return false;
            RectInt bounds = room.Bounds;
            position = (map.CellToWorld(new Vector3Int(bounds.xMin, bounds.yMin, 0))
                + map.CellToWorld(new Vector3Int(bounds.xMax, bounds.yMax, 0))) * 0.5f;
            return true;
        }

        private void Start()
        {
            if (roomVisuals != null) roomVisuals.BindTo(map);
            StartCoroutine(NewRun());
        }

        private void Update()
        {
            if (!generating && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                StartCoroutine(NewRun());
        }

        private IEnumerator NewRun()
        {
            generating = true;
            playing = false;
            routeRooms = null;
            if (enemySpawner != null) enemySpawner.Clear();
            if (roomVisuals != null) roomVisuals.SetDecorationExclusions();
            player.gameObject.SetActive(false);
            portal.gameObject.SetActive(false);
            goal.gameObject.SetActive(false);
            if (middlePortal == null)
                middlePortal = Instantiate(portal, portal.transform.parent);
            middlePortal.gameObject.SetActive(false);
            foreach (var marker in arrivalMarkers)
                if (marker != null) marker.enabled = false;
            goal.ResetGoal();
            var gravity = GravityRotator.Instance;
            gravity.Rotate(-gravity.GravityAngle);
            gravity.enabled = false;
            var body = player.GetComponent<Rigidbody2D>();
            body.rotation = 0;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0;
            roomRenderer.Clear();
            if (itemSpawner != null) itemSpawner.Clear();
            ItemEffects.ResetUpgrades(player);
            clearance = new ShowcaseRouteClearance(map);

            int initial = manualSeed ? seed : BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);
            string reason = "No world attempts available.";
            for (int attempt = 0; attempt < maxWorldGenerationAttempts; attempt++)
            {
                int candidate = unchecked(initial + attempt * 1000000007);
                status = $"Generating seed {candidate} (attempt {attempt + 1}/{maxWorldGenerationAttempts})";
                yield return null;
                try
                {
                    if (MaxRequiredGravityRotations < MinRequiredGravityRotations || MinRequiredGravityRotations < 0
                        || MinStartGoalDistance < 0 || MaxPlacementEvaluations <= 0 || maxRoomPairEvaluations <= 0
                        || map.gameObject.layer != LayerMask.NameToLayer("Ground"))
                        throw new InvalidOperationException("Check the placement budgets, rotation range, distance and Ground layer.");
                    if (TryWorld(candidate, out reason))
                    {
                        status = $"Seed {candidate}  |  Three gravity routes checked";
                        Debug.Log($"Showcase accepted: input {initial}, seed {candidate}, attempt {attempt + 1}. "
                            + $"Limits: rotations {MinRequiredGravityRotations}-{MaxRequiredGravityRotations}, "
                            + $"distance {MinStartGoalDistance}, evaluations {MaxPlacementEvaluations}.", this);
                        generating = false;
                        yield break;
                    }
                }
                catch (Exception e)
                {
                    reason = e.Message;
                    Debug.LogError(status + ": " + reason, this);
                    break;
                }
                Debug.Log(status + ": " + reason, this);
            }
            roomRenderer.Clear();
            if (itemSpawner != null) itemSpawner.Clear();
            playing = false;
            status = $"LEVEL GENERATION FAILED — input seed {initial}\n{reason}";
            Debug.LogWarning(status, this);
            generating = false;
        }

        private bool TryWorld(int candidate, out string reason)
        {
            var world = RoomPlacementGenerator.Generate(new RectInt(0, 0, 120, 80), candidate,
                6, 18, 32, 14, 24, 3, 500).World;
            var rooms = new List<RoomData>();
            foreach (var room in world.Rooms)
            {
                RoomNoiseGenerator.Generate(room, 0.45f);
                RoomCellularAutomata.ApplyIterations(room, 5, 2);
                if (RoomRegionCleanup.Clean(room).Succeeded) rooms.Add(room);
            }
            if (roomVisuals != null) roomVisuals.SetDecorationSeed(world.Seed);
            roomRenderer.RenderWorld(world);
            map.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
            Physics2D.SyncTransforms();

            var order = new System.Random(candidate);
            for (int i = rooms.Count - 1; i > 0; i--)
            {
                int j = order.Next(i + 1);
                (rooms[i], rooms[j]) = (rooms[j], rooms[i]);
            }
            var exits = new Dictionary<int, GravityEndpointPlacementResult>();
            var goals = new Dictionary<int, GravityEndpointPlacementResult>();
            int pairs = 0;
            reason = "No three distinct rooms qualified within the room-pair and endpoint budgets.";
            var selected = new RoomData[3];
            var legs = new GravityEndpointPlacementResult[3];
            var spawns = new Vector2[3];
            var playerBox = player.GetComponent<BoxCollider2D>();
            foreach (var last in rooms)
            {
                var end = FindLeg(last, goal.GetComponent<BoxCollider2D>(), goals);
                if (!end.Succeeded || !clearance.TryGroundedSpawn(last, end.Start.Value, playerBox, out spawns[2]))
                    continue;
                selected[2] = last;
                legs[2] = end;

                int count = 0;
                foreach (var room in rooms)
                {
                    if (room == last) continue;
                    if (pairs++ >= maxRoomPairEvaluations) return false;
                    var exit = FindLeg(room, portal.GetComponent<BoxCollider2D>(), exits);
                    if (!exit.Succeeded || !clearance.TryGroundedSpawn(room, exit.Start.Value, playerBox, out spawns[count]))
                        continue;
                    selected[count] = room;
                    legs[count] = exit;
                    count++;
                    if (count == 2) break;
                }
                if (count < 2) continue;

                portal.transform.position = clearance.Center(selected[0], legs[0].Goal.Value);
                portal.SetSameSceneArrival(spawns[1]);
                middlePortal.transform.position = clearance.Center(selected[1], legs[1].Goal.Value);
                middlePortal.SetSameSceneArrival(spawns[2]);
                goal.transform.position = clearance.Center(last, end.Goal.Value);
                ShowArrivalMarker(0, spawns[1]);
                ShowArrivalMarker(1, spawns[2]);
                var body = player.GetComponent<Rigidbody2D>();
                player.transform.SetPositionAndRotation(spawns[0], Quaternion.identity);
                player.gameObject.SetActive(true);
                body.position = player.transform.position;
                body.rotation = 0;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0;
                GravityRotator.Instance.enabled = true;
                portal.gameObject.SetActive(true);
                middlePortal.gameObject.SetActive(true);
                goal.gameObject.SetActive(true);
                Physics2D.SyncTransforms();
                int spawnedEnemies = 0;
                for (int i = 0; i < selected.Length; i++)
                {
                    var room = selected[i];
                    var leg = legs[i];
                    roomTurns[i] = TurnKeys(leg);
                    if (enemySpawner != null && enemyGrid != null)
                        spawnedEnemies += enemySpawner.SpawnInRoom(enemyGrid, player.transform, room.Bounds,
                            spawns[i], clearance.Center(room, leg.Goal.Value), unchecked(candidate * 31 + room.Seed));
                    if (itemSpawner != null)
                        itemSpawner.SpawnInRoom(room, leg.Start.Value, leg.Goal.Value,
                            unchecked(candidate * 31 + room.Seed), cell => clearance.Center(room, cell), leg.RestingCosts);
                }
                if (roomVisuals != null)
                    roomVisuals.SetDecorationExclusions(portal.GetComponent<Collider2D>().bounds,
                        middlePortal.GetComponent<Collider2D>().bounds, goal.GetComponent<Collider2D>().bounds,
                        playerBox.bounds, new Bounds(spawns[1], playerBox.bounds.size),
                        new Bounds(spawns[2], playerBox.bounds.size));
                routeRooms = selected;
                playing = true;
                if (enemySpawner != null) Debug.Log($"Spawned {spawnedEnemies} enemies across the three playable rooms.", this);
                return true;
            }
            return false;
        }

        private GravityEndpointPlacementResult FindLeg(RoomData room, BoxCollider2D marker,
            Dictionary<int, GravityEndpointPlacementResult> cache)
        {
            if (cache.TryGetValue(room.Id, out var result)) return result;
            var box = player.GetComponent<BoxCollider2D>();
            result = GravityEndpointPlacement.Find(room, GravityDirection.Down, MinRequiredGravityRotations,
                MaxRequiredGravityRotations, MinStartGoalDistance, MaxPlacementEvaluations,
                route => clearance.Accept(room, route.Path, box, marker), new System.Random(room.Seed),
                cell => clearance.TryGroundedSpawn(room, cell, box, out _));
            cache.Add(room.Id, result);
            return result;
        }

        private void ShowArrivalMarker(int index, Vector2 arrival)
        {
            var arrivalMarker = arrivalMarkers[index];
            if (arrivalMarker == null)
            {
                var entrance = portal.GetComponent<SpriteRenderer>();
                arrivalMarker = new GameObject("Portal Exit").AddComponent<SpriteRenderer>();
                arrivalMarkers[index] = arrivalMarker;
                arrivalMarker.transform.SetParent(transform, true);
                arrivalMarker.sprite = entrance.sprite;
                arrivalMarker.sharedMaterial = entrance.sharedMaterial;
                arrivalMarker.drawMode = entrance.drawMode;
                arrivalMarker.size = entrance.size;
                arrivalMarker.sortingLayerID = entrance.sortingLayerID;
                arrivalMarker.sortingOrder = entrance.sortingOrder;
                arrivalMarker.color = new Color(entrance.color.r, entrance.color.g, entrance.color.b, 0.6f);
                arrivalMarker.transform.localScale = portal.transform.lossyScale * 0.6f;
            }
            arrivalMarker.transform.SetPositionAndRotation(arrival, portal.transform.rotation);
            arrivalMarker.enabled = true;
        }

        private void OnGUI()
        {
            string stage = "";
            if (playing)
            {
                int index = CurrentRoomIndex;
                stage = goal.Complete ? "LEVEL COMPLETE" : index == 2 ? "Room 3: reach the green goal"
                    : $"Room {index + 1}: reach the purple portal";
                if (!goal.Complete) stage += "\nRoute from entry: " + roomTurns[index];
            }
            GUI.Box(new Rect(12, 12, 430, 86), $"{status}\n{stage}\nA/D: Move    Space: Jump    Q/E: Rotate gravity    R: New run");
        }

        private static string TurnKeys(GravityEndpointPlacementResult leg)
        {
            var keys = new List<string>();
            for (int i = 1; i < leg.Path.Count; i++)
            {
                float turn = Mathf.DeltaAngle(ShowcaseRouteClearance.Angle(leg.Path[i - 1].Gravity),
                    ShowcaseRouteClearance.Angle(leg.Path[i].Gravity));
                if (turn != 0) keys.Add(turn > 0 ? "E" : "Q");
            }
            return keys.Count == 0 ? "fall" : string.Join(" → ", keys);
        }
    }
}
