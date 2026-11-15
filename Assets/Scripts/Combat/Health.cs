using System;
using UnityEngine;

namespace GravityRoguelike.Combat
{
    /// <summary>One receiver per target; child colliders share this health pool.</summary>
    public class Health : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 3;

        public int MaxHealth => maxHealth;
        public int CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth == 0;

        private int baseMaxHealth;

        private void Awake()
        {
            baseMaxHealth = maxHealth;
        }

        private void OnEnable()
        {
            CurrentHealth = Mathf.Max(1, maxHealth);
        }

        /// <summary>Returns false for inactive/dead targets. Re-enabling resets health.</summary>
        public bool TakeDamage(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!isActiveAndEnabled || IsDead) return false;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            if (IsDead) gameObject.SetActive(false);
            return true;
        }

        /// <summary>Returns false when inactive, dead or already at full health.</summary>
        public bool Heal(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!isActiveAndEnabled || IsDead || CurrentHealth >= maxHealth) return false;

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            return true;
        }

        /// <summary>Raises the maximum and heals by the same amount (item: Max-HP-Kristall).</summary>
        public void AddMaxHealth(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            maxHealth += amount;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        }

        /// <summary>Removes item bonuses, e.g. for a new run.</summary>
        public void ResetMaxHealth()
        {
            if (baseMaxHealth <= 0) return; // Awake has not run yet: nothing to undo.
            maxHealth = baseMaxHealth;
            CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
        }
    }
}
