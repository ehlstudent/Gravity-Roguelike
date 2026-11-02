using System;
using System.Collections;
using System.Collections.Generic;
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

        private bool generating;
        private string status;
        private ShowcaseRouteClearance clearance;
        private GravityEndpointPlacementResult legA, legB;
        private string roomALabel, roomBLabel;
        private string roomATurns, roomBTurns;
        private bool playing => legA != null && legB != null;

        private void Start() => StartCoroutine(NewRun());

        private void Update()
        {
            if (!generating && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                StartCoroutine(NewRun());
        }

        private IEnumerator NewRun()
        {
            generating = true;
            legA = legB = null;
            player.gameObject.SetActive(false);
            portal.gameObject.SetActive(false);
            goal.gameObject.SetActive(false);
            goal.ResetGoal();
            var gravity = GravityRotator.Instance;
            gravity.Rotate(-gravity.GravityAngle);
            gravity.enabled = false;
            var body = player.GetComponent<Rigidbody2D>();
            body.rotation = 0;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0;
            roomRenderer.Clear();
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
                        status = $"Seed {candidate}  |  Two gravity routes checked";
                        Debug.Log($"Showcase accepted: input {initial}, seed {candidate}, attempt {attempt + 1}. "
                            + $"{roomALabel}; {roomBLabel}. Limits: rotations {MinRequiredGravityRotations}-{MaxRequiredGravityRotations}, "
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
            legA = legB = null;
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
                RoomCellularAutomata.ApplyIterations(room, 5, 5);
                if (RoomRegionCleanup.Clean(room).Succeeded) rooms.Add(room);
            }
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
            reason = "No two distinct rooms qualified within the room-pair and endpoint budgets.";
            foreach (var a in rooms)
            foreach (var b in rooms)
            {
                if (a == b) continue;
                if (pairs++ >= maxRoomPairEvaluations) return false;
                var exit = FindLeg(a, portal.GetComponent<BoxCollider2D>(), exits);
                if (!exit.Succeeded) continue;
                var end = FindLeg(b, goal.GetComponent<BoxCollider2D>(), goals);
                if (!end.Succeeded) continue;

                portal.transform.position = clearance.Center(a, exit.Goal.Value);
                goal.transform.position = clearance.Center(b, end.Goal.Value);
                portal.SetSameSceneArrival(clearance.Center(b, end.Start.Value));
                var body = player.GetComponent<Rigidbody2D>();
                player.transform.SetPositionAndRotation(clearance.Center(a, exit.Start.Value), Quaternion.identity);
                player.gameObject.SetActive(true);
                body.position = player.transform.position;
                body.rotation = 0;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0;
                GravityRotator.Instance.enabled = true;
                portal.gameObject.SetActive(true);
                goal.gameObject.SetActive(true);
                Physics2D.SyncTransforms();
                legA = exit;
                legB = end;
                roomALabel = $"Room {a.Id}: {exit.Start} → {exit.Goal}, {exit.MinimumRotations} turns";
                roomBLabel = $"Room {b.Id}: {end.Start} → {end.Goal}, {end.MinimumRotations} turns";
                roomATurns = TurnKeys(exit);
                roomBTurns = TurnKeys(end);
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
                route => clearance.Accept(room, route.Path, box, marker), new System.Random(room.Seed));
            cache.Add(room.Id, result);
            return result;
        }

        private void OnGUI()
        {
            string stage = "";
            if (playing)
                stage = goal.Complete ? "LEVEL COMPLETE" : portal.Used ? "Room B: reach the green goal" : "Room A: reach the purple portal";
            if (playing && !goal.Complete)
                stage += "\nRoute from entry: " + (portal.Used ? roomBTurns : roomATurns);
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
