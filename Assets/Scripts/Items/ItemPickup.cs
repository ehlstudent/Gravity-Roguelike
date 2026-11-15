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

        private Transform visual;
        private float bobOffset;

        public ItemType Type => type;

        public void Init(ItemType itemType, Transform bobbingVisual)
        {
            type = itemType;
            visual = bobbingVisual;
            bobOffset = Random.value * Mathf.PI * 2f;
        }

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void Update()
        {
            if (visual == null) return;
            visual.localPosition = Vector3.up * (Mathf.Sin(Time.time * bobSpeed + bobOffset) * bobHeight);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController2D>();
            if (player == null) return;
            if (ItemEffects.TryApply(type, player)) Destroy(gameObject);
        }
    }
}
