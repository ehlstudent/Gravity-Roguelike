using System;
using System.Collections.Generic;
using GravityRoguelike.World;
using UnityEngine;

namespace GravityRoguelike.Items
{
    /// <summary>
    /// Creates item pickups in generated rooms. Put it on an empty GameObject and
    /// drag it into ProceduralGameplayDemo's "Item Spawner" field.
    /// Without sprites, items appear as colored squares.
    /// </summary>
    public class ItemSpawner : MonoBehaviour
    {
        [Header("Sprites (optional)")]
        [SerializeField] private Sprite heartSprite;
        [SerializeField] private Sprite maxHealthCrystalSprite;
        [SerializeField] private Sprite damageShardSprite;
        [SerializeField] private Sprite speedBootsSprite;

        [Header("Placement")]
        [Min(0), SerializeField] private int itemsPerRoom = 3;
        [Tooltip("Manhattan distance kept free around a room's start and goal.")]
        [Min(0), SerializeField] private int keepClearAroundEndpoints = 2;
        [Tooltip("Minimum Manhattan distance between two items.")]
        [Min(0), SerializeField] private int minItemSpacing = 4;

        [Header("Look")]
        [Min(0.05f), SerializeField] private float pickupRadius = 0.35f;
        [Min(0.05f), SerializeField] private float itemScale = 0.7f;
        [SerializeField] private int sortingOrder = 5;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private static Sprite fallbackSprite;

        public IReadOnlyList<GameObject> Spawned => spawned;

        public void Clear()
        {
            foreach (GameObject item in spawned)
                if (item != null) Destroy(item);
            spawned.Clear();
        }

        /// <param name="cellToWorld">Turns a local room cell into a world position (cell center).</param>
        public List<PlacedItem> SpawnInRoom(RoomData room, Vector2Int start, Vector2Int goal, int seed,
            Func<Vector2Int, Vector2> cellToWorld, IReadOnlyDictionary<Vector2Int, int> restingCells = null)
        {
            if (cellToWorld == null) throw new ArgumentNullException(nameof(cellToWorld));
            var placed = ItemPlacement.Place(restingCells ?? ItemPlacement.FindRestingCells(room, start, GravityDirection.Down), new[] { start, goal },
                itemsPerRoom, keepClearAroundEndpoints, minItemSpacing, new System.Random(seed));

            foreach (PlacedItem item in placed)
                spawned.Add(CreatePickup(item.Type, cellToWorld(item.Cell)));
            return placed;
        }

        private GameObject CreatePickup(ItemType type, Vector2 position)
        {
            var root = new GameObject($"Item_{type}");
            root.transform.SetParent(transform, false);
            root.transform.position = position;

            var collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = pickupRadius;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * itemScale;
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            Sprite sprite = SpriteFor(type);
            if (sprite != null) renderer.sprite = sprite;
            else
            {
                renderer.sprite = FallbackSprite();
                renderer.color = FallbackColor(type);
            }

            root.AddComponent<ItemPickup>().Init(type, visual.transform);
            return root;
        }

        private Sprite SpriteFor(ItemType type)
        {
            switch (type)
            {
                case ItemType.Heart: return heartSprite;
                case ItemType.MaxHealthCrystal: return maxHealthCrystalSprite;
                case ItemType.DamageShard: return damageShardSprite;
                case ItemType.SpeedBoots: return speedBootsSprite;
                default: return null;
            }
        }

        private static Color FallbackColor(ItemType type)
        {
            switch (type)
            {
                case ItemType.Heart: return new Color(0.88f, 0.2f, 0.35f);
                case ItemType.MaxHealthCrystal: return new Color(0.7f, 0.42f, 0.94f);
                case ItemType.DamageShard: return new Color(1f, 0.6f, 0.24f);
                default: return new Color(0.43f, 0.9f, 1f);
            }
        }

        private static Sprite FallbackSprite()
        {
            if (fallbackSprite != null) return fallbackSprite;
            var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            fallbackSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return fallbackSprite;
        }
    }
}
