using System;
using UnityEngine;
using GravityRoguelike.Combat;

// Rigidbody2D for knockback, Health for the numbers (both added automatically if missing)
[RequireComponent(typeof(Rigidbody2D), typeof(Health))]
public abstract class Enemy : MonoBehaviour
{
    [Header("Contact attack")]
    [SerializeField] protected int contactDamage = 1;        // damage dealt when touching the player
    [SerializeField] protected float contactKnockback = 6f;  // push applied to the player

    [Header("Hit reaction")]
    [SerializeField] protected float stunDuration = 0.25f;   // seconds the AI pauses after a hit

    protected Health health;       // the shared health pool (also read by the sword)
    protected Rigidbody2D rb;      // cached for knockback
    private float stunnedUntil;    // Time.time until which this enemy is stunned

    // True while stunned: movement scripts check this before steering
    public bool IsStunned => Time.time < stunnedUntil;

    // Room, loot or UI can subscribe to learn about deaths
    public event Action<Enemy> Died;

    // Caches components and subscribes to Health. Subclasses must call base.Awake()
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        health.Damaged += HandleDamaged;
        health.Died += HandleDied;
    }

    // Unsubscribes so no event keeps pointing at a destroyed object
    protected virtual void OnDestroy()
    {
        if (health == null) return;
        health.Damaged -= HandleDamaged;
        health.Died -= HandleDied;
    }

    // Health says we were hit: knock back, stun, then let the subclass react
    private void HandleDamaged(DamageInfo info)
    {
        ApplyKnockback(info);
        stunnedUntil = Time.time + stunDuration;
        OnDamaged(info);
    }

    // Health says we died: notify listeners, then the subclass hook
    private void HandleDied()
    {
        Died?.Invoke(this);
        OnDeath();
    }

    // Pushes away from the hit source, whatever direction gravity points
    private void ApplyKnockback(DamageInfo info)
    {
        if (info.knockbackForce <= 0f || rb.bodyType != RigidbodyType2D.Dynamic) return;

        Vector2 direction = ((Vector2)transform.position - info.sourcePosition).normalized;
        rb.linearVelocity = Vector2.zero;   // cancel motion so the push is consistent
        rb.AddForce(direction * info.knockbackForce, ForceMode2D.Impulse);
    }

    // Hook for subclasses (wake up, flash, sound). Empty by default
    protected virtual void OnDamaged(DamageInfo info) { }

    // Hook for subclasses (drops, effects). Health deactivates the object afterwards
    protected virtual void OnDeath() { }

    // While touching the player, hurts them via their Health
    protected virtual void OnCollisionStay2D(Collision2D col)
    {
        if (!col.gameObject.CompareTag("Player")) return;

        Health target = col.gameObject.GetComponentInParent<Health>();
        if (target == null) return;

        target.TakeDamage(new DamageInfo
        {
            amount = contactDamage,
            sourcePosition = transform.position,
            knockbackForce = contactKnockback
        });
    }
}