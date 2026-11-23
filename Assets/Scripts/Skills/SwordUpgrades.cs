using UnityEngine;

namespace GravityRoguelike.Combat
{
    /// <summary>
    /// Applies sword-specific upgrade levels to the primary and optional secondary sword.
    /// Upgrade levels themselves are tracked centrally by UpgradeManager.
    /// </summary>
    public class SwordUpgrades : MonoBehaviour
    {
        [Header("Swords")]
        [SerializeField] private Sword primarySword;
        [Tooltip("Pivot object of the second sword (ActiveWeapon with Mirror Aim on). Starts disabled.")]
        [SerializeField] private GameObject secondSwordRoot;

        private Sword secondSword;
        private int widthLevel;
        private int speedLevel;

        public bool HasSecondSword { get; private set; }
        public bool CanUnlockSecondSword => secondSwordRoot != null && !HasSecondSword;

        public float WidthMultiplier { get; private set; } = 1f;
        public float SpeedMultiplier { get; private set; } = 1f;

        private void Awake()
        {
            if (primarySword == null)
                Debug.LogWarning("SwordUpgrades: 'Primary Sword' is not assigned, width and speed upgrades will have no effect.", this);

            if (secondSwordRoot == null)
            {
                Debug.LogWarning("SwordUpgrades: 'Second Sword Root' is not assigned, the second sword cannot be unlocked.", this);
            }
            else
            {
                secondSword = secondSwordRoot.GetComponentInChildren<Sword>(true);
                secondSwordRoot.SetActive(false);
            }

            ResetUpgrades();
        }

        public void ApplyWidthLevel(int level, float bonusPerLevel)
        {
            widthLevel = Mathf.Max(0, level);
            WidthMultiplier = 1f + Mathf.Max(0f, bonusPerLevel) * widthLevel;
            ApplyToSwords();
        }

        public void ApplySpeedLevel(int level, float bonusPerLevel)
        {
            speedLevel = Mathf.Max(0, level);
            SpeedMultiplier = 1f + Mathf.Max(0f, bonusPerLevel) * speedLevel;
            ApplyToSwords();
        }

        public void ApplySecondSwordLevel(int level)
        {
            if (secondSwordRoot == null)
            {
                Debug.LogWarning("SwordUpgrades: cannot unlock the second sword because 'Second Sword Root' is not assigned.", this);
                return;
            }

            HasSecondSword = level > 0;
            secondSwordRoot.SetActive(HasSecondSword);
            ApplyToSwords();
        }

        /// <summary>Removes all sword upgrade effects, e.g. at the start of a new run.</summary>
        public void ResetUpgrades()
        {
            widthLevel = 0;
            speedLevel = 0;
            WidthMultiplier = 1f;
            SpeedMultiplier = 1f;
            HasSecondSword = false;

            if (secondSwordRoot != null)
                secondSwordRoot.SetActive(false);

            ApplyToSwords();
        }

        private void ApplyToSwords()
        {
            Apply(primarySword);
            Apply(secondSword);
        }

        private void Apply(Sword sword)
        {
            if (sword == null) return;

            sword.SetWidthMultiplier(WidthMultiplier);
            sword.SetSpeedMultiplier(SpeedMultiplier);
        }
    }
}
