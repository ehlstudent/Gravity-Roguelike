using System;
using UnityEngine;

namespace GravityRoguelike.Run
{
    /// <summary>The selection result is shared here; confirmation fires only after the UI finishes fading.</summary>
    public static class SelectedClass
    {
        public static ClassDefinition Current { get; private set; }
        public static event Action<ClassDefinition> Confirmed;

        public static void Confirm(ClassDefinition choice)
        {
            if (choice == null) throw new ArgumentNullException(nameof(choice));
            Current = choice;
            Confirmed?.Invoke(choice);
        }

        public static void Clear() => Current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            Confirmed = null;
        }
    }
}
