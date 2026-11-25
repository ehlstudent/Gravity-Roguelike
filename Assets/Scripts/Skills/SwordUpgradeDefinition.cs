using UnityEngine;

namespace GravityRoguelike.Combat
{
    public enum SwordUpgradeType
    {
        Width,
        Speed,
        SecondSword
    }

    [CreateAssetMenu(
        fileName = "SwordUpgrade",
        menuName = "Gravity Roguelike/Upgrades/Sword Upgrade")]
    public sealed class SwordUpgradeDefinition : UpgradeDefinition
    {
        [Header("Sword Upgrade")]
        [SerializeField] private SwordUpgradeType upgradeType;

        [Tooltip("Additive width multiplier bonus per level. 0.1 gives 1.1x / 1.2x / 1.3x.")]
        [SerializeField, Min(0f)] private float widthBonusPerLevel = 0.1f;

        [Tooltip("Additive swing speed multiplier bonus per level. 0.2 gives 1.2x / 1.4x / 1.6x.")]
        [SerializeField, Min(0f)] private float speedBonusPerLevel = 0.2f;

        // The second sword is a one-time unlock; width and speed use the configured maximum.
        public override int MaxLevel => upgradeType == SwordUpgradeType.SecondSword ? 1 : base.MaxLevel;

        public override bool IsAvailable(UpgradeContext context)
        {
            SwordUpgrades upgrades = context.GetPlayerComponent<SwordUpgrades>();
            if (upgrades == null) return false;

            return upgradeType != SwordUpgradeType.SecondSword || upgrades.CanUnlockSecondSword;
        }

        public override void Apply(UpgradeContext context, int level)
        {
            SwordUpgrades upgrades = context.GetPlayerComponent<SwordUpgrades>();
            if (upgrades == null)
            {
                Debug.LogWarning($"Cannot apply '{UpgradeName}': no SwordUpgrades component was found on the player.");
                return;
            }

            level = Mathf.Clamp(level, 0, MaxLevel);

            switch (upgradeType)
            {
                case SwordUpgradeType.Width:
                    upgrades.ApplyWidthLevel(level, widthBonusPerLevel);
                    break;

                case SwordUpgradeType.Speed:
                    upgrades.ApplySpeedLevel(level, speedBonusPerLevel);
                    break;

                case SwordUpgradeType.SecondSword:
                    upgrades.ApplySecondSwordLevel(level);
                    break;
            }
        }
    }
}
