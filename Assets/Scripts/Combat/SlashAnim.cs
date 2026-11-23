using GravityRoguelike.VFX;
using UnityEngine;

namespace GravityRoguelike.Combat
{
    /// <summary>
    /// The sword's slash effect. Plays the Slash clip and adds code-driven juice on top:
    /// a tint, a quick scale "whoosh", a fade-out at the end, and a few sparks along the swing.
    /// The animation itself only moves the frames and swaps sprites, so these do not conflict.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class SlashAnim : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("Tint of the slash sprite. Slightly transparent, so enemies behind it stay readable.")]
        [SerializeField] private Color tint = new Color(1f, 0.97f, 0.85f, 0.9f);
        [Tooltip("Starts this much smaller and grows to full size: reads as a fast swing.")]
        [SerializeField, Range(0f, 0.5f)] private float whooshScale = 0.2f;
        [Tooltip("Part of the duration (at the end) in which the slash fades out.")]
        [SerializeField, Range(0f, 1f)] private float fadeOutPortion = 0.35f;

        [Header("Sparks")]
        [SerializeField, Min(0)] private int sparkCount = 6;
        [SerializeField] private Color sparkColor = new Color(1f, 0.95f, 0.7f);

        private SpriteRenderer spriteRenderer;
        private Vector3 baseScale = Vector3.one;
        private float startTime;
        private float duration = -1f;

        public void Play(bool swingUp, float duration)
        {
            // Mirror the whole effect, including the cropped frames' position curves.
            // The width upgrade needs no code here: the weapon pivot is scaled, and this effect
            // is spawned below it, so it grows with the sword.
            baseScale = new Vector3(1f, swingUp ? -1f : 1f, 1f);
            transform.localScale = baseScale * (1f - whooshScale);
            GetComponent<Animator>().speed = 1f / duration;
            // Safety net if the animation is interrupted or its final event is missed.
            Destroy(gameObject, duration + 0.1f);

            this.duration = duration;
            startTime = Time.time;
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) spriteRenderer.color = tint;

            SpawnSparks(swingUp);
        }

        private void LateUpdate()
        {
            if (duration <= 0f) return;
            float t = Mathf.Clamp01((Time.time - startTime) / duration);

            // Whoosh: grow quickly to full size in the first third.
            float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(t * 3f), 2f);
            transform.localScale = baseScale * Mathf.Lerp(1f - whooshScale, 1f, grow);

            // Fade out at the end so the slash does not linger over enemies.
            if (spriteRenderer != null)
            {
                float fadeStart = 1f - fadeOutPortion;
                float alpha = t <= fadeStart || fadeOutPortion <= 0f ? 1f : 1f - (t - fadeStart) / fadeOutPortion;
                spriteRenderer.color = new Color(tint.r, tint.g, tint.b, tint.a * alpha);
            }
        }

        private void SpawnSparks(bool swingUp)
        {
            if (sparkCount <= 0) return;
            // Facing direction of the spawn point, including the player's sprite flip (negative
            // scale). TransformVector respects scale; the slash's own mirror is left out on purpose.
            Transform frame = transform.parent != null ? transform.parent : transform;
            Vector2 forward = frame.TransformVector(Vector3.right);
            Vector2 up = frame.TransformVector(Vector3.up);
            if (forward.sqrMagnitude < 0.0001f || up.sqrMagnitude < 0.0001f) return;
            forward.Normalize();
            up.Normalize();

            // Sparks trail toward the side the blade swings to.
            Vector2 direction = (forward + up * (swingUp ? 0.35f : -0.35f)).normalized;

            int layer = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
            // Behind characters: sparks never cover the enemy that is being hit.
            VfxAssets.Burst(transform.position, direction, sparkColor, sparkCount, 6f, 50f, 0.12f, 0.25f,
                layer, VfxAssets.BehindCharactersOrder, 0.2f);
        }

        // Animation event on the last frame of the Slash clip.
        public void DestroySelf()
        {
            Destroy(gameObject);
        }
    }
}