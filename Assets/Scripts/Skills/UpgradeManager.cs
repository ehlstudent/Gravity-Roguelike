using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GravityRoguelike.Combat
{
    /// <summary>
    /// Tracks acquired upgrades and generates level-up choices.
    /// Put this component on the player GameObject.
    /// Press P to test a level-up: three choices are logged and one is selected randomly.
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        private const int ChoicesPerLevelUp = 3;

        [Header("Available upgrades")]
        [SerializeField] private List<UpgradeDefinition> availableUpgrades = new List<UpgradeDefinition>();

        [Header("Weapon reference")]
        [Tooltip("Automatically found in the player's children if left empty.")]
        [SerializeField] private ActiveWeapon activeWeapon;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLevelUps = true;
        [SerializeField] private Key debugLevelUpKey = Key.P;

        private readonly Dictionary<UpgradeDefinition, int> upgradeLevels =
            new Dictionary<UpgradeDefinition, int>();

        private readonly List<UpgradeDefinition> currentChoices =
            new List<UpgradeDefinition>(ChoicesPerLevelUp);

        private UpgradeContext context;

        public IReadOnlyList<UpgradeDefinition> CurrentChoices => currentChoices;

        /// <summary>Raised whenever a new set of level-up choices has been generated.</summary>
        public event Action<IReadOnlyList<UpgradeDefinition>> ChoicesGenerated;

        /// <summary>Raised after an upgrade has been successfully applied.</summary>
        public event Action<UpgradeDefinition, int> UpgradeSelected;

        private void Awake()
        {
            if (activeWeapon == null)
                activeWeapon = GetComponentInChildren<ActiveWeapon>(true);

            context = new UpgradeContext(gameObject, activeWeapon);

            foreach (UpgradeDefinition upgrade in availableUpgrades)
            {
                if (upgrade != null && !upgradeLevels.ContainsKey(upgrade))
                    upgradeLevels.Add(upgrade, 0);
            }
        }

        private void Start()
        {
            ResetUpgrades();
        }

        private void Update()
        {
            if (!enableDebugLevelUps || Keyboard.current == null)
                return;

            if (Keyboard.current[debugLevelUpKey].wasPressedThisFrame)
                DebugLevelUp();
        }

        /// <summary>Returns the acquired level of an upgrade, or 0 if it is not registered.</summary>
        public int GetLevel(UpgradeDefinition upgrade)
        {
            return upgrade != null && upgradeLevels.TryGetValue(upgrade, out int level) ? level : 0;
        }

        /// <summary>
        /// Generates up to three distinct choices from upgrades that are not maxed out
        /// and whose requirements are currently met.
        /// </summary>
        public IReadOnlyList<UpgradeDefinition> GenerateChoices()
        {
            currentChoices.Clear();

            List<UpgradeDefinition> eligible = new List<UpgradeDefinition>();

            foreach (UpgradeDefinition upgrade in availableUpgrades)
            {
                if (upgrade == null || !upgradeLevels.ContainsKey(upgrade))
                    continue;

                int currentLevel = GetLevel(upgrade);
                if (currentLevel >= upgrade.MaxLevel || !upgrade.IsAvailable(context))
                    continue;

                if (!eligible.Contains(upgrade))
                    eligible.Add(upgrade);
            }

            // Fisher-Yates shuffle gives each eligible upgrade an equal chance of appearing.
            for (int i = eligible.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                UpgradeDefinition temp = eligible[i];
                eligible[i] = eligible[j];
                eligible[j] = temp;
            }

            int choiceCount = Mathf.Min(ChoicesPerLevelUp, eligible.Count);
            for (int i = 0; i < choiceCount; i++)
                currentChoices.Add(eligible[i]);

            ChoicesGenerated?.Invoke(currentChoices);
            return currentChoices;
        }

        /// <summary>
        /// Applies a choice from the currently generated set.
        /// Returns false if it is not a current choice or is no longer eligible.
        /// </summary>
        public bool TrySelectUpgrade(UpgradeDefinition upgrade)
        {
            if (upgrade == null || !currentChoices.Contains(upgrade))
                return false;

            int currentLevel = GetLevel(upgrade);
            if (currentLevel >= upgrade.MaxLevel || !upgrade.IsAvailable(context))
                return false;

            int newLevel = currentLevel + 1;
            upgrade.Apply(context, newLevel);
            upgradeLevels[upgrade] = newLevel;

            Debug.Log($"Upgrade acquired: {upgrade.UpgradeName} (Level {newLevel}/{upgrade.MaxLevel})", this);

            currentChoices.Clear();
            UpgradeSelected?.Invoke(upgrade, newLevel);
            return true;
        }

        /// <summary>
        /// Debug-only flow: generate three choices, log them, then randomly acquire one.
        /// </summary>
        public void DebugLevelUp()
        {
            IReadOnlyList<UpgradeDefinition> choices = GenerateChoices();

            if (choices.Count == 0)
            {
                Debug.Log("Level-up: no available upgrades. All upgrades may be maxed out or unavailable.", this);
                return;
            }

            Debug.Log($"Level-up choices ({choices.Count}):", this);
            for (int i = 0; i < choices.Count; i++)
            {
                UpgradeDefinition choice = choices[i];
                int currentLevel = GetLevel(choice);
                Debug.Log(
                    $"{i + 1}. {choice.UpgradeName} — {choice.Description} " +
                    $"(Level {currentLevel}/{choice.MaxLevel} → {currentLevel + 1}/{choice.MaxLevel})",
                    this);
            }

            UpgradeDefinition selected = choices[UnityEngine.Random.Range(0, choices.Count)];
            Debug.Log($"Randomly selected: {selected.UpgradeName}", this);
            TrySelectUpgrade(selected);
        }

        /// <summary>Resets tracked levels and asks each definition to remove its own effect at level 0.</summary>
        public void ResetUpgrades()
        {
            foreach (UpgradeDefinition upgrade in upgradeLevels.Keys)
            {
                upgradeLevels[upgrade] = 0;
                upgrade.Apply(context, 0);
            }

            currentChoices.Clear();
        }
    }
}
