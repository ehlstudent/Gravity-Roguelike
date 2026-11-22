using System.Collections.Generic;
using GravityRoguelike.Combat;
using GravityRoguelike.Items;
using UnityEngine;
using UnityEngine.UI;

namespace GravityRoguelike.VFX
{
    /// <summary>
    /// Central place for gameplay feedback effects: hit flash and impact particles,
    /// gravity-change and cooldown feedback, pickup bursts and floating text.
    ///
    /// Created automatically when Play starts (like GravityRotator) and kept across scenes.
    /// It only listens to events (Health.AnyDamaged, GravityRotator.GravityChanged /
    /// RotationBlocked), so no scene or prefab has to reference it.
    ///
    /// Readability rule: world effects are drawn BEHIND characters, and screen effects only
    /// touch the screen edges or the top of the screen, so the player and enemies are never
    /// covered, also not while the camera rotates.
    /// </summary>
    [DefaultExecutionOrder(200)]   // after gameplay scripts, so flashes are restored after animation changes
    public class GameVfx : MonoBehaviour
    {
        public static GameVfx Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate() => EnsureInstance();

        /// <summary>Returns the instance, creating it if needed.</summary>
        public static GameVfx EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Game VFX (auto)");
            DontDestroyOnLoad(go);
            return go.AddComponent<GameVfx>();   // Awake sets Instance
        }

        [Header("Hit feedback")]
        [SerializeField, Min(0f)] private float flashDuration = 0.1f;
        [SerializeField] private Color enemyFlashColor = Color.white;
        [SerializeField] private Color playerFlashColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private Color enemyHitParticleColor = new Color(1f, 0.85f, 0.45f);
        [SerializeField] private Color playerHitParticleColor = new Color(1f, 0.3f, 0.25f);
        [SerializeField, Min(0)] private int hitParticleCount = 10;
        [SerializeField, Min(0)] private int killParticleCount = 24;
        [Tooltip("Red screen-edge pulse when the player is hit.")]
        [SerializeField, Range(0f, 1f)] private float playerHitPulseAlpha = 0.3f;

        [Header("Gravity feedback")]
        [SerializeField] private Color gravityColor = new Color(0.4f, 0.9f, 1f);
        [SerializeField] private Color blockedColor = new Color(1f, 0.4f, 0.3f);
        [SerializeField, Range(0f, 1f)] private float gravityPulseAlpha = 0.45f;
        [SerializeField, Min(0)] private int gravityDustCount = 28;
        [Tooltip("Small pulse when the gravity cooldown is over and Q/E work again.")]
        [SerializeField] private bool pulseWhenReady = true;

        [Header("Overlay")]
        [Tooltip("Canvas sorting order of the effect overlay. Keep it below the HUD.")]
        [SerializeField] private int overlaySortingOrder = 50;

        private struct FlashState
        {
            public Material material;
            public Color color;
            public float until;
        }

        private readonly Dictionary<SpriteRenderer, FlashState> flashing = new Dictionary<SpriteRenderer, FlashState>();
        private readonly List<SpriteRenderer> flashBuffer = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> expiredBuffer = new List<SpriteRenderer>();

        private Canvas canvas;
        private Image pulseImage;
        private Color pulseColor;
        private float pulseAlpha;
        private float pulseStart;
        private float pulseDuration = 1f;
        private Text statusText;
        private Color statusColor;
        private float statusStart;
        private const float StatusDuration = 0.9f;

        private PlayerController2D player;
        private bool wasCoolingDown;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            Health.AnyDamaged += OnAnyDamaged;
            GravityRotator.GravityChanged += OnGravityChanged;
            GravityRotator.RotationBlocked += OnRotationBlocked;
        }

        private void OnDisable()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            GravityRotator.GravityChanged -= OnGravityChanged;
            GravityRotator.RotationBlocked -= OnRotationBlocked;
            RestoreAllFlashes();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LateUpdate()
        {
            UpdateFlashes();
            UpdatePulse();
            UpdateStatus();
            UpdateCooldownReady();
        }

        // ------------------------------------------------------------------ hits

        private void OnAnyDamaged(Health health, DamageInfo info)
        {
            if (health == null) return;
            bool isPlayer = health.GetComponent<PlayerController2D>() != null;

            Flash(health, isPlayer ? playerFlashColor : enemyFlashColor);

            SpriteRenderer body = MainRenderer(health);
            int layer = body != null ? body.sortingLayerID : 0;
            int order = (body != null ? body.sortingOrder : 0) - 1;   // behind the one who was hit

            Collider2D col = health.GetComponent<Collider2D>();
            Vector2 center = col != null ? (Vector2)col.bounds.center : (Vector2)health.transform.position;

            // Sparks fly away from the attacker. sourcePosition is (0,0) when the source is unknown.
            Vector2 away = center - info.sourcePosition;
            bool knownSource = info.sourcePosition != Vector2.zero && away.sqrMagnitude > 0.0001f;
            away = knownSource ? away.normalized : Vector2.zero;

            Color sparkColor = isPlayer ? playerHitParticleColor : enemyHitParticleColor;
            if (health.IsDead)
            {
                VfxAssets.Burst(center, Vector2.zero, sparkColor, killParticleCount, 7f, 360f, 0.24f, 0.55f, layer, order, 0.2f);
            }
            else
            {
                VfxAssets.Burst(center, away, sparkColor, hitParticleCount, 5.5f, knownSource ? 80f : 360f, 0.18f, 0.35f, layer, order);
            }

            if (isPlayer) Pulse(playerFlashColor, playerHitPulseAlpha, 0.35f);
        }

        private void Flash(Health target, Color color)
        {
            if (flashDuration <= 0f) return;
            target.GetComponentsInChildren(false, flashBuffer);
            Material flashMaterial = VfxAssets.FlashMaterial;
            float until = Time.unscaledTime + flashDuration;

            foreach (SpriteRenderer r in flashBuffer)
            {
                if (r == null || !r.enabled || r.GetComponent<SlashAnim>() != null) continue;

                if (flashing.TryGetValue(r, out FlashState state))
                {
                    state.until = until;          // hit again while flashing: just extend
                    flashing[r] = state;
                }
                else
                {
                    state = new FlashState { material = r.sharedMaterial, color = r.color, until = until };
                    flashing.Add(r, state);
                }

                if (flashMaterial != null)
                {
                    r.sharedMaterial = flashMaterial;   // solid silhouette in r.color
                    r.color = new Color(color.r, color.g, color.b, state.color.a);
                }
                else
                {
                    r.color = new Color(1f, 0.45f, 0.45f, state.color.a);   // fallback: red tint
                }
            }
            flashBuffer.Clear();
        }

        private void UpdateFlashes()
        {
            if (flashing.Count == 0) return;
            float now = Time.unscaledTime;
            expiredBuffer.Clear();
            foreach (KeyValuePair<SpriteRenderer, FlashState> pair in flashing)
            {
                if (pair.Key == null || now >= pair.Value.until) expiredBuffer.Add(pair.Key);
            }
            foreach (SpriteRenderer r in expiredBuffer) RestoreFlash(r);
        }

        private void RestoreFlash(SpriteRenderer r)
        {
            if (!flashing.TryGetValue(r, out FlashState state)) return;
            flashing.Remove(r);
            if (r == null) return;   // destroyed meanwhile
            r.sharedMaterial = state.material;
            r.color = state.color;
        }

        private void RestoreAllFlashes()
        {
            expiredBuffer.Clear();
            expiredBuffer.AddRange(flashing.Keys);
            foreach (SpriteRenderer r in expiredBuffer) RestoreFlash(r);
            flashing.Clear();
        }

        private static SpriteRenderer MainRenderer(Component target)
        {
            SpriteRenderer r = target.GetComponent<SpriteRenderer>();
            return r != null ? r : target.GetComponentInChildren<SpriteRenderer>();
        }

        // ------------------------------------------------------------------ gravity

        private void OnGravityChanged(Vector2Int down)
        {
            Pulse(gravityColor, gravityPulseAlpha, 0.55f);
            SpawnGravityDust(new Vector2(down.x, down.y));
            wasCoolingDown = true;
        }

        private void OnRotationBlocked(float remaining)
        {
            Pulse(blockedColor, 0.22f, 0.3f);
            ShowStatus(remaining > 0f ? $"Gravity recharging  {remaining:0.0}s" : "Gravity: stand on the ground", blockedColor);
        }

        /// <summary>Streaks around the player that fall in the new gravity direction, behind all characters.</summary>
        private void SpawnGravityDust(Vector2 down)
        {
            if (gravityDustCount <= 0) return;
            PlayerController2D p = FindPlayer();
            if (p == null) return;

            SpriteRenderer body = MainRenderer(p);
            int layer = body != null ? body.sortingLayerID : 0;
            Color color = new Color(gravityColor.r, gravityColor.g, gravityColor.b, 0.7f);

            ParticleSystem ps = VfxAssets.Burst(p.transform.position, Vector2.zero, color, gravityDustCount,
                0.3f, 360f, 0.1f, 0.6f, layer, VfxAssets.BehindCharactersOrder, 4f);

            var force = ps.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.x = down.x * 30f;
            force.y = down.y * 30f;
            force.z = 0f;

            // Stretch along the velocity so they read as "falling" speed lines.
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1f;
        }

        private void UpdateCooldownReady()
        {
            GravityRotator rotator = GravityRotator.Instance;
            bool coolingDown = rotator != null && rotator.CooldownRemaining > 0f;
            if (pulseWhenReady && wasCoolingDown && !coolingDown && rotator != null)
                Pulse(gravityColor, 0.15f, 0.3f);
            wasCoolingDown = coolingDown;
        }

        private PlayerController2D FindPlayer()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController2D>();
            return player;
        }

        // ------------------------------------------------------------------ pickups

        /// <summary>Burst in the item's rarity color plus floating text. Called by ItemPickup.</summary>
        public static void PickupCollected(ItemType type, Vector3 position, int sortingLayerId, int sortingOrder)
        {
            GameVfx vfx = EnsureInstance();
            Color color = VfxAssets.RarityColor(type);
            ItemRarity rarity = ItemLootTable.GetRarity(type);
            int count = rarity == ItemRarity.Rare ? 32 : rarity == ItemRarity.Uncommon ? 22 : 14;

            // Behind the pickup's own order, so the player touching it stays visible.
            VfxAssets.Burst(position, Vector2.zero, color, count, 4.5f, 360f, 0.2f, 0.5f,
                sortingLayerId, Mathf.Min(sortingOrder - 1, VfxAssets.BehindCharactersOrder), 0.15f);

            vfx.ShowFloatingText(ItemEffects.Describe(type), position, color);
        }

        // ------------------------------------------------------------------ overlay

        /// <summary>Text that pops up above a world position (on screen) and floats upward.</summary>
        public void ShowFloatingText(string message, Vector3 worldPosition, Color color, float duration = 1f)
        {
            if (string.IsNullOrEmpty(message)) return;
            EnsureOverlay();
            Camera cam = Camera.main;
            // Start a bit "above" on screen, so the text does not sit on the player.
            Vector3 anchor = worldPosition + (cam != null ? cam.transform.up : Vector3.up) * 0.9f;

            Text text = CreateText("Floating Text", 30, TextAnchor.MiddleCenter);
            RectTransform rect = text.rectTransform;
            rect.sizeDelta = new Vector2(400f, 50f);
            text.text = message;
            text.gameObject.AddComponent<FloatingText>().Init(text, canvas, anchor, color, duration, 70f);
        }

        private void ShowStatus(string message, Color color)
        {
            EnsureOverlay();
            statusText.text = message;
            statusColor = color;
            statusStart = Time.unscaledTime;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (statusText == null) return;
            float t = (Time.unscaledTime - statusStart) / StatusDuration;
            float alpha = t >= 1f ? 0f : t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            statusText.enabled = alpha > 0f;
            statusText.color = new Color(statusColor.r, statusColor.g, statusColor.b, alpha);
        }

        /// <summary>Colored glow along the screen edges. The center stays clear.</summary>
        public void Pulse(Color color, float alpha, float duration)
        {
            EnsureOverlay();
            pulseColor = color;
            pulseAlpha = Mathf.Clamp01(alpha);
            pulseDuration = Mathf.Max(0.05f, duration);
            pulseStart = Time.unscaledTime;
            UpdatePulse();
        }

        private void UpdatePulse()
        {
            if (pulseImage == null) return;
            float t = (Time.unscaledTime - pulseStart) / pulseDuration;
            float a = t >= 1f ? 0f : t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
            pulseImage.enabled = a > 0f;
            pulseImage.color = new Color(pulseColor.r, pulseColor.g, pulseColor.b, a * pulseAlpha);
        }

        private void EnsureOverlay()
        {
            if (canvas != null) return;

            var canvasObject = new GameObject("VFX Overlay", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;   // unaffected by the camera rotation
            canvas.sortingOrder = overlaySortingOrder;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            // No GraphicRaycaster: the overlay never blocks clicks.

            var pulseObject = new GameObject("Edge Pulse", typeof(RectTransform));
            pulseObject.transform.SetParent(canvasObject.transform, false);
            pulseImage = pulseObject.AddComponent<Image>();
            pulseImage.sprite = CreateEdgeSprite();
            pulseImage.raycastTarget = false;
            pulseImage.enabled = false;
            Stretch(pulseImage.rectTransform);

            statusText = CreateText("Status", 34, TextAnchor.UpperCenter);
            RectTransform statusRect = statusText.rectTransform;
            statusRect.anchorMin = statusRect.anchorMax = new Vector2(0.5f, 1f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.anchoredPosition = new Vector2(0f, -40f);
            statusRect.sizeDelta = new Vector2(1000f, 60f);
            statusText.enabled = false;
        }

        private Text CreateText(string name, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var text = go.AddComponent<Text>();
            text.font = VfxAssets.Font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>White frame that fades towards the center (transparent middle).</summary>
        private static Sprite CreateEdgeSprite()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VFX Edge Glow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
                    float dy = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                    float edge = Mathf.Max(dx, dy);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, edge));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
