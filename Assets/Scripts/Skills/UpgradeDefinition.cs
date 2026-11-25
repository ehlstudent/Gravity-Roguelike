using UnityEngine;

namespace GravityRoguelike.Combat
{
    /// <summary>
    /// Data and behavior for one upgrade choice.
    /// Create concrete ScriptableObject subclasses for different upgrade types.
    /// </summary>
    public abstract class UpgradeDefinition : ScriptableObject
    {
        [Header("Display")]
        [SerializeField] private string upgradeName = "New Upgrade";
        [TextArea(2, 4)]
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;

        [Header("Leveling")]
        [SerializeField, Min(1)] private int maxLevel = 3;

        public string UpgradeName => string.IsNullOrWhiteSpace(upgradeName) ? name : upgradeName;
        public string Description => description;
        public Sprite Icon => icon;
        public virtual int MaxLevel => Mathf.Max(1, maxLevel);

        /// <summary>
        /// Whether this upgrade can currently be offered.
        /// Override this for requirements such as a specific weapon being present.
        /// </summary>
        public virtual bool IsAvailable(UpgradeContext context) => true;

        /// <summary>
        /// Applies the given resulting level. Implementations must also support level 0,
        /// which is used to remove/reset the upgrade's effect.
        /// </summary>
        public abstract void Apply(UpgradeContext context, int level);
    }
}
