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
    }
}
