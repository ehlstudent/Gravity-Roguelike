using GravityRoguelike.Items;
using UnityEngine;

namespace GravityRoguelike.VFX
{
    /// <summary>
    /// Shared, code-generated VFX resources (textures, materials, font) and small helpers.
    /// Everything is created on first use, so no prefabs or assets have to be wired up.
    /// </summary>
    public static class VfxAssets
    {
        /// <summary>
        /// Sorting order for effects that must stay behind characters (player/enemies use 0,
        /// the sword 5, the slash 6). Effects never cover the bodies we want to read.
        /// </summary>
        public const int BehindCharactersOrder = -1;

        private static Sprite softCircle;
        private static Material particleMaterial;
        private static Material glowMaterial;
        private static Material flashMaterial;
        private static bool flashShaderMissingLogged;
        private static Font font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Generated objects are destroyed between Play sessions; forget the stale references.
            softCircle = null;
            particleMaterial = null;
            glowMaterial = null;
            flashMaterial = null;
            flashShaderMissingLogged = false;
            font = null;
        }

        /// <summary>Round sprite with a soft edge, used for particles and glows.</summary>
        public static Sprite SoftCircle
        {
            get
            {
                if (softCircle != null) return softCircle;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "VFX Soft Circle",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f;
                        float dy = (y + 0.5f) / size * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a; // soft falloff
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                softCircle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                softCircle.name = "VFX Soft Circle";
                return softCircle;
            }
        }

        /// <summary>Unlit sprite material (Sprites/Default) with the soft circle, for particles.</summary>
        public static Material ParticleMaterial
        {
            get
            {
                if (particleMaterial != null) return particleMaterial;
                particleMaterial = new Material(FindUnlitSpriteShader()) { name = "VFX Particles" };
                particleMaterial.mainTexture = SoftCircle.texture;
                return particleMaterial;
            }
        }

        /// <summary>Unlit material for glows, so they stay visible in dark caves.</summary>
        public static Material GlowMaterial
        {
            get
            {
                if (glowMaterial != null) return glowMaterial;
                glowMaterial = new Material(FindUnlitSpriteShader()) { name = "VFX Glow" };
                return glowMaterial;
            }
        }

        /// <summary>Silhouette material for hit flashes; null if the shader is missing.</summary>
        public static Material FlashMaterial
        {
            get
            {
                if (flashMaterial != null) return flashMaterial;
                Shader shader = Resources.Load<Shader>("VFX/SpriteFlash");
                if (shader == null || !shader.isSupported)
                {
                    if (!flashShaderMissingLogged)
                        Debug.LogWarning("VfxAssets: Resources/VFX/SpriteFlash.shader not found or unsupported; hit flash falls back to a color tint.");
                    flashShaderMissingLogged = true;
                    return null;
                }
                flashMaterial = new Material(shader) { name = "VFX Hit Flash" };
                return flashMaterial;
            }
        }

        /// <summary>Built-in font that needs no imported assets.</summary>
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Color RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return new Color(0.35f, 1f, 0.45f);  // green
                case ItemRarity.Rare: return new Color(0.78f, 0.45f, 1f);      // purple
                default: return new Color(0.9f, 0.95f, 1f);                     // common: soft white
            }
        }

        public static Color RarityColor(ItemType type) => RarityColor(ItemLootTable.GetRarity(type));

        /// <summary>
        /// One-shot particle burst. Particles fly out in a cone around <paramref name="direction"/>
        /// (a zero direction means all around), shrink and fade, then the object destroys itself.
        /// </summary>
        public static ParticleSystem Burst(Vector2 position, Vector2 direction, Color color, int count,
            float speed, float arcDegrees, float size, float lifetime, int sortingLayerId, int sortingOrder,
            float radius = 0.05f)
        {
            var go = new GameObject("VFX Burst");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = lifetime;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = color;
            main.gravityModifier = 0f;   // Physics.gravity is 3D and ignores our rotated 2D gravity
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(1, count);
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, count)) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;   // emits in the XY plane
            shape.radius = Mathf.Max(0.01f, radius);
            shape.radiusThickness = 1f;
            if (direction.sqrMagnitude > 0.0001f && arcDegrees < 360f)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                shape.arc = arcDegrees;
                shape.rotation = new Vector3(0f, 0f, angle - arcDegrees * 0.5f);
            }
            else
            {
                shape.arc = 360f;
            }

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;

            ps.Play();
            return ps;
        }

        private static Shader FindUnlitSpriteShader()
        {
            // Sprites/Default is in "Always Included Shaders"; the URP unlit sprite is the fallback.
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            return shader;
        }
    }
}
