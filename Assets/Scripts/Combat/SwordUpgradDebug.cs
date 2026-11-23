#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

namespace GravityRoguelike.Combat
{
    /// <summary>
    /// Test helper: change sword upgrade levels with F-keys and see them on screen, so you
    /// can do and undo each skill and compare. Compiled out of release builds.
    /// F1/F2 width +/-, F3/F4 speed +/-, F5 toggle second sword, F6 reset all.
    /// </summary>
    [RequireComponent(typeof(SwordUpgrades))]
    public class SwordUpgradesDebug : MonoBehaviour
    {
        private SwordUpgrades upgrades;

        /// <summary>Caches the SwordUpgrades on the same object.</summary>
        private void Awake()
        {
            upgrades = GetComponent<SwordUpgrades>();
            Debug.Log("SwordUpgradesDebug is running. Click into the Game view, then press F1-F6.", this);
        }

        /// <summary>Reads the debug keys once per frame.</summary>
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame || keyboard.f2Key.wasPressedThisFrame ||
                keyboard.f3Key.wasPressedThisFrame || keyboard.f4Key.wasPressedThisFrame ||
                keyboard.f5Key.wasPressedThisFrame || keyboard.f6Key.wasPressedThisFrame)
                Debug.Log("SwordUpgradesDebug: key received.", this);

            if (keyboard.f1Key.wasPressedThisFrame) upgrades.TryUpgradeWidth();
            if (keyboard.f2Key.wasPressedThisFrame) upgrades.TryDowngradeWidth();
            if (keyboard.f3Key.wasPressedThisFrame) upgrades.TryUpgradeSpeed();
            if (keyboard.f4Key.wasPressedThisFrame) upgrades.TryDowngradeSpeed();
            if (keyboard.f5Key.wasPressedThisFrame)
            {
                // Toggle: unlock if locked, otherwise remove again.
                if (!upgrades.TryUnlockSecondSword()) upgrades.TryRemoveSecondSword();
            }
            if (keyboard.f6Key.wasPressedThisFrame) upgrades.ResetUpgrades();
        }

        /// <summary>Draws the current levels and multipliers in the top-left corner.</summary>
        private void OnGUI()
        {
            GUI.Label(new Rect(10f, 10f, 420f, 90f),
                $"Width  L{upgrades.WidthLevel}/{SwordUpgrades.MaxLevel}  x{upgrades.WidthMultiplier:0.00}   [F1 +, F2 -]\n" +
                $"Speed  L{upgrades.SpeedLevel}/{SwordUpgrades.MaxLevel}  x{upgrades.SpeedMultiplier:0.00}   [F3 +, F4 -]\n" +
                $"Second sword: {(upgrades.HasSecondSword ? "ON" : "off")}   [F5]     Reset all [F6]");
        }
    }
}
#endif