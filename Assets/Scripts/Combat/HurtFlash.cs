using System.Collections;
using GravityRoguelike.VFX;
using UnityEngine;

namespace GravityRoguelike.Combat
{
    /// <summary>Shared Health-driven flash for every child sprite; preserves each original material.</summary>
    [RequireComponent(typeof(Health))]
    public class HurtFlash : MonoBehaviour
    {
        [SerializeField] private Material flashMaterial;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.15f;
        [SerializeField] private bool screenPulse;
        [SerializeField, Range(0, 1)] private float pulseIntensity = 0.15f;
        [SerializeField, Min(0.01f)] private float pulseDuration = 0.2f;
        [Tooltip("0 disables hit-stop. Uses unscaled time and shares one stop across simultaneous hits.")]
        [SerializeField, Range(0, 0.08f)] private float hitStopDuration;

        private Health health;
        private SpriteRenderer[] sprites;
        private Material[] originals;
        private MaterialPropertyBlock properties;
        private float flashStarted;
        private bool flashing;
        private static bool stopping;
        private static float stopUntil;
        private static float previousTimeScale;

        private void Awake()
        {
            health = GetComponent<Health>();
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            originals = new Material[sprites.Length];
            properties = new MaterialPropertyBlock();
        }

        private void OnEnable() => health.Damaged += OnDamaged;
        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            Restore();
        }

        private void OnDamaged(DamageInfo info)
        {
            flashStarted = Time.unscaledTime;
            if (!flashing && flashMaterial != null)
            {
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (sprites[i] == null) continue;
                    originals[i] = sprites[i].sharedMaterial;
                    sprites[i].sharedMaterial = flashMaterial;
                }
                flashing = true;
            }
            Apply(1);
            if (screenPulse) GameVfx.EnsureInstance().Pulse(flashColor, pulseIntensity, pulseDuration);
            if (hitStopDuration > 0 && (stopping || Time.timeScale > 0))
            {
                stopUntil = Mathf.Max(stopUntil, Time.realtimeSinceStartup + hitStopDuration);
                if (!stopping) GameVfx.EnsureInstance().StartCoroutine(StopTime());
            }
        }

        private void LateUpdate()
        {
            if (!flashing) return;
            float strength = 1 - (Time.unscaledTime - flashStarted) / flashDuration;
            if (strength <= 0) Restore();
            else Apply(strength);
        }

        private void Apply(float strength)
        {
            if (!flashing) return;
            foreach (SpriteRenderer sprite in sprites)
            {
                if (sprite == null) continue;
                sprite.GetPropertyBlock(properties);
                properties.SetFloat("_FlashAmount", strength);
                properties.SetColor("_FlashColor", flashColor);
                sprite.SetPropertyBlock(properties);
            }
        }

        private void Restore()
        {
            if (!flashing) return;
            Apply(0);
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] != null) sprites[i].sharedMaterial = originals[i];
            flashing = false;
        }

        private static IEnumerator StopTime()
        {
            stopping = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
            while (Time.realtimeSinceStartup < stopUntil) yield return null;
            if (Time.timeScale == 0) Time.timeScale = previousTimeScale;
            stopping = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStop()
        {
            if (stopping && Time.timeScale == 0) Time.timeScale = previousTimeScale;
            stopping = false;
            stopUntil = 0;
        }
    }
}
