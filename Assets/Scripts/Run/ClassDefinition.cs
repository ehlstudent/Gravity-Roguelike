using UnityEngine;

namespace GravityRoguelike.Run
{
    [CreateAssetMenu(menuName = "Gravity Roguelike/Class Definition")]
    public class ClassDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField, TextArea(3, 5)] private string description;
        [SerializeField, Range(1, 5)] private int difficulty = 1;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Sprite portrait;
        [SerializeField] private Color accentColor = Color.white;

        public string DisplayName => displayName;
        public string Description => description;
        public int Difficulty => difficulty;
        public GameObject PlayerPrefab => playerPrefab;
        public Sprite Portrait => portrait;
        public Color AccentColor => accentColor;
    }
}
