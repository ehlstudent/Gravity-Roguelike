using GravityRoguelike.Combat;
using UnityEngine;

namespace GravityRoguelike.Items
{
    /// <summary>What each item does to the player. Tune the values here.</summary>
    public static class ItemEffects
    {
        public const int HeartHeal = 1;
        public const int MaxHealthBonus = 1;
        public const int DamageBonus = 1;
        public const float SpeedMultiplier = 1.15f;
        /// <summary>Boots stop stacking at this multiple of the base speed.</summary>
        public const float MaxSpeedMultiplier = 1.6f;

        /// <summary>Returns true when the item was used and the pickup should disappear.</summary>
        public static bool TryApply(ItemType type, PlayerController2D player)
        {
            if (player == null) return false;
            switch (type)
            {
                case ItemType.Heart:
                {
                    Health health = FindHealth(player);
                    return health != null && health.Heal(HeartHeal); // Stays on the floor at full HP.
                }
                case ItemType.MaxHealthCrystal:
                {
                    Health health = FindHealth(player);
                    if (health == null) return false;
                    health.AddMaxHealth(MaxHealthBonus);
                    return true;
                }
                case ItemType.DamageShard:
                {
                    Sword sword = player.GetComponentInChildren<Sword>(true);
                    if (sword == null)
                    {
                        Debug.LogWarning("ItemEffects: the player has no Sword, so the damage shard does nothing.", player);
                        return false;
                    }
                    sword.AddDamage(DamageBonus);
                    return true;
                }
                case ItemType.SpeedBoots:
                {
                    float capped = player.BaseMoveSpeed * MaxSpeedMultiplier;
                    player.SetMoveSpeed(Mathf.Min(player.MoveSpeed * SpeedMultiplier, capped));
                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary>Short text shown when the item is picked up, e.g. "+1 Max HP".</summary>
        public static string Describe(ItemType type)
        {
            switch (type)
            {
                case ItemType.Heart: return $"+{HeartHeal} HP";
                case ItemType.MaxHealthCrystal: return $"+{MaxHealthBonus} Max HP";
                case ItemType.DamageShard: return $"+{DamageBonus} Damage";
                case ItemType.SpeedBoots: return $"+{Mathf.RoundToInt((SpeedMultiplier - 1f) * 100f)}% Speed";
                default: return string.Empty;
            }
        }

        /// <summary>Removes all item bonuses, e.g. when a new run starts.</summary>
        public static void ResetUpgrades(PlayerController2D player)
        {
            if (player == null) return;
            Health health = player.GetComponent<Health>();
            if (health != null) health.ResetMaxHealth();
            Sword sword = player.GetComponentInChildren<Sword>(true);
            if (sword != null) sword.ResetDamage();
            player.ResetMoveSpeed();
        }

        private static Health FindHealth(PlayerController2D player)
        {
            Health health = player.GetComponent<Health>();
            if (health == null)
                Debug.LogWarning("ItemEffects: add a Health component to the player for hearts and crystals.", player);
            return health;
        }
    }
}
