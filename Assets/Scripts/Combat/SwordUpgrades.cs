using UnityEngine;

namespace GravityRoguelike.Combat
{
    /// <summary>
    /// Holds the sword upgrade levels of the current run and applies them to the sword(s).
    /// Levels are per run only: call ResetUpgrades() when a new run starts.
    /// </summary>
    public class SwordUpgrades : MonoBehaviour
    {
        public const int MaxLevel = 3;

        [Header("Swords")]
        [SerializeField] private Sword primarySword;
        [Tooltip("Pivot object of the second sword (ActiveWeapon with Mirror Aim on). Starts disabled.")]
        [SerializeField] private GameObject secondSwordRoot;

        [Header("Per level")]
        [Tooltip("Width bonus per level, additive: 0.25 means 1.25x / 1.5x / 1.75x.")]
        [SerializeField, Min(0f)] private float widthBonusPerLevel = 0.25f;
        [Tooltip("Speed bonus per level, additive: 0.2 means 1.2x / 1.4x / 1.6x swing speed.")]
        [SerializeField, Min(0f)] private float speedBonusPerLevel = 0.2f;

        private Sword secondSword;

        public int WidthLevel { get; private set; }
        public int SpeedLevel { get; private set; }
        public bool HasSecondSword { get; private set; }

        /// <summary>Finds the second Sword (even though its object is inactive) and applies level 0.</summary>
        private void Awake()
        {
            if (secondSwordRoot != null)
                secondSword = secondSwordRoot.GetComponentInChildren<Sword>(true);
            ResetUpgrades();
        }

        /// <summary>Buys one width level. Returns false at max level.</summary>
        public bool TryUpgradeWidth()
        {
            if (WidthLevel >= MaxLevel) return false;
            WidthLevel++;
            ApplyToSwords();
            return true;
        }

        /// <summary>Buys one speed level. Returns false at max level.</summary>
        public bool TryUpgradeSpeed()
        {
            if (SpeedLevel >= MaxLevel) return false;
            SpeedLevel++;
            ApplyToSwords();
            return true;
        }

        /// <summary>Unlocks the second sword (one-time, no levels). Returns false if already unlocked.</summary>
        public bool TryUnlockSecondSword()
        {
            if (HasSecondSword || secondSwordRoot == null) return false;
            HasSecondSword = true;
            secondSwordRoot.SetActive(true);
            ApplyToSwords();
            return true;
        }

        /// <summary>Removes all upgrades, e.g. at the start of a new run.</summary>
        public void ResetUpgrades()
        {
            WidthLevel = 0;
            SpeedLevel = 0;
            HasSecondSword = false;
            if (secondSwordRoot != null) secondSwordRoot.SetActive(false);
            ApplyToSwords();
        }

        /// <summary>Pushes the current levels as multipliers to both swords so they stay identical.</summary>
        private void ApplyToSwords()
        {
            float width = 1f + widthBonusPerLevel * WidthLevel;
            float speed = 1f + speedBonusPerLevel * SpeedLevel;
            Apply(primarySword, width, speed);
            Apply(secondSword, width, speed);
        }

        /// <summary>Sets width and speed on one sword if it exists.</summary>
        private static void Apply(Sword sword, float width, float speed)
        {
            if (sword == null) return;
            sword.SetWidthMultiplier(width);
            sword.SetSpeedMultiplier(speed);
        }
    }
}