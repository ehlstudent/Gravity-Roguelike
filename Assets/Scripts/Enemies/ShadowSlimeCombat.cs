using System.Collections.Generic;
using GravityRoguelike.Combat;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(ShadowSlimeEnemy), typeof(Animator))]
public class ShadowSlimeCombat : MonoBehaviour
{
    [SerializeField, Min(0)] private float attackTriggerRange = 2.2f;
    [SerializeField, Min(0)] private float attackCooldown = 1f;
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(0)] private float attackKnockback = 5f;
    [SerializeField, Min(0)] private float receivedKnockback = 2f;
    [SerializeField, Min(0)] private float hurtLockDuration = 0.5f;
    [SerializeField] private Vector2 spikeOrigin = new Vector2(0, 0.28f);
    [Tooltip("Local units; the prefab's scale also scales the art and these hitboxes.")]
    [SerializeField, Min(0.01f)] private float spikeLength = 2.25f;
    [SerializeField, Min(0.01f)] private float spikeWidth = 0.12f;
    [SerializeField] private LayerMask playerLayers = 1;
    [SerializeField] private LayerMask blockingLayers = 8;
    [SerializeField] private bool showAttackGizmos = true;

    private enum State { Chase, Attack, Cooldown, Hurt, Dead }
    private State state;
    private ShadowSlimeEnemy walker;
    private Rigidbody2D body;
    private Health health;
    private Animator animator;
    private SpriteRenderer sprite;
    private Transform target;
    private bool spikesActive;
    private bool hitPlayer;
    private float nextAttackAt;
    private float nextHurtAt;
    private readonly List<Collider2D> hits = new List<Collider2D>(8);

    private void Awake()
    {
        walker = GetComponent<ShadowSlimeEnemy>();
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        animator = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
    }

    public void Bind(Transform player) => target = player;

    private void OnEnable()
    {
        GravityRotator.GravityChanged += AlignWithGravity;
        AlignWithGravity(Vector2Int.RoundToInt(Physics2D.gravity.normalized));
    }

    private void AlignWithGravity(Vector2Int down)
    {
        if (down == Vector2Int.zero) return;
        float angle = Mathf.Atan2(down.y, down.x) * Mathf.Rad2Deg + 90f;
        // Rotate the animated sprite and its local hitboxes together, including during attacks/death.
        body.rotation = angle;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void FixedUpdate()
    {
        if (state == State.Dead || target == null) return;
        Vector2 up = Physics2D.gravity.sqrMagnitude > 0 ? -Physics2D.gravity.normalized : Vector2.up;
        Vector2 tangent = new Vector2(up.y, -up.x);
        if (state == State.Chase && Vector2.Distance(Origin, target.position) <= attackTriggerRange
            && !Physics2D.Linecast(Origin, target.position, blockingLayers))
        {
            if (Time.time >= nextAttackAt && !walker.IsStunned)
            {
                state = State.Attack;
                spikesActive = hitPlayer = false;
                walker.enabled = false;
                body.linearVelocity = up * Vector2.Dot(body.linearVelocity, up);
                animator.SetTrigger("Attack");
            }
            else walker.enabled = false;
        }
        else if (state == State.Chase) walker.enabled = true;
        if (!walker.IsStunned && (state == State.Attack || state == State.Cooldown || (state == State.Chase && !walker.enabled)))
            body.linearVelocity = up * Vector2.Dot(body.linearVelocity, up);
        if (state == State.Cooldown && Time.time >= nextAttackAt) state = State.Chase;
        if (state == State.Chase && Mathf.Abs(Vector2.Dot(body.linearVelocity, tangent)) > 0.05f)
            sprite.flipX = Vector2.Dot(body.linearVelocity, tangent) < 0;
        animator.SetFloat("Speed", state == State.Chase ? Mathf.Abs(Vector2.Dot(body.linearVelocity, tangent)) : 0);
    }

    private Vector2 Origin => transform.TransformPoint(spikeOrigin);

    private void GetSpoke(int index, out Vector2 center, out Vector2 size, out float angle)
    {
        float radians = index * Mathf.PI / 4;
        Vector3 reach = transform.TransformVector(new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0) * spikeLength);
        center = Origin + (Vector2)reach * 0.5f;
        size = new Vector2(reach.magnitude, spikeWidth * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)));
        angle = Mathf.Atan2(reach.y, reach.x) * Mathf.Rad2Deg;
    }

    // Animation events: attack frames 4-5 only (one-based).
    public void ExtendSpikes()
    {
        if (state != State.Attack || health.IsDead) return;
        spikesActive = true;
        DamagePlayer();
    }
    public void RetractSpikes() => spikesActive = false;
    private void LateUpdate() { if (spikesActive) DamagePlayer(); }

    private void DamagePlayer()
    {
        if (hitPlayer || health.IsDead || target == null) return;
        Physics2D.SyncTransforms();
        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(playerLayers);
        for (int i = 0; i < 8; i++)
        {
            GetSpoke(i, out Vector2 center, out Vector2 size, out float angle);
            hits.Clear();
            Physics2D.OverlapBox(center, size, angle, filter, hits);
            foreach (Collider2D hit in hits)
            {
                var player = hit.GetComponentInParent<PlayerController2D>();
                if (player == null || player.transform != target) continue;
                Health receiver = player.GetComponent<Health>();
                if (receiver == null || receiver.IsDead || Physics2D.Linecast(Origin, hit.bounds.center, blockingLayers)) continue;
                // Consume the attack even if player immunity rejects the damage.
                hitPlayer = true;
                receiver.TakeDamage(new DamageInfo { amount = damage, sourcePosition = Origin, knockbackForce = attackKnockback });
                return;
            }
        }
    }

    public void FinishAttack()
    {
        if (state != State.Attack) return;
        spikesActive = false;
        state = State.Cooldown;
        nextAttackAt = Time.time + attackCooldown;
    }

    public void ReceiveHit(DamageInfo info)
    {
        if (health.IsDead || state == State.Dead) return;
        if (info.knockbackForce <= 0)
            body.AddForce(((Vector2)transform.position - info.sourcePosition).normalized * receivedKnockback, ForceMode2D.Impulse);
        if (Time.time < nextHurtAt || spikesActive) return;
        nextHurtAt = Time.time + hurtLockDuration;
        RetractSpikes();
        state = State.Hurt;
        walker.enabled = false;
        animator.ResetTrigger("Attack");
        animator.SetTrigger("Hurt");
        animator.Update(0); // Interrupt the windup on the damage frame.
    }

    public void FinishHurt()
    {
        if (state != State.Hurt) return;
        state = State.Cooldown;
        nextAttackAt = Mathf.Max(nextAttackAt, Time.time + 0.15f);
    }

    public void BeginDeath()
    {
        if (state == State.Dead) return;
        state = State.Dead;
        RetractSpikes();
        walker.enabled = false;
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        body.linearVelocity = Vector2.zero;
        body.simulated = false;
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("Hurt");
        animator.SetBool("Dead", true);
        animator.SetTrigger("Die");
        animator.Update(0);
    }

    public void FinishDeath() { if (state == State.Dead) Destroy(gameObject); }
    private void OnDisable()
    {
        GravityRotator.GravityChanged -= AlignWithGravity;
        spikesActive = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showAttackGizmos) return;
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Origin, attackTriggerRange);
        Gizmos.color = Color.red;
        for (int i = 0; i < 8; i++)
        {
            GetSpoke(i, out Vector2 center, out Vector2 size, out float angle);
            Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0, 0, angle), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
        Gizmos.matrix = previous;
    }
#endif
}
