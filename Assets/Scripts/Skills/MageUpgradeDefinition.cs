using UnityEngine;

namespace GravityRoguelike.Combat
{
    public enum MageUpgradeType
    {
        DoubleBlobs,
        ExplosiveBlobs,
        BouncingBlobs
    }

    [CreateAssetMenu(
        fileName = "MageUpgrade",
        menuName = "Gravity Roguelike/Upgrades/Mage Upgrade")]
    public sealed class MageUpgradeDefinition : UpgradeDefinition
    {
        [Header("Mage Upgrade")]
        [SerializeField] private MageUpgradeType upgradeType;

        // MaxLevel comes from the base class (default 3): 3 levels for every mage upgrade.

        /// <summary>Only offered if the player actually has a MageUpgrades component.</summary>
        public override bool IsAvailable(UpgradeContext context)
        {
            return context.GetPlayerComponent<MageUpgrades>() != null;
        }

        /// <summary>Forwards the level to MageUpgrades. Level 0 removes the effect.</summary>
        public override void Apply(UpgradeContext context, int level)
        {
            MageUpgrades upgrades = context.GetPlayerComponent<MageUpgrades>();
            if (upgrades == null)
            {
                Debug.LogWarning($"Cannot apply '{UpgradeName}': no MageUpgrades component was found on the player.");
                return;
            }

            level = Mathf.Clamp(level, 0, MaxLevel);

            switch (upgradeType)
            {
                case MageUpgradeType.DoubleBlobs:
                    upgrades.ApplyDoubleBlobsLevel(level);
                    break;

                case MageUpgradeType.ExplosiveBlobs:
                    upgrades.ApplyExplosiveLevel(level);
                    break;

                case MageUpgradeType.BouncingBlobs:
                    upgrades.ApplyBouncingLevel(level);
                    break;
            }
        }
    }
}
