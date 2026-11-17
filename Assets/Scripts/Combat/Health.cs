using System;
using UnityEngine;

namespace GravityRoguelike.Combat
{
    /// <summary>Everything a single hit carries. A struct: copied cheaply, no garbage.</summary>
    public struct DamageInfo
    {
        public int amount;              // how much health to remove
        public Vector2 sourcePosition;  // where the hit came from (for knockback direction)
        public float knockbackForce;    // push strength, 0 = no knockback
    }

    /// <summary>One receiver per target; child colliders share this health pool.</summary>
    public class Health : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 3;
        [Tooltip("Seconds of immunity after a hit. 0 for enemies, ~1 for the player.")]
        [SerializeField, Min(0f)] private float invincibilityDuration = 0f;
        [Tooltip("Untick if the owner handles its own death (animation, drops).")]
        [SerializeField] private bool deactivateOnDeath = true;

        public int MaxHealth => maxHealth;
        public int CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth == 0;


        // Fired after damage was applied (also for the killing hit)
        public event Action<DamageInfo> Damaged;
        // Fired once when health reaches 0, before the object is deactivated
        public event Action Died;

        private int baseMaxHealth;
        private float invincibleUntil;   // Time.time until which hits are ignored

        private void Awake()
        {
            baseMaxHealth = maxHealth;
        }

        private void OnEnable()
        {
            CurrentHealth = Mathf.Max(1, maxHealth);
            invincibleUntil = 0f;         // fresh start after re-enabling
        }

        /// <summary>Old entry point: plain damage with no direction. Still used where no source is known.</summary>
        public bool TakeDamage(int amount)
        {
            return TakeDamage(new DamageInfo { amount = amount });
        }

        /// <summary>Full hit with source and knockback. Returns false if ignored (dead, inactive, invincible).</summary>
        public bool TakeDamage(DamageInfo info)
        {
            if (info.amount <= 0) throw new ArgumentOutOfRangeException(nameof(info.amount));
            if (!isActiveAndEnabled || IsDead) return false;
            if (Time.time < invincibleUntil) return false;

            invincibleUntil = Time.time + invincibilityDuration;
            CurrentHealth = Mathf.Max(0, CurrentHealth - info.amount);
            Damaged?.Invoke(info);

            if (IsDead)
            {
                Died?.Invoke();
                if (deactivateOnDeath) gameObject.SetActive(false);
            }
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
