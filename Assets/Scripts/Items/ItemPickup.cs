using GravityRoguelike.VFX;
using UnityEngine;

namespace GravityRoguelike.Items
{
    /// <summary>A trigger in the level. Touching it applies the item and removes the pickup.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class ItemPickup : MonoBehaviour
    {
        [SerializeField] private ItemType type;
        [SerializeField] private float bobHeight = 0.08f;
        [SerializeField] private float bobSpeed = 3f;

        [Header("Glow")]
        [Tooltip("Soft light behind the item in its rarity color, so it stands out in dark caves.")]
        [SerializeField] private bool glow = true;
        [Tooltip("Glow size relative to the item sprite.")]
        [SerializeField, Min(0.1f)] private float glowSize = 2f;
        [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.45f;
        [SerializeField, Min(0f)] private float glowPulseSpeed = 2.5f;

        private Transform visual;
        private float bobOffset;
        private float displayedAngle;   // smoothed angle the item is currently drawn at
        [SerializeField, Min(0f)] private float rotateSpeed = 360f;   // degrees per second the item turns toward gravity
        private SpriteRenderer glowRenderer;
        private Color glowColor;
        private Vector3 glowBaseScale;

        public ItemType Type => type;

        public void Init(ItemType itemType, Transform bobbingVisual)
        {
            type = itemType;
            visual = bobbingVisual;
            bobOffset = Random.value * Mathf.PI * 2f;
            displayedAngle = CurrentGravityAngle();
        }

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void Start()
        {
            if (glow) CreateGlow();
        }

        private void Update()
        {
            if (visual != null)
            {
                // Turn smoothly toward the current gravity angle (instead of snapping).
                displayedAngle = Mathf.MoveTowardsAngle(displayedAngle, CurrentGravityAngle(), rotateSpeed * Time.deltaTime);
                Quaternion rotation = Quaternion.Euler(0f, 0f, displayedAngle);

                // Rotate the sprite so it stays upright relative to gravity.
                visual.rotation = rotation;

                // "Up" in world space for the current gravity, converted into the parent's local space.
                Vector3 worldUp = rotation * Vector3.up;
                Transform parent = visual.parent;
                Vector3 localUp = parent != null ? parent.InverseTransformDirection(worldUp) : worldUp;

                visual.localPosition = localUp * (Mathf.Sin(Time.time * bobSpeed + bobOffset) * bobHeight);
            }

            if (glowRenderer != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * glowPulseSpeed + bobOffset);
                glowRenderer.color = new Color(glowColor.r, glowColor.g, glowColor.b, glowAlpha * Mathf.Lerp(0.6f, 1f, pulse));
                glowRenderer.transform.localScale = glowBaseScale * Mathf.Lerp(0.92f, 1.08f, pulse);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController2D>();
            if (player == null) return;
            if (!ItemEffects.TryApply(type, player)) return;

            SpriteRenderer itemRenderer = ItemRenderer();
            Vector3 position = itemRenderer != null ? itemRenderer.bounds.center : transform.position;
            GameVfx.PickupCollected(type, position,
                itemRenderer != null ? itemRenderer.sortingLayerID : 0,
                itemRenderer != null ? itemRenderer.sortingOrder : 0);
            Destroy(gameObject);
        }

        /// <summary>Glow sprite as a child of the bobbing visual, drawn just behind the item.</summary>
        private void CreateGlow()
        {
            SpriteRenderer itemRenderer = ItemRenderer();
            Transform parent = itemRenderer != null ? itemRenderer.transform : transform;

            var glowObject = new GameObject("Glow");
            glowObject.transform.SetParent(parent, false);
            glowRenderer = glowObject.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = VfxAssets.SoftCircle;
            glowRenderer.sharedMaterial = VfxAssets.GlowMaterial;   // unlit: visible without lights
            glowRenderer.sortingLayerID = itemRenderer != null ? itemRenderer.sortingLayerID : 0;
            glowRenderer.sortingOrder = (itemRenderer != null ? itemRenderer.sortingOrder : 0) - 1;
            glowColor = VfxAssets.RarityColor(type);

            // Size in world units from the item sprite; the soft circle is 1 unit wide.
            float itemSize = itemRenderer != null && itemRenderer.sprite != null
                ? Mathf.Max(itemRenderer.bounds.size.x, itemRenderer.bounds.size.y)
                : 0.5f;
            float parentScale = Mathf.Max(0.0001f, Mathf.Abs(parent.lossyScale.x));
            glowBaseScale = Vector3.one * (itemSize * glowSize / parentScale);
            glowObject.transform.localScale = glowBaseScale;
        }

        private SpriteRenderer ItemRenderer()
        {
            if (visual != null)
            {
                SpriteRenderer r = visual.GetComponent<SpriteRenderer>();
                if (r != null) return r;
            }
            foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>())
            {
                if (r != glowRenderer) return r;
            }
            return null;
        }
        /// <summary>Current gravity angle in degrees (0 if there is no GravityRotator in the scene).</summary>
        private static float CurrentGravityAngle()
        {
            return GravityRotator.Instance != null ? GravityRotator.Instance.GravityAngle : 0f;
        }
    }
}
