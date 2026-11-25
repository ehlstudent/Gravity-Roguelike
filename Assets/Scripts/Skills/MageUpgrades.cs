using UnityEngine;

namespace GravityRoguelike.Combat
{
    /// <summary>
    /// Applies mage-specific upgrade levels (blob modifiers) and exposes the resulting values
    /// to MageSpellController. Upgrade levels themselves are tracked centrally by UpgradeManager.
    /// Put this component somewhere in the Player hierarchy.
    /// </summary>
    public class MageUpgrades : MonoBehaviour
    {
        [Header("Double Blobs")]
        [Tooltip("Total angle in degrees between two neighbouring blobs. 0 = all fly exactly parallel.")]
        [SerializeField, Min(0f)] private float spreadAngle = 8f;

        [Tooltip("Sideways distance in world units between the spawn points of neighbouring blobs.")]
        [SerializeField, Min(0f)] private float spawnSpacing = 0.25f;

        [Header("Explosive Blobs")]
        [Tooltip("Explosion radius in world units for level 1 / 2 / 3.")]
        [SerializeField] private float[] explosionRadiusPerLevel = { 1f, 1.6f, 2.2f };

        private int doubleBlobsLevel;
        private int explosiveLevel;
        private int bouncingLevel;

        /// <summary>How many blobs are fired per Spell A shot (1 without upgrade, up to 4).</summary>
        public int ProjectileCount => 1 + doubleBlobsLevel;

        /// <summary>Wall bounces per blob: one per Bouncing Blobs level.</summary>
        public int BounceCount => bouncingLevel;

        /// <summary>Explosion radius of a blob, 0 if Explosive Blobs is not acquired.</summary>
        public float ExplosionRadius
        {
            get
            {
                if (explosiveLevel <= 0 || explosionRadiusPerLevel == null || explosionRadiusPerLevel.Length == 0)
                    return 0f;
                int index = Mathf.Min(explosiveLevel, explosionRadiusPerLevel.Length) - 1;
                return explosionRadiusPerLevel[index];
            }
        }

        /// <summary>Angle in degrees between neighbouring blobs.</summary>
        public float SpreadAngle => spreadAngle;

        /// <summary>Sideways spawn distance between neighbouring blobs.</summary>
        public float SpawnSpacing => spawnSpacing;

        /// <summary>Resets all upgrades once when the scene starts.</summary>
        private void Awake()
        {
            ResetUpgrades();
        }

        /// <summary>Sets the Double Blobs level (0 = off, 1/2/3 = 2/3/4 blobs).</summary>
        public void ApplyDoubleBlobsLevel(int level)
        {
            doubleBlobsLevel = Mathf.Max(0, level);
        }

        /// <summary>Sets the Explosive Blobs level (0 = off, higher = bigger radius).</summary>
        public void ApplyExplosiveLevel(int level)
        {
            explosiveLevel = Mathf.Max(0, level);
        }

        /// <summary>Sets the Bouncing Blobs level (0 = off, each level adds one wall bounce).</summary>
        public void ApplyBouncingLevel(int level)
        {
            bouncingLevel = Mathf.Max(0, level);
        }

        /// <summary>Removes all mage upgrade effects, e.g. at the start of a new run.</summary>
        public void ResetUpgrades()
        {
            doubleBlobsLevel = 0;
            explosiveLevel = 0;
            bouncingLevel = 0;
        }
    }
}
