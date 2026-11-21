using System.Collections.Generic;
using UnityEngine;

namespace GravityRoguelike.Combat
{
    /// <summary>Animation-gated B sweeps. Child triggers share the player's existing Rigidbody2D.</summary>
    [DefaultExecutionOrder(110)]
    public class MageSpellArea : MonoBehaviour
    {
        [SerializeField] private Transform owner;
        [SerializeField] private PolygonCollider2D leftSweep;
        [SerializeField] private PolygonCollider2D rightSweep;
        [SerializeField, Min(1)] private int damage = 2;
        [SerializeField] private LayerMask targetLayers = 1;
        [SerializeField] private LayerMask blockingLayers = 8;
        [SerializeField] private Camera viewCamera;
#if UNITY_EDITOR
        [SerializeField] private bool showAreaGizmos = true;
#endif
        private readonly HashSet<Health> hitTargets = new HashSet<Health>();
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private PolygonCollider2D activeSweep;

        private void Awake() => EndSweep();
        private void OnDisable() => EndSweep();

        public void BeginSweep(int side)
        {
            EndSweep();
            activeSweep = side == 0 ? leftSweep : rightSweep;
            if (activeSweep == null || owner == null) return;
            hitTargets.Clear();
            activeSweep.enabled = true;
            // Also sample at the event itself so low frame rates cannot skip a whole hit window.
            DamageTargets();
        }

        public void EndSweep()
        {
            if (leftSweep != null) leftSweep.enabled = false;
            if (rightSweep != null) rightSweep.enabled = false;
            activeSweep = null;
        }

        private void LateUpdate()
        {
            if (activeSweep != null) DamageTargets();
        }

        private void DamageTargets()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            Physics2D.SyncTransforms();
            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(targetLayers);
            activeSweep.Overlap(filter, overlaps);
            foreach (Collider2D target in overlaps)
            {
                var health = target.GetComponentInParent<Health>();
                if (health == null || !health.isActiveAndEnabled || health.transform.IsChildOf(owner) ||
                    hitTargets.Contains(health) || !ArrowProjectile.IsTargetVisible(viewCamera, health)) continue;
                Vector2 hit = target.ClosestPoint(owner.position);
                if (Physics2D.Linecast(owner.position, hit, blockingLayers)) continue;
                hitTargets.Add(health);
                health.TakeDamage(new DamageInfo { amount = damage, sourcePosition = owner.position });
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showAreaGizmos) return;
            Color old = Gizmos.color;
            DrawArea(leftSweep, Color.red);
            DrawArea(rightSweep, Color.yellow);
            Gizmos.color = old;
        }

        private static void DrawArea(PolygonCollider2D area, Color color)
        {
            if (area == null) return;
            Gizmos.color = color;
            for (int path = 0; path < area.pathCount; path++)
            {
                Vector2[] points = area.GetPath(path);
                for (int i = 0; i < points.Length; i++)
                    Gizmos.DrawLine(area.transform.TransformPoint(points[i] + area.offset),
                        area.transform.TransformPoint(points[(i + 1) % points.Length] + area.offset));
            }
        }
#endif
    }
}
