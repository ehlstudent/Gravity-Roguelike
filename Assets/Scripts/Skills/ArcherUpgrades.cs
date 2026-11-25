using UnityEngine;

namespace GravityRoguelike.Combat
{
    /// <summary>
    /// Applies archer-specific upgrade levels and exposes the resulting values to ArcherBow.
    /// Upgrade levels themselves are tracked centrally by UpgradeManager.
    /// Put this component somewhere in the Player hierarchy.
    /// </summary>
    public class ArcherUpgrades : MonoBehaviour
    {
        [Header("Point Blank Shot")]
        [Tooltip("An enemy closer than this (world units) to the fire point makes the shot a one-shot.")]
        [SerializeField, Min(0f)] private float pointBlankRadius = 2.5f;
        [Tooltip("Seconds until the point blank shot is ready again after it was used.")]
        [SerializeField, Min(0f)] private float pointBlankCooldown = 15f;
        [Tooltip("Damage of a point blank shot. High enough to kill any enemy in one hit.")]
        [SerializeField, Min(1)] private int pointBlankDamage = 9999;
        [Tooltip("Layers that contain enemies.")]
        [SerializeField] private LayerMask enemyLayers = ~0;

        private int speedLevel;
        private int pierceLevel;
        private int pointBlankLevel;
        private float nextPointBlankAt;

        /// <summary>Arrow speed multiplier (1 without upgrade).</summary>
        public float SpeedMultiplier { get; private set; } = 1f;

        /// <summary>How many enemies an arrow can pierce (one per Piercing level).</summary>
        public int PierceCount => pierceLevel;

        /// <summary>True once the Point Blank Shot upgrade has been acquired.</summary>
        public bool PointBlankEnabled => pointBlankLevel > 0;

        /// <summary>Resets all upgrades once when the scene starts.</summary>
        private void Awake()
        {
            ResetUpgrades();
        }

        /// <summary>Sets the arrow speed level; each level adds bonusPerLevel to the multiplier.</summary>
        public void ApplyProjectileSpeedLevel(int level, float bonusPerLevel)
        {
            speedLevel = Mathf.Max(0, level);
            SpeedMultiplier = 1f + Mathf.Max(0f, bonusPerLevel) * speedLevel;
        }

        /// <summary>Sets the Piercing level (0 = off).</summary>
        public void ApplyPierceLevel(int level)
        {
            pierceLevel = Mathf.Max(0, level);
        }

        /// <summary>Sets the Point Blank Shot level (0 = off, 1 = on).</summary>
        public void ApplyPointBlankLevel(int level)
        {
            pointBlankLevel = Mathf.Max(0, level);
            // The shot is ready right after acquiring it.
            nextPointBlankAt = 0f;
        }

        /// <summary>Removes all archer upgrade effects, e.g. at the start of a new run.</summary>
        public void ResetUpgrades()
        {
            speedLevel = 0;
            pierceLevel = 0;
            pointBlankLevel = 0;
            nextPointBlankAt = 0f;
            SpeedMultiplier = 1f;
        }

        /// <summary>
        /// Call when an arrow is fired. If Point Blank Shot is ready and an enemy is within the radius,
        /// returns true with the one-shot damage and starts the cooldown. The aim is not changed.
        /// </summary>
        public bool TryConsumePointBlankShot(Vector2 origin, Transform wielder, out int damage)
        {
            damage = 0;
            if (!PointBlankEnabled || Time.time < nextPointBlankAt) return false;

            foreach (Collider2D candidate in Physics2D.OverlapCircleAll(origin, pointBlankRadius, enemyLayers))
            {
                Health health = candidate.GetComponentInParent<Health>();
                if (health == null || !health.isActiveAndEnabled) continue;
                if (wielder != null && health.transform.IsChildOf(wielder)) continue;

                damage = pointBlankDamage;
                nextPointBlankAt = Time.time + pointBlankCooldown;
                return true;
            }
            return false;
        }
    }
}
