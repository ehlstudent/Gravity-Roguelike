using UnityEngine;

namespace GravityRoguelike.Combat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class ArrowProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float speed = 20f;
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Min(0.01f)] private float lifetime = 3f;
        [Tooltip("Despawn margin in viewport units. Damage always requires the actual camera view.")]
        [SerializeField, Min(0f)] private float viewportMargin = 0f;
        [SerializeField] private LayerMask collisionLayers = ~0;
        [Tooltip("Layers damaged by an explosion (explosions also hurt the player, so include the player layer).")]
        [SerializeField] private LayerMask explosionLayers = ~0;
        [Tooltip("After its first wall bounce, the blob can hit (and damage) the player who fired it.")]
        [SerializeField] private bool hurtOwnerAfterBounce = true;
        [Tooltip("Logs the reason every time a blob disappears.")]
        [SerializeField] private bool debugDespawn;
        private Rigidbody2D body;
        // Set by upgrades via ConfigureUpgrades(). 0 means: no bounce / no explosion.
        private int bouncesLeft;
        private bool hasBounced;
        private float explosionRadius;
        private BoxCollider2D hitbox;
        private Transform owner;
        private Camera viewCamera;
        private ContactFilter2D filter;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
        private bool launched;
        private bool spent;

        public float Speed => speed;
        public Vector2 CollisionSize => GetComponent<BoxCollider2D>().size;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<BoxCollider2D>();
            filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(collisionLayers);
        }

        /// <summary>Sets upgrade effects: number of wall bounces and explosion radius (0 = no explosion).</summary>
        public void ConfigureUpgrades(int bounces, float radius)
        {
            bouncesLeft = Mathf.Max(0, bounces);
            explosionRadius = Mathf.Max(0f, radius);
        }

        public void Launch(Transform shooter, Vector2 direction, Camera camera)
        {
            if (shooter == null || camera == null || direction.sqrMagnitude < 0.000001f)
                throw new System.ArgumentException("Arrow requires an owner, a camera and a non-zero flight direction.");
            owner = shooter;
            viewCamera = camera;
            direction.Normalize();
            body.rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            body.linearVelocity = direction * speed;
            foreach (Collider2D ownCollider in owner.GetComponentsInChildren<Collider2D>())
                Physics2D.IgnoreCollision(hitbox, ownCollider);
            launched = true;
            Destroy(gameObject, lifetime);
        }

        private void Update()
        {
            if (launched && !spent && !IsPointInView(viewCamera, transform.position, viewportMargin)) Despawn("left the camera view");
        }

        private void FixedUpdate()
        {
            if (!launched || spent) return;
            // Sweep the real arrow collider too: fast trigger projectiles must not tunnel through walls.
            Vector2 velocity = body.linearVelocity;
            int count = hitbox.Cast(velocity.normalized, filter, hits, velocity.magnitude * Time.fixedDeltaTime);
            int nearest = -1;
            for (int i = 0; i < count; i++)
                if (CanHit(hits[i].collider) && (nearest < 0 || hits[i].distance < hits[nearest].distance)) nearest = i;
            if (nearest >= 0) Hit(hits[nearest].collider, hits[nearest].point, hits[nearest].normal);
        }

        private bool CanHit(Collider2D other)
        {
            // Blobs fired together must never hit each other.
            if (other != null && other.GetComponentInParent<ArrowProjectile>() != null) return false;
            // The shooter is only hittable after a bounce (otherwise every blob would hit the mage on spawn).
            bool isOwner = other != null && other.transform.IsChildOf(owner);
            if (isOwner && !(hurtOwnerAfterBounce && hasBounced)) return false;
            if (other == null || other == hitbox ||
                (collisionLayers.value & (1 << other.gameObject.layer)) == 0) return false;
            // Ignore non-damageable trigger volumes such as pickups and portals.
            return !other.isTrigger || other.GetComponentInParent<Health>() != null;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!launched || spent || !CanHit(other)) return;
            // While bounces remain, walls are handled by the sweep in FixedUpdate (it knows the surface normal).
            if (bouncesLeft > 0 && other.GetComponentInParent<Health>() == null) return;
            Hit(other, other.ClosestPoint(transform.position), Vector2.zero);
        }

        // Handles one hit: bounce off a wall, or explode / deal damage and despawn.
        private void Hit(Collider2D other, Vector2 point, Vector2 normal)
        {
            if (spent) return;
            Health health = other.GetComponentInParent<Health>();

            // Wall (no Health) with bounces left: reflect instead of despawning.
            if (health == null && bouncesLeft > 0 && normal.sqrMagnitude > 0.5f)
            {
                // Only bounce if we actually fly into the surface (avoids re-hitting the wall we just left).
                if (Vector2.Dot(body.linearVelocity, normal) < 0f) Bounce(normal);
                return;
            }

            if (explosionRadius > 0f)
            {
                // The explosion replaces the direct hit damage, so a target is never damaged twice.
                Explode(transform.position);
            }
            // Renderer visibility is checked against the gameplay camera, never Scene-view isVisible.
            else if (health != null && IsPointInView(viewCamera, point, 0f) && IsTargetVisible(viewCamera, health))
                health.TakeDamage(new DamageInfo { amount = damage, sourcePosition = owner.position });
            Despawn("hit " + other.name);
        }

        // Reflects the velocity on the wall normal, keeps the speed and uses up one bounce.
        private void Bounce(Vector2 normal)
        {
            bouncesLeft--;
            if (hurtOwnerAfterBounce && !hasBounced)
            {
                hasBounced = true;
                // Launch() disabled collisions with the shooter; turn them back on now.
                foreach (Collider2D ownCollider in owner.GetComponentsInChildren<Collider2D>())
                    Physics2D.IgnoreCollision(hitbox, ownCollider, false);
            }
            Vector2 velocity = Vector2.Reflect(body.linearVelocity, normal);
            body.linearVelocity = velocity;
            body.rotation = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            // Nudge out of the wall so the next sweep doesn't start inside it.
            body.position += normal * 0.02f;
        }

        // Damages every Health in the radius (including the player) with the constant projectile damage.
        private void Explode(Vector2 center)
        {
            var damaged = new System.Collections.Generic.HashSet<Health>();
            foreach (Collider2D target in Physics2D.OverlapCircleAll(center, explosionRadius, explosionLayers))
            {
                Health health = target.GetComponentInParent<Health>();
                if (health == null || !health.isActiveAndEnabled || !damaged.Add(health)) continue;
                if (!IsTargetVisible(viewCamera, health)) continue;
                health.TakeDamage(new DamageInfo { amount = damage, sourcePosition = center });
            }
            SpawnExplosionPlaceholder(center, explosionRadius);
        }

        // PLACEHOLDER visual: a red circle outline that disappears after a short time. Replace with a real effect later.
        private static void SpawnExplosionPlaceholder(Vector2 center, float radius)
        {
            const int segments = 32;
            var go = new GameObject("ExplosionPlaceholder");
            go.transform.position = center;
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segments;
            line.startWidth = line.endWidth = 0.08f;
            line.sortingOrder = 100;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = Color.red;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }
            Destroy(go, 0.25f);
        }

        // Logs why a blob vanished (enable "debugDespawn"), so mid-air disappearances can be traced.
        private void OnDestroy()
        {
            if (debugDespawn && !spent) Debug.Log($"{name}: destroyed by lifetime ({lifetime}s) at {transform.position}", this);
        }

        // Removes the blob; the reason is only used for the debug log.
        private void Despawn(string reason)
        {
            if (debugDespawn) Debug.Log($"{name}: despawned - {reason} at {transform.position}", this);
            spent = true;
            body.linearVelocity = Vector2.zero;
            hitbox.enabled = false;
            Destroy(gameObject);
        }

        public static bool IsPointInView(Camera camera, Vector3 position, float margin)
        {
            if (camera == null || !camera.isActiveAndEnabled) return false;
            Vector3 p = camera.WorldToViewportPoint(position);
            return p.z >= camera.nearClipPlane && p.z <= camera.farClipPlane &&
                p.x >= -margin && p.x <= 1f + margin && p.y >= -margin && p.y <= 1f + margin;
        }

        public static bool IsTargetVisible(Camera camera, Health target)
        {
            if (camera == null || !camera.isActiveAndEnabled || target == null || !target.isActiveAndEnabled) return false;
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                if (renderer is SpriteRenderer sprite && (sprite.sprite == null || sprite.color.a <= 0f)) continue;
                Bounds b = renderer.bounds;
                Vector3 min = Vector3.one * float.PositiveInfinity;
                Vector3 max = Vector3.one * float.NegativeInfinity;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 world = new Vector3((corner & 1) == 0 ? b.min.x : b.max.x,
                        (corner & 2) == 0 ? b.min.y : b.max.y, (corner & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 p = camera.WorldToViewportPoint(world);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                if (max.z >= camera.nearClipPlane && min.z <= camera.farClipPlane &&
                    max.x >= 0f && min.x <= 1f && max.y >= 0f && min.y <= 1f) return true;
            }
            return false;
        }
    }
}