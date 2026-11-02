using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GravityRoguelike.World
{
    // Sweep the actual box along the logical witness, using the demo's grid transform.
    // This is a geometric certificate for the waypoints, not a simulation of input timing.
    public class ShowcaseRouteClearance
    {
        private readonly Tilemap map;
        private readonly ContactFilter2D ground;
        private readonly Collider2D[] overlaps = new Collider2D[1];
        private readonly RaycastHit2D[] hits = new RaycastHit2D[1];

        public ShowcaseRouteClearance(Tilemap map)
        {
            this.map = map;
            ground = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = 1 << map.gameObject.layer };
        }

        public Vector2 Center(RoomData room, Vector2Int local) =>
            map.GetCellCenterWorld((Vector3Int)room.LocalToWorld(local));

        private static Vector2 Size(BoxCollider2D box)
        {
            Vector2 scale = box.transform.lossyScale;
            return Vector2.Scale(box.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
        }

        private static Vector2 Offset(BoxCollider2D box, float angle) =>
            Quaternion.Euler(0, 0, angle) * Vector2.Scale(box.offset, box.transform.lossyScale);

        internal static float Angle(GravityDirection gravity)
        {
            switch (gravity)
            {
                case GravityDirection.Left: return -90;
                case GravityDirection.Right: return 90;
                case GravityDirection.Up: return 180;
                default: return 0;
            }
        }

        public bool Accept(RoomData room, IReadOnlyList<PlayerState> path, BoxCollider2D player, BoxCollider2D marker)
        {
            Vector2 size = Size(player);
            Vector2 position = Center(room, path[0].Position);
            float angle = Angle(path[0].Gravity);
            if (Physics2D.OverlapBox(position + Offset(player, angle), size, angle, ground, overlaps) != 0) return false;

            for (int i = 1; i < path.Count; i++)
            {
                float nextAngle = Angle(path[i].Gravity);
                if (angle != nextAngle)
                {
                    // Enclose all intermediate orientations, including the offset about the pivot.
                    float radius = size.magnitude * 0.5f + Offset(player, 0).magnitude;
                    if (Physics2D.OverlapCircle(position, radius, ground, overlaps) != 0) return false;
                    angle = nextAngle;
                }
                Vector2 next = Center(room, path[i].Position);
                Vector2 delta = next - position;
                if (delta == Vector2.zero) return false; // No stationary midair turn sequence.
                if (Physics2D.BoxCast(position + Offset(player, angle), size, angle, delta.normalized,
                    ground, hits, delta.magnitude) != 0) return false;
                position = next;
            }

            float markerAngle = marker.transform.eulerAngles.z;
            Vector2 markerCenter = position + Offset(marker, markerAngle);
            // The two colliders must both include the shared endpoint pivot.
            if (!ContainsPivot(player) || !ContainsPivot(marker)) return false;
            return Physics2D.OverlapBox(markerCenter, Size(marker), markerAngle, ground, overlaps) == 0;
        }
        private static bool ContainsPivot(BoxCollider2D box) =>
            Mathf.Abs(box.offset.x) < box.size.x / 2 && Mathf.Abs(box.offset.y) < box.size.y / 2;
    }
}
