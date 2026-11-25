using UnityEngine;

namespace GravityRoguelike.Combat
{
    public enum ArcherUpgradeType
    {
        Speed,
        Piercing,
        PointBlankShot
    }

    [CreateAssetMenu(
        fileName = "ArcherUpgrade",
        menuName = "Gravity Roguelike/Upgrades/Archer Upgrade")]
    public sealed class ArcherUpgradeDefinition : UpgradeDefinition
    {
        [Header("Archer Upgrade")]
        [SerializeField] private ArcherUpgradeType upgradeType;

        [Tooltip("Additive arrow speed bonus per level (only used by Speed). 0.3 gives 1.3x / 1.6x / 1.9x.")]
        [SerializeField, Min(0f)] private float speedBonusPerLevel = 0.3f;

        /// <summary>Point Blank Shot is a one-time unlock; Speed and Piercing use the configured maximum (default 3).</summary>
        public override int MaxLevel => upgradeType == ArcherUpgradeType.PointBlankShot ? 1 : base.MaxLevel;

        /// <summary>Only offered if the player actually has an ArcherUpgrades component.</summary>
        public override bool IsAvailable(UpgradeContext context)
        {
            return context.GetPlayerComponent<ArcherUpgrades>() != null;
        }

        /// <summary>Forwards the level to ArcherUpgrades. Level 0 removes the effect.</summary>
        public override void Apply(UpgradeContext context, int level)
        {
            ArcherUpgrades upgrades = context.GetPlayerComponent<ArcherUpgrades>();
            if (upgrades == null)
            {
                Debug.LogWarning($"Cannot apply '{UpgradeName}': no ArcherUpgrades component was found on the player.");
                return;
            }

            level = Mathf.Clamp(level, 0, MaxLevel);

            switch (upgradeType)
            {
                case ArcherUpgradeType.Speed:
                    upgrades.ApplyProjectileSpeedLevel(level, speedBonusPerLevel);
                    break;

                case ArcherUpgradeType.Piercing:
                    upgrades.ApplyPierceLevel(level);
                    break;

                case ArcherUpgradeType.PointBlankShot:
                    upgrades.ApplyPointBlankLevel(level);
                    break;
            }
        }
    }
}
