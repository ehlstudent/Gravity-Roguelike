using GravityRoguelike.Combat;
using UnityEngine;

/// <summary>Gravity-independent enemy shot using the shared Health/DamageInfo hit system.</summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class BatProjectile : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float speed = 9f;
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(0.01f)] private float lifetime = 5f;
    [SerializeField, Min(0f)] private float knockback = 3.5f;
    [SerializeField] private LayerMask playerLayers = 1;
    [SerializeField] private LayerMask blockingLayers = 8;
    [Tooltip("Authored sprite's forward angle, before rotating it into the flight direction.")]
    [SerializeField] private float spriteForwardAngle = -45f;

    private Rigidbody2D body;
    private BoxCollider2D hitbox;
    private Transform owner;
    private Health ownerHealth;
    private ContactFilter2D filter;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    private bool launched;
    private bool spent;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        hitbox = GetComponent<BoxCollider2D>();
        body.gravityScale = 0f;
        filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(playerLayers | blockingLayers);
    }

    public void Launch(Transform shooter, Vector2 direction)
    {
        if (shooter == null || direction.sqrMagnitude < 0.000001f)
            throw new System.ArgumentException("Bat projectile requires an owner and a non-zero flight direction.");
        owner = shooter;
        ownerHealth = shooter.GetComponent<Health>();
        direction.Normalize();
        body.rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - spriteForwardAngle;
        body.linearVelocity = direction * speed;
        foreach (Collider2D collider in owner.GetComponentsInChildren<Collider2D>())
            Physics2D.IgnoreCollision(hitbox, collider);
        launched = true;
        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        if (!launched || spent) return;
        if (owner == null || ownerHealth != null && ownerHealth.IsDead) { Despawn(); return; }
        // Sweep the actual collider, like ArrowProjectile, so fast trigger shots don't tunnel.
        Vector2 velocity = body.linearVelocity;
        int count = hitbox.Cast(velocity.normalized, filter, hits, velocity.magnitude * Time.fixedDeltaTime);
        int nearest = -1;
        for (int i = 0; i < count; i++)
            if (CanHit(hits[i].collider) && (nearest < 0 || hits[i].distance < hits[nearest].distance)) nearest = i;
        if (nearest >= 0) Hit(hits[nearest].collider);
    }

    private bool CanHit(Collider2D other)
    {
        if (other == null || other == hitbox || owner == null || other.transform.IsChildOf(owner)) return false;
        int layer = 1 << other.gameObject.layer;
        if ((blockingLayers.value & layer) != 0 && !other.isTrigger) return true;
        return (playerLayers.value & layer) != 0 && other.GetComponentInParent<PlayerController2D>() != null &&
            other.GetComponentInParent<Health>() != null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (launched && !spent && CanHit(other)) Hit(other);
    }

    private void Hit(Collider2D other)
    {
        if (spent) return;
        // Mark spent first: multiple player colliders must never produce multiple hits.
        spent = true;
        if (owner != null && (ownerHealth == null || !ownerHealth.IsDead) &&
            other.GetComponentInParent<PlayerController2D>() != null)
        {
            Health target = other.GetComponentInParent<Health>();
            if (target != null) target.TakeDamage(new DamageInfo
            {
                amount = damage,
                sourcePosition = transform.position,
                knockbackForce = knockback
            });
        }
        Despawn();
    }

    private void Despawn()
    {
        spent = true;
        hitbox.enabled = false;
        body.linearVelocity = Vector2.zero;
        Destroy(gameObject);
    }
}
