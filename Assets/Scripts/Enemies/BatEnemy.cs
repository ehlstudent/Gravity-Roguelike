using GravityRoguelike.Combat;
using UnityEngine;

/// <summary>Animated sleep and ranged combat around the existing EnemyChase path follower.</summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(EnemyChase), typeof(Animator), typeof(BoxCollider2D))]
public class BatEnemy : Enemy
{
    [Header("Ranged attack")]
    [SerializeField] private BatProjectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField, Min(0.1f)] private float attackRange = 7f;
    [SerializeField, Min(0f)] private float attackCooldown = 1.5f;
    [Tooltip("0 stops during the attack; 1 keeps the normal chase speed.")]
    [SerializeField, Range(0f, 1f)] private float attackMovementMultiplier;
    [Tooltip("Scales the whole attack clip. At 1, frame 6 fires after a 0.5 second windup.")]
    [SerializeField, Min(0.01f)] private float attackAnimationSpeed = 1f;
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask blockingLayers = 8;

    [Header("Hit reaction")]
    [SerializeField, Min(0f)] private float receivedKnockback = 2f;
    [SerializeField, Min(0f)] private float hurtLockDuration = 0.65f;

    [Header("Facing and preview")]
    [SerializeField] private BoxCollider2D hurtbox;
    [SerializeField] private bool showAttackGizmos = true;

    // Values correspond to the Bat Animator's State parameter.
    private enum State { Sleep, Awake, Flying, Attack, Hurt, Dead }
    private State state;
    private EnemyChase chase;
    private Animator animator;
    private SpriteRenderer sprite;
    private BoxCollider2D bodyCollider;
    private Collider2D playerCollider;
    private Vector2 bodyOffset;
    private Vector2 hurtOffset;
    private Vector3 fireOffset;
    private Vector2 previousPosition;
    private float flightSpeed;
    private float nextAttackAt;
    private float nextHurtAt;
    private bool awakeCompleted;
    private bool projectileFired;

    protected override void Awake()
    {
        base.Awake();
        chase = GetComponent<EnemyChase>();
        animator = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<BoxCollider2D>();
        bodyOffset = bodyCollider.offset;
        if (hurtbox != null) hurtOffset = hurtbox.offset;
        if (firePoint != null) fireOffset = firePoint.localPosition;
        flightSpeed = chase.speed;
        previousPosition = rb.position;
        chase.enabled = false;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        animator.SetInteger("State", (int)State.Sleep);
    }

    private void Start()
    {
        if (roomGrid == null || player == null)
        {
            RoomGrid grid = FindFirstObjectByType<RoomGrid>();
            GameObject target = GameObject.FindWithTag("Player");
            if (grid != null && target != null) Init(grid, target.transform);
            else Debug.LogWarning("Bat needs a RoomGrid and an active Player, or EnemySpawner.Init, to fly and attack.", this);
        }
        if (projectilePrefab == null || firePoint == null)
            Debug.LogWarning("Bat needs a projectile prefab and FirePoint to shoot.", this);
    }

    private void OnEnable()
    {
        GravityRotator.GravityChanged += OnGravityChanged;
        AlignWithGravity(Vector2Int.RoundToInt(Physics2D.gravity.normalized));
        UpdateMovement();
    }

    private void OnDisable()
    {
        GravityRotator.GravityChanged -= OnGravityChanged;
        if (chase != null) chase.enabled = false;
    }

    protected override void OnInitialized()
    {
        chase.Init(roomGrid, player);
        playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
        UpdateMovement();
    }

    // Match the original bat's ceiling-spawn rule without changing its pathfinding.
    public override bool CanSpawnAt(RoomGrid grid, Vector2Int cell) =>
        !grid.IsSolid(cell) && grid.IsSolid(cell - grid.gravity);

    private void OnGravityChanged(Vector2Int down)
    {
        AlignWithGravity(down);
        if (state == State.Sleep) SetState(State.Awake);
    }

    private void AlignWithGravity(Vector2Int down)
    {
        if (down == Vector2Int.zero) return;
        float angle = Mathf.Atan2(down.y, down.x) * Mathf.Rad2Deg + 90f;
        rb.rotation = angle;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Vector2 TargetCenter => playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)player.position;

    private void FixedUpdate()
    {
        if (state != State.Flying || player == null || roomGrid == null || IsStunned) return;
        if (Time.time < nextAttackAt || projectilePrefab == null || firePoint == null) return;
        if (Vector2.Distance(rb.position, TargetCenter) > attackRange) return;
        Face(TargetCenter - rb.position);
        if (!HasClearShot()) return;
        projectileFired = false;
        animator.SetFloat("AttackSpeed", Mathf.Max(0.01f, attackAnimationSpeed));
        SetState(State.Attack);
    }

    private void LateUpdate()
    {
        Vector2 movement = rb.position - previousPosition;
        previousPosition = rb.position;
        if (state == State.Attack && player != null) Face(TargetCenter - rb.position);
        else if (state == State.Flying)
            Face(movement.sqrMagnitude > 0.000001f ? movement : player != null ? TargetCenter - rb.position : Vector2.zero);
    }

    // SpriteRenderer.flipX doesn't flip colliders: mirror their authored offsets with the mouth.
    // Root rotation then carries all of them into the current gravity orientation.
    private void Face(Vector2 worldDirection)
    {
        float horizontal = transform.InverseTransformDirection(worldDirection).x;
        if (Mathf.Abs(horizontal) < 0.001f) return;
        float sign = horizontal < 0f ? -1f : 1f;
        sprite.flipX = sign < 0f;
        bodyCollider.offset = new Vector2(bodyOffset.x * sign, bodyOffset.y);
        if (hurtbox != null) hurtbox.offset = new Vector2(hurtOffset.x * sign, hurtOffset.y);
        if (firePoint != null) firePoint.localPosition = new Vector3(fireOffset.x * sign, fireOffset.y, fireOffset.z);
    }

    private bool HasClearShot()
    {
        if (!requireLineOfSight) return true;
        // Check the short mouth offset too, so an offset fire point cannot shoot through a wall.
        return !Physics2D.Linecast(rb.position, firePoint.position, blockingLayers) &&
            !Physics2D.Linecast(firePoint.position, TargetCenter, blockingLayers);
    }

    private void SetState(State next)
    {
        if (state == State.Dead) return;
        state = next;
        animator.SetInteger("State", (int)state);
        rb.constraints = state == State.Sleep || state == State.Awake
            ? RigidbodyConstraints2D.FreezeAll : RigidbodyConstraints2D.FreezeRotation;
        UpdateMovement();
    }

    private void UpdateMovement()
    {
        chase.speed = state == State.Attack ? flightSpeed * attackMovementMultiplier : flightSpeed;
        chase.enabled = isActiveAndEnabled && roomGrid != null && player != null &&
            (state == State.Flying || state == State.Attack && attackMovementMultiplier > 0f);
        if (!chase.enabled && state != State.Hurt && state != State.Dead) rb.linearVelocity = Vector2.zero;
    }

    // Animation events: no movement until waking ends; aim is sampled at attack frame 6.
    public void FinishAwake()
    {
        if (state != State.Awake) return;
        awakeCompleted = true;
        SetState(State.Flying);
    }

    public void FireProjectile()
    {
        if (state != State.Attack || projectileFired || health.IsDead || player == null) return;
        projectileFired = true;
        if (projectilePrefab == null || firePoint == null) return;
        Face(TargetCenter - rb.position);
        Physics2D.SyncTransforms();
        if (!HasClearShot()) return;
        Vector2 direction = TargetCenter - (Vector2)firePoint.position;
        if (direction.sqrMagnitude < 0.000001f) return;
        Instantiate(projectilePrefab, firePoint.position, Quaternion.identity).Launch(transform, direction);
    }

    public void FinishAttack()
    {
        if (state != State.Attack) return;
        nextAttackAt = Time.time + attackCooldown;
        SetState(State.Flying);
    }

    protected override void OnDamaged(DamageInfo info)
    {
        if (health.IsDead || state == State.Dead) return;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        // Replace the base impulse with this bat's small, tunable physical reaction.
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(((Vector2)transform.position - info.sourcePosition).normalized * receivedKnockback, ForceMode2D.Impulse);
        if (Time.time < nextHurtAt || state == State.Attack && projectileFired) return;
        nextHurtAt = Time.time + hurtLockDuration;
        nextAttackAt = Time.time + attackCooldown;
        SetState(State.Hurt);
    }

    public void FinishHurt()
    {
        if (state != State.Hurt) return;
        SetState(awakeCompleted ? State.Flying : State.Awake);
    }

    protected override void OnDeath()
    {
        if (state == State.Dead) return;
        SetState(State.Dead);
        GravityRotator.GravityChanged -= OnGravityChanged;
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
    }

    public void FinishDeath()
    {
        if (state == State.Dead) Destroy(gameObject);
    }

    // The projectile is the only damage source; touching the bat does not hurt the player.
    protected override void OnCollisionStay2D(Collision2D collision) { }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showAttackGizmos) return;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        if (firePoint == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(firePoint.position, 0.08f);
        if (player != null)
        {
            Gizmos.color = requireLineOfSight && !HasClearShot() ? Color.red : Color.green;
            Gizmos.DrawLine(transform.position, firePoint.position);
            Gizmos.DrawLine(firePoint.position, TargetCenter);
        }
        else Gizmos.DrawLine(firePoint.position, firePoint.position + transform.right * attackRange);
    }
#endif
}
