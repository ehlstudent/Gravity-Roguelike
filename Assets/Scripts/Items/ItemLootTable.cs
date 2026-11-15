using System;

namespace GravityRoguelike.Items
{
    /// <summary>Seeded weighted roll: common 60, uncommon 25, rare 10 per item.</summary>
    public static class ItemLootTable
    {
        private static readonly ItemType[] Items =
        {
            ItemType.Heart, ItemType.DamageShard, ItemType.SpeedBoots, ItemType.MaxHealthCrystal
        };

        public static ItemRarity GetRarity(ItemType type)
        {
            switch (type)
            {
                case ItemType.Heart: return ItemRarity.Common;
                case ItemType.DamageShard: return ItemRarity.Uncommon;
                case ItemType.SpeedBoots: return ItemRarity.Uncommon;
                case ItemType.MaxHealthCrystal: return ItemRarity.Rare;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown item type.");
            }
        }

        public static int GetWeight(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return 60;
                case ItemRarity.Uncommon: return 25;
                case ItemRarity.Rare: return 10;
                default: throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "Unknown rarity.");
            }
        }

        /// <summary>Same Random state gives the same item.</summary>
        public static ItemType Roll(Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            int total = 0;
            foreach (ItemType item in Items) total += GetWeight(GetRarity(item));

            int roll = rng.Next(total);
            foreach (ItemType item in Items)
            {
                roll -= GetWeight(GetRarity(item));
                if (roll < 0) return item;
            }
            return Items[Items.Length - 1];
        }
    }
}
