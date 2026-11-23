using UnityEngine;

namespace GravityRoguelike.Combat
{
    /// <summary>
    /// Runtime references available to upgrade definitions.
    /// The player is the root object containing the UpgradeManager.
    /// </summary>
    public sealed class UpgradeContext
    {
        public GameObject Player { get; }
        public ActiveWeapon ActiveWeapon { get; }

        public UpgradeContext(GameObject player, ActiveWeapon activeWeapon)
        {
            Player = player;
            ActiveWeapon = activeWeapon;
        }

        /// <summary>Finds a component on the player or any of its children, including inactive children.</summary>
        public T GetPlayerComponent<T>() where T : Component
        {
            return Player != null ? Player.GetComponentInChildren<T>(true) : null;
        }

        /// <summary>Finds a component on the active weapon or any of its children, including inactive children.</summary>
        public T GetWeaponComponent<T>() where T : Component
        {
            return ActiveWeapon != null ? ActiveWeapon.GetComponentInChildren<T>(true) : null;
        }
    }
}
